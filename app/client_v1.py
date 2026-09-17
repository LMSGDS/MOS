"""Client API v1 — desktop MOS-KulKul (JWT, store-and-forward)."""
from __future__ import annotations

import json
import secrets
from datetime import datetime, timezone
from pathlib import Path

from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import FileResponse

from app.auth import authenticate
from app.db import cursor
from app.grade import GRADER_VERSION, sha256_file
from app.programs import normalize
from app.scoring import score_file
from app.security import bearer_user
from app.tokens import issue

ROOT = Path(__file__).resolve().parent.parent
RESULTS = ROOT / "data" / "results"
MAX_UPLOAD = 20 * 1024 * 1024
router = APIRouter(prefix="/api/v1")


def _user_row(username: str) -> dict | None:
    with cursor() as cur:
        cur.execute("SELECT * FROM users WHERE username = %s", (username,))
        return cur.fetchone()


def _as_dict(value) -> dict:
    if isinstance(value, dict):
        return value
    if not value:
        return {}
    return json.loads(value)


def _staff(row: dict | None) -> bool:
    return bool(row and row.get("role") in ("admin", "teacher", "leadership"))


def _require_user(user: dict) -> dict:
    row = _user_row(user["username"])
    if not row:
        raise HTTPException(status_code=401, detail="user")
    return row


def _load_attempt(attempt_id: str) -> dict | None:
    with cursor() as cur:
        cur.execute(
            """
            SELECT a.*, p.rubric, p.skill_domain, p.filename, p.file_path, p.rubric_version AS project_rubric_version
            FROM attempts a JOIN projects p ON p.id = a.project_id
            WHERE a.id = %s
            """,
            (attempt_id,),
        )
        return cur.fetchone()


def _assert_owner(attempt: dict | None, row_user: dict, *, write: bool = True) -> dict:
    if not attempt:
        raise HTTPException(status_code=404, detail="attempt")
    owner = attempt["user_id"] == row_user["id"]
    if owner:
        return attempt
    if write or not _staff(row_user):
        raise HTTPException(status_code=403, detail="forbidden")
    return attempt


def _locked_rubric(attempt: dict) -> dict:
    version_id = attempt.get("project_version_id")
    if version_id:
        with cursor() as cur:
            cur.execute("SELECT rubric FROM project_versions WHERE id = %s", (version_id,))
            row = cur.fetchone()
            if row:
                return _as_dict(row["rubric"])
    return _as_dict(attempt.get("rubric"))


def _public_criteria(rubric: dict) -> list[dict]:
    out = []
    for item in rubric.get("criteria") or []:
        out.append(
            {
                "id": item.get("id"),
                "kind": item.get("kind"),
                "weight": item.get("weight"),
                "prompt": item.get("prompt") or "",
            }
        )
    return out


def _grade_payload(scored: dict) -> dict:
    return {
        "score": scored.get("score"),
        "verified": scored.get("verified", scored.get("score")),
        "pending": scored.get("pending", 0),
        "max_score": scored.get("max_score") or 100,
        "complete": scored.get("complete"),
        "status": scored.get("status"),
        "checks": scored.get("checks") or [],
        "criteria": scored.get("criteria") or [],
        "grader_version": scored.get("grader_version"),
        "rubric_version": scored.get("rubric_version"),
        "error": scored.get("error"),
    }


async def _read_upload(request: Request) -> tuple[bytes | None, str]:
    ctype = (request.headers.get("content-type") or "").lower()
    if "multipart/form-data" not in ctype:
        return None, ""
    form = await request.form()
    upload = form.get("file")
    if upload is None or not hasattr(upload, "read"):
        return None, ""
    filename = Path(getattr(upload, "filename", None) or "bai").name
    if filename in ("", ".", "..") or "/" in filename or "\\" in filename:
        filename = "bai.bin"
    data = await upload.read()
    if len(data) > MAX_UPLOAD:
        raise HTTPException(status_code=413, detail="too_large")
    return data, filename


def _save_bytes(dest: Path, data: bytes) -> Path:
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(data)
    return dest


def _store_results(cur, submission_id: str, scored: dict) -> str:
    run_id = secrets.token_hex(12)
    cur.execute(
        """
        INSERT INTO grading_runs (id, submission_id, grader_version, rubric_version, status, finished_at)
        VALUES (%s, %s, %s, %s, %s, now())
        """,
        (
            run_id,
            submission_id,
            scored.get("grader_version") or GRADER_VERSION,
            scored.get("rubric_version") or "",
            scored.get("status") or "provisional",
        ),
    )
    for item in scored.get("criteria") or []:
        cur.execute(
            """
            INSERT INTO criterion_results (
              grading_run_id, criterion_id, status, earned, possible, reason_code, evidence_refs, message
            ) VALUES (%s, %s, %s, %s, %s, %s, %s::jsonb, %s)
            """,
            (
                run_id,
                item.get("criterion_id"),
                item.get("status"),
                item.get("earned") or 0,
                item.get("possible") or 0,
                item.get("reason_code"),
                json.dumps(item.get("evidence_refs") or [], ensure_ascii=False),
                item.get("message"),
            ),
        )
    return run_id


@router.post("/auth/login")
async def v1_login(request: Request):
    data = await request.json()
    username = str(data.get("username") or "")
    password = str(data.get("password") or "")
    program = normalize(str(data.get("chuong_trinh") or "word"))
    user = authenticate(username, password)
    if not user:
        raise HTTPException(status_code=401, detail="sai")
    row = _user_row(user["username"])
    if row:
        user = {
            "id": row["id"],
            "username": row["username"],
            "name": row["name"],
            "role": row["role"],
        }
    token = issue(user)
    request.session["user"] = {
        "username": user["username"],
        "name": user["name"],
        "role": user["role"],
    }
    request.session["chuong_trinh"] = program
    request.session["che_do"] = "dock"
    return {"ok": True, "token": token, "user": user, "program": program}


@router.get("/me")
def v1_me(request: Request):
    user = bearer_user(request)
    row = _user_row(user["username"])
    if row:
        user["id"] = row["id"]
        user["role"] = row["role"]
        user["name"] = row["name"]
    return {"ok": True, "user": user}


@router.get("/projects")
def v1_projects(request: Request, program: str | None = None):
    bearer_user(request)
    prog = normalize(program) if program else None
    with cursor() as cur:
        if prog:
            cur.execute(
                """
                SELECT id, title, program, skill_domain, filename, steps, time_limit_sec, rubric_version
                FROM projects WHERE program = %s ORDER BY title
                """,
                (prog,),
            )
        else:
            cur.execute(
                """
                SELECT id, title, program, skill_domain, filename, steps, time_limit_sec, rubric_version
                FROM projects ORDER BY program, title
                """
            )
        rows = cur.fetchall()
    return {"ok": True, "projects": rows}


def _project_row(project_id: str) -> dict | None:
    with cursor() as cur:
        cur.execute("SELECT * FROM projects WHERE id = %s", (project_id,))
        return cur.fetchone()


@router.get("/projects/{project_id}/manifest")
def v1_project_manifest(request: Request, project_id: str):
    bearer_user(request)
    row = _project_row(project_id)
    if not row:
        raise HTTPException(status_code=404, detail="project")
    rubric = _as_dict(row.get("rubric"))
    path = Path(row["file_path"])
    digest = sha256_file(path) if path.is_file() else None
    with cursor() as cur:
        cur.execute(
            """
            SELECT id, rubric_version, source_sha256, manifest
            FROM project_versions
            WHERE project_id = %s AND published = TRUE
            ORDER BY created_at DESC
            LIMIT 1
            """,
            (project_id,),
        )
        version = cur.fetchone()
    return {
        "ok": True,
        "project_id": project_id,
        "title": row["title"],
        "program": row["program"],
        "filename": row["filename"],
        "rubric_version": (version or {}).get("rubric_version") or row.get("rubric_version") or "legacy",
        "project_version_id": (version or {}).get("id"),
        "sha256": (version or {}).get("source_sha256") or digest,
        "capabilities": rubric.get("capabilities") or [],
        "criteria": _public_criteria(rubric),
        "time_limit_sec": row.get("time_limit_sec") or 1800,
    }


@router.get("/projects/{project_id}/rubric")
def v1_project_rubric(request: Request, project_id: str):
    bearer_user(request)
    row = _project_row(project_id)
    if not row:
        raise HTTPException(status_code=404, detail="project")
    return {"ok": True, "project_id": project_id, "rubric": _as_dict(row.get("rubric"))}


@router.get("/projects/{project_id}/file")
def v1_project_file(request: Request, project_id: str):
    bearer_user(request)
    row = _project_row(project_id)
    if not row:
        raise HTTPException(status_code=404, detail="project")
    path = Path(row["file_path"])
    if not path.is_file():
        raise HTTPException(status_code=404, detail="file")
    return FileResponse(path, filename=row["filename"])


@router.post("/attempts")
async def v1_start_attempt(request: Request):
    user = bearer_user(request)
    body = await request.json()
    project_id = str(body.get("project_id") or "")
    mode = str(body.get("mode") or "training")
    if mode not in ("training", "testing"):
        mode = "training"
    row = _require_user(user)
    with cursor() as cur:
        cur.execute("SELECT id FROM projects WHERE id = %s", (project_id,))
        if not cur.fetchone():
            raise HTTPException(status_code=404, detail="project")
        cur.execute(
            """
            SELECT id, rubric_version FROM project_versions
            WHERE project_id = %s AND published = TRUE
            ORDER BY created_at DESC LIMIT 1
            """,
            (project_id,),
        )
        version = cur.fetchone()
        cur.execute("SELECT class_id FROM enrollments WHERE user_id = %s LIMIT 1", (row["id"],))
        enr = cur.fetchone()
        attempt_id = secrets.token_hex(12)
        cur.execute(
            """
            INSERT INTO attempts (id, user_id, project_id, class_id, mode, status, project_version_id)
            VALUES (%s, %s, %s, %s, %s, 'running', %s)
            """,
            (attempt_id, row["id"], project_id, enr["class_id"] if enr else None, mode, version["id"] if version else None),
        )
    return {
        "ok": True,
        "attempt_id": attempt_id,
        "mode": mode,
        "project_id": project_id,
        "project_version_id": version["id"] if version else None,
        "rubric_version": version["rubric_version"] if version else None,
    }


@router.post("/attempts/{attempt_id}/telemetry")
async def v1_telemetry(request: Request, attempt_id: str):
    user = bearer_user(request)
    row_user = _require_user(user)
    attempt = _load_attempt(attempt_id)
    _assert_owner(attempt, row_user, write=True)
    body = await request.json()
    events = body.get("events") or []
    if not isinstance(events, list):
        raise HTTPException(status_code=400, detail="events")
    accepted = 0
    with cursor() as cur:
        for ev in events:
            if not isinstance(ev, dict):
                continue
            event_id = str(ev.get("event_id") or "") or None
            sequence = ev.get("sequence")
            seq_val = int(sequence) if isinstance(sequence, int) or (isinstance(sequence, str) and str(sequence).isdigit()) else None
            cur.execute(
                """
                INSERT INTO telemetry (attempt_id, skill, action, detail, event_id, sequence, document_id, source_version)
                VALUES (%s, %s, %s, %s::jsonb, %s, %s, %s, %s)
                ON CONFLICT (attempt_id, event_id) WHERE event_id IS NOT NULL AND event_id <> '' DO NOTHING
                """,
                (
                    attempt_id,
                    str(ev.get("skill") or ""),
                    str(ev.get("action") or "event"),
                    json.dumps(ev.get("detail") or {}, ensure_ascii=False),
                    event_id,
                    seq_val,
                    str(ev.get("document_id") or "") or None,
                    str(ev.get("source_version") or "") or None,
                ),
            )
            if cur.rowcount:
                accepted += 1
    return {"ok": True, "accepted": accepted}


@router.get("/attempts")
def v1_attempts(request: Request):
    user = bearer_user(request)
    row = _require_user(user)
    with cursor() as cur:
        if _staff(row):
            cur.execute(
                """
                SELECT a.id, a.project_id, a.mode, a.status, a.score, a.max_score,
                       a.verified_score, a.pending_score,
                       a.started_at, a.submitted_at, a.duration_sec, p.title
                FROM attempts a JOIN projects p ON p.id = a.project_id
                ORDER BY a.started_at DESC
                LIMIT 100
                """
            )
        else:
            cur.execute(
                """
                SELECT a.id, a.project_id, a.mode, a.status, a.score, a.max_score,
                       a.verified_score, a.pending_score,
                       a.started_at, a.submitted_at, a.duration_sec, p.title
                FROM attempts a JOIN projects p ON p.id = a.project_id
                WHERE a.user_id = %s
                ORDER BY a.started_at DESC
                LIMIT 50
                """,
                (row["id"],),
            )
        rows = cur.fetchall()
    return {"ok": True, "attempts": rows}


def _score_saved(path: Path | None, attempt: dict, evidence: list | None = None) -> dict:
    rubric = _locked_rubric(attempt)
    return score_file(path, rubric, evidence)


@router.post("/attempts/{attempt_id}/checkpoints")
async def v1_checkpoint(request: Request, attempt_id: str):
    user = bearer_user(request)
    row_user = _require_user(user)
    attempt = _load_attempt(attempt_id)
    _assert_owner(attempt, row_user, write=True)
    data, filename = await _read_upload(request)
    if not data:
        raise HTTPException(status_code=400, detail="file")
    dest = RESULTS / attempt_id / f"checkpoint-{secrets.token_hex(6)}-{filename}"
    saved = _save_bytes(dest, data)
    scored = _score_saved(saved, attempt)
    payload = _grade_payload(scored)
    training = attempt.get("mode") != "testing"
    if not training:
        return {
            "ok": True,
            "checkpoint": True,
            "mode": "testing",
            "saved": True,
            "message": "Đã lưu checkpoint. Điểm và gợi ý ẩn trong chế độ thi.",
        }
    return {"ok": True, "checkpoint": True, "mode": "training", "score": payload}


async def _submit_attempt(request: Request, attempt_id: str, *, idempotency_key: str | None = None) -> dict:
    user = bearer_user(request)
    row_user = _require_user(user)
    attempt = _load_attempt(attempt_id)
    _assert_owner(attempt, row_user, write=True)
    if idempotency_key:
        with cursor() as cur:
            cur.execute(
                "SELECT id, payload FROM submissions WHERE attempt_id = %s AND idempotency_key = %s",
                (attempt_id, idempotency_key),
            )
            existing = cur.fetchone()
        if existing:
            stored = _as_dict(existing["payload"])
            return {"ok": True, "submission_id": existing["id"], "score": stored, "replayed": True}
    data, filename = await _read_upload(request)
    saved = None
    digest = None
    submission_id = secrets.token_hex(12)
    if data:
        dest = RESULTS / attempt_id / f"{submission_id}-{filename}"
        saved = _save_bytes(dest, data)
        digest = sha256_file(saved)
    scored = _score_saved(saved, attempt)
    payload = _grade_payload(scored)
    started = attempt["started_at"]
    now = datetime.now(timezone.utc)
    duration = None
    if started:
        if started.tzinfo is None:
            started = started.replace(tzinfo=timezone.utc)
        duration = int((now - started).total_seconds())
    with cursor() as cur:
        cur.execute(
            """
            INSERT INTO submissions (
              id, attempt_id, idempotency_key, status, artifact_sha256, snapshot_path,
              grader_version, score, verified_score, pending_score, max_score, payload, sealed_at
            ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s::jsonb, now())
            """,
            (
                submission_id,
                attempt_id,
                idempotency_key,
                "graded" if payload.get("complete") else "provisional",
                digest,
                str(saved) if saved else None,
                payload.get("grader_version"),
                payload.get("score"),
                payload.get("verified"),
                payload.get("pending"),
                payload.get("max_score") or 100,
                json.dumps(payload, ensure_ascii=False),
            ),
        )
        _store_results(cur, submission_id, scored)
        cur.execute(
            """
            UPDATE attempts SET
              status = 'submitted',
              score = %s,
              verified_score = %s,
              pending_score = %s,
              max_score = %s,
              submitted_at = now(),
              duration_sec = %s,
              result_path = %s,
              payload = %s::jsonb,
              retries = retries + 1
            WHERE id = %s
            """,
            (
                payload.get("score"),
                payload.get("verified"),
                payload.get("pending"),
                payload.get("max_score") or 100,
                duration,
                str(saved) if saved else None,
                json.dumps(payload, ensure_ascii=False),
                attempt_id,
            ),
        )
        cur.execute(
            """
            INSERT INTO telemetry (attempt_id, skill, action, detail)
            VALUES (%s, %s, 'submit', %s::jsonb)
            """,
            (
                attempt_id,
                attempt.get("skill_domain") or "",
                json.dumps({"score": payload.get("score"), "pending": payload.get("pending"), "submission_id": submission_id}, ensure_ascii=False),
            ),
        )
    return {"ok": True, "submission_id": submission_id, "score": payload, "duration_sec": duration}


@router.post("/attempts/{attempt_id}/submit")
async def v1_submit(request: Request, attempt_id: str):
    key = request.headers.get("idempotency-key")
    return await _submit_attempt(request, attempt_id, idempotency_key=key)


@router.post("/attempts/{attempt_id}/submissions")
async def v1_create_submission(request: Request, attempt_id: str):
    key = request.headers.get("idempotency-key") or request.query_params.get("idempotency_key")
    return await _submit_attempt(request, attempt_id, idempotency_key=key)


@router.get("/submissions/{submission_id}")
def v1_get_submission(request: Request, submission_id: str):
    user = bearer_user(request)
    row_user = _require_user(user)
    with cursor() as cur:
        cur.execute(
            """
            SELECT s.*, a.user_id, a.mode, a.project_id
            FROM submissions s JOIN attempts a ON a.id = s.attempt_id
            WHERE s.id = %s
            """,
            (submission_id,),
        )
        row = cur.fetchone()
    if not row:
        raise HTTPException(status_code=404, detail="submission")
    if row["user_id"] != row_user["id"] and not _staff(row_user):
        raise HTTPException(status_code=403, detail="forbidden")
    return {"ok": True, "submission": row}
