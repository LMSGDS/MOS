"""Grade every MOS Word training pack with generated demo action evidence."""
from __future__ import annotations

import argparse
import json
import mimetypes
import uuid
from pathlib import Path
from typing import Any
from urllib import error, request

from app.grade import GRADER_VERSION, grade_path, load_rubric

ROOT = Path(__file__).resolve().parent.parent
RUBRIC_DIR = ROOT / "app" / "rubrics"
FIXTURES = ROOT / "tests" / "fixtures"

ACTION_NAME = {
    "search_query": "find",
    "find": "find",
    "results_tab": "results_tab",
    "search_navigate": "find_navigate",
    "find_navigate": "find_navigate",
    "advanced_find": "advanced_find",
    "goto_graphic": "goto_graphic",
    "goto_page": "goto_page",
    "goto_bookmark": "goto_bookmark",
    "save_alternate_format": "save_alternate_format",
    "print_settings": "print_settings",
    "share_electronic": "share_electronic",
    "inspect_document": "inspect_document",
    "compatibility_check": "compatibility_check",
}

WORD_MIME = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"


def evidence_from_rubric(rubric: dict) -> list[dict]:
    events: list[dict] = []
    for item in rubric.get("criteria") or []:
        if str(item.get("kind") or "").casefold() != "action_sequence":
            continue
        pred = item.get("predicate") or {}
        sel = item.get("selector") or {}
        kind = str(pred.get("type") or sel.get("action") or "").casefold()
        action = ACTION_NAME.get(kind, kind or "event")
        event: dict[str, Any] = {
            "action": action,
            "source": sel.get("source") or "demo",
            "skill": item.get("id") or "",
        }
        query = pred.get("query") if pred.get("query") is not None else sel.get("query")
        if query:
            event["query"] = query
        style = pred.get("style") if pred.get("style") is not None else sel.get("style")
        if style:
            event["style"] = style
        if pred.get("match_case") or sel.get("match_case"):
            event["match_case"] = True
        if pred.get("whole_word") or sel.get("whole_word"):
            event["whole_word"] = True
        page = pred.get("page") if pred.get("page") is not None else sel.get("page")
        if page is not None:
            event["page"] = page
        name = pred.get("name") or sel.get("name") or sel.get("bookmark")
        if name:
            event["name"] = name
        event["hits"] = max(int(pred.get("min_hits") or pred.get("min") or 1), 1)
        if action == "save_alternate_format":
            event["format"] = pred.get("format") or sel.get("format") or "pdf"
        events.append(event)
    return events


def _docx_pair(folder: Path, project_id: str) -> tuple[Path, Path]:
    tail = project_id.removeprefix("word-objective-")
    starter = folder / f"Word_{tail}.docx"
    results = folder / f"Word_{tail}_results.docx"
    if not starter.is_file():
        starter = next(p for p in folder.glob("Word_*.docx") if "_results" not in p.name)
    if not results.is_file():
        results = next(folder.glob("Word_*_results.docx"))
    return starter, results


def iter_packs() -> list[dict]:
    packs = []
    for path in sorted(RUBRIC_DIR.glob("word-objective-*.json")):
        rubric = load_rubric(path)
        project_id = str(rubric.get("project_id") or path.stem)
        folder = FIXTURES / project_id
        starter, results = _docx_pair(folder, project_id)
        actions = evidence_from_rubric(rubric)
        packs.append(
            {
                "project_id": project_id,
                "title": rubric.get("title") or project_id,
                "objective": rubric.get("objective") or project_id.removeprefix("word-objective-"),
                "rubric": rubric,
                "starter": starter,
                "results": results,
                "evidence": actions,
                "action_count": len(actions),
                "criterion_count": len(rubric.get("criteria") or []),
            }
        )
    return packs


def grade_pack(pack: dict) -> dict:
    rubric = pack["rubric"]
    starter = grade_path(pack["starter"], rubric)
    artifact = grade_path(pack["results"], rubric)
    demo = grade_path(pack["results"], rubric, evidence=pack["evidence"] or None)
    failed = [
        {
            "id": item["criterion_id"],
            "status": item["status"],
            "reason": item.get("reason_code"),
            "message": item.get("message"),
        }
        for item in demo.get("criteria") or []
        if item.get("status") != "pass"
    ]
    return {
        "project_id": pack["project_id"],
        "title": pack["title"],
        "objective": pack["objective"],
        "criterion_count": pack["criterion_count"],
        "action_count": pack["action_count"],
        "starter_verified": starter.get("verified"),
        "starter_pending": starter.get("pending"),
        "artifact_verified": artifact.get("verified"),
        "artifact_pending": artifact.get("pending"),
        "demo_verified": demo.get("verified"),
        "demo_pending": demo.get("pending"),
        "demo_score": demo.get("score"),
        "complete": bool(demo.get("complete")),
        "grader_version": demo.get("grader_version") or GRADER_VERSION,
        "failed": failed,
    }


def run_all() -> list[dict]:
    return [grade_pack(pack) for pack in iter_packs()]


def format_report(rows: list[dict]) -> str:
    header = f"{'Mã':<22} {'Đề gốc':>8} {'File nộp':>10} {'Demo':>8} {'Pending':>8} {'Thao tác':>8} {'KQ':<8} Tiêu đề"
    lines = [
        f"MOS-KulKul demo toàn bộ bài Word — grader {GRADER_VERSION}",
        header,
        "-" * len(header),
    ]
    ok = 0
    for row in rows:
        mark = "ĐẠT" if row["complete"] and float(row["demo_verified"] or 0) == 100 else "LỖI"
        if mark == "ĐẠT":
            ok += 1
        lines.append(
            f"{row['project_id']:<22} {float(row['starter_verified'] or 0):8.0f} "
            f"{float(row['artifact_verified'] or 0):10.0f} {float(row['demo_verified'] or 0):8.0f} "
            f"{float(row['demo_pending'] or 0):8.0f} {row['action_count']:8d} {mark:<8} {row['title']}"
        )
        for fail in row.get("failed") or []:
            lines.append(f"    · {fail['id']} {fail['status']} {fail.get('reason') or ''}")
    lines.append("-" * len(header))
    lines.append(f"{ok}/{len(rows)} bài đạt 100 sau demo.")
    return "\n".join(lines)


def _api(method: str, url: str, token: str | None = None, json_body=None, files: dict | None = None):
    headers = {}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    data = None
    if json_body is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(json_body).encode("utf-8")
    if files is not None:
        boundary = uuid.uuid4().hex
        chunks: list[bytes] = []
        for key, (filename, content, ctype) in files.items():
            chunks.append(
                (
                    f"--{boundary}\r\n"
                    f'Content-Disposition: form-data; name="{key}"; filename="{filename}"\r\n'
                    f"Content-Type: {ctype}\r\n\r\n"
                ).encode()
            )
            chunks.append(content)
            chunks.append(b"\r\n")
        chunks.append(f"--{boundary}--\r\n".encode())
        data = b"".join(chunks)
        headers["Content-Type"] = f"multipart/form-data; boundary={boundary}"
    req = request.Request(url, data=data, headers=headers, method=method)
    try:
        with request.urlopen(req, timeout=60) as resp:
            return resp.status, json.loads(resp.read().decode())
    except error.HTTPError as exc:
        return exc.code, exc.read().decode("utf-8", errors="replace")


def upload_all(base: str, username: str, password: str) -> list[dict]:
    base = base.rstrip("/")
    status, login = _api(
        "POST",
        f"{base}/api/v1/auth/login",
        json_body={"username": username, "password": password, "chuong_trinh": "word"},
    )
    if status != 200 or not isinstance(login, dict) or not login.get("token"):
        raise RuntimeError(f"login_failed:{status}")
    token = login["token"]
    uploaded = []
    for pack in iter_packs():
        local = grade_pack(pack)
        status, started = _api(
            "POST",
            f"{base}/api/v1/attempts",
            token=token,
            json_body={"project_id": pack["project_id"], "mode": "training"},
        )
        if status != 200 or not isinstance(started, dict):
            local["upload_error"] = f"start:{status}"
            uploaded.append(local)
            continue
        attempt_id = started["attempt_id"]
        local["attempt_id"] = attempt_id
        filename = pack["results"].name
        ctype = mimetypes.guess_type(filename)[0] or WORD_MIME
        status, check = _api(
            "POST",
            f"{base}/api/v1/attempts/{attempt_id}/checkpoints",
            token=token,
            files={"file": (filename, pack["results"].read_bytes(), ctype)},
        )
        if status != 200 or not isinstance(check, dict):
            local["upload_error"] = f"checkpoint:{status}"
            uploaded.append(local)
            continue
        score = check.get("score") or {}
        local["live_checkpoint_verified"] = score.get("verified")
        local["live_checkpoint_pending"] = score.get("pending")
        if pack["evidence"]:
            status, posted = _api(
                "POST",
                f"{base}/api/v1/attempts/{attempt_id}/evidence",
                token=token,
                json_body={"events": pack["evidence"]},
            )
            if status != 200 or not isinstance(posted, dict):
                local["upload_error"] = f"evidence:{status}"
            else:
                live = posted.get("score") or {}
                local["live_verified"] = live.get("verified")
                local["live_pending"] = live.get("pending")
                local["live_complete"] = live.get("complete")
                local["regraded"] = posted.get("regraded")
        else:
            local["live_verified"] = score.get("verified")
            local["live_pending"] = score.get("pending")
            local["live_complete"] = score.get("complete")
        uploaded.append(local)
    return uploaded


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Demo và tự chấm mọi bài MOS Word.")
    parser.add_argument("--json", action="store_true", help="In JSON thay vì bảng.")
    parser.add_argument("--upload", metavar="URL", help="Gửi checkpoint + bằng chứng lên API (vd. http://127.0.0.1:8088).")
    parser.add_argument("--username", default="hocsinh")
    parser.add_argument("--password", default="Mos@Gds2026")
    args = parser.parse_args(argv)
    rows = upload_all(args.upload, args.username, args.password) if args.upload else run_all()
    if args.json:
        print(json.dumps(rows, ensure_ascii=False, indent=2, default=str))
    else:
        print(format_report(rows))
        if args.upload:
            live_ok = sum(
                1
                for row in rows
                if float(row.get("live_verified") or 0) == 100 and not row.get("upload_error")
            )
            print(f"Đã gửi server: {live_ok}/{len(rows)} bài 100 điểm.")
    failed = [row for row in rows if not row.get("complete") or float(row.get("demo_verified") or 0) != 100]
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
