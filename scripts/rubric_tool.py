#!/usr/bin/env python3
"""Công cụ rubric MOS-KulKul: chuyển sang khung song ngữ + kiểm tra chất lượng.

    python3 scripts/rubric_tool.py migrate app/rubrics      # thêm khung {vi,en}, kc[]
    python3 scripts/rubric_tool.py check   app/rubrics      # báo cáo, thoát 0
    python3 scripts/rubric_tool.py check   app/rubrics --strict   # CI: lỗi thì thoát 1
    python3 scripts/rubric_tool.py baseline app/rubrics           # chốt lỗi đang tồn đọng
    python3 scripts/rubric_tool.py check   app/rubrics --strict --baseline rubric-baseline.json

`migrate` không ghi đè bản dịch đã có và chạy lại nhiều lần vẫn an toàn.
`check` chạy bộ luật ở mục "Ngưỡng chất lượng rubric" trong đặc tả.

`--baseline` là bánh cóc: CI chỉ gãy khi có lỗi MỚI, còn lỗi tồn đọng đã chốt thì
bỏ qua. Nhờ vậy gắn `--strict` vào CI được NGAY hôm nay mà không chặn mọi PR,
trong khi 52 lỗi cũ được trả dần. Sửa xong một lỗi thì chạy lại `baseline` để
chốt mức mới — đã trả rồi thì không mắc lại được.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from app import i18n  # noqa: E402

TEXT = i18n.CRITERION_TEXT_FIELDS          # ("prompt",)
LISTS = i18n.CRITERION_LIST_FIELDS         # ("help_steps",)
MAPS = i18n.CRITERION_MAP_FIELDS           # ("feedback",)

# Predicate quá yếu để gánh điểm lớn: chỉ nhìn chữ, không nhìn cấu trúc.
WEAK_PREDICATES = {"contains_text", "not_contains_text"}
WEAK_WEIGHT_LIMIT = 20                     # điểm tối đa một predicate yếu được gánh

# Loại thao tác mà WordActionProbe.cs thật sự sinh ra được bằng chứng.
# Mọi predicate action_sequence NGOÀI danh sách này là "điểm chết":
# không có nguồn bằng chứng nào, nên vĩnh viễn ở trạng thái unverified.
PRODUCIBLE_ACTIONS = {
    "find", "search_query",
    "find_navigate", "search_navigate",
    "results_tab",
    "advanced_find",
}

# Predicate so sánh ngưỡng: hai tiêu chí cùng type + cùng text, khác min,
# thì cái ngưỡng cao đỗ kéo theo cái ngưỡng thấp đỗ — không đo thêm gì.
THRESHOLD_KEYS = ("min", "max", "rows", "cols")


# ---------------------------------------------------------------- migrate

def _wrap(value):
    """Chuỗi thuần -> {'vi': chuỗi, 'en': ''}. Khối đã song ngữ thì giữ nguyên."""
    if isinstance(value, str):
        return {"vi": value, "en": ""}
    if i18n.is_bundle(value):
        out = dict(value)
        out.setdefault("vi", "")
        out.setdefault("en", "")
        return out
    return value


def migrate_rubric(rubric: dict) -> dict:
    out = dict(rubric)
    out["langs"] = ["vi", "en"]
    out["default_lang"] = "vi"
    for field in i18n.RUBRIC_TEXT_FIELDS:
        if field in out:
            out[field] = _wrap(out[field])

    criteria = []
    for criterion in out.get("criteria") or []:
        if not isinstance(criterion, dict):
            criteria.append(criterion)
            continue
        c = dict(criterion)
        c.setdefault("kc", [])                      # chỗ cho Q-matrix nội dung
        for field in TEXT:
            if field in c:
                c[field] = _wrap(c[field])
        for field in LISTS:
            steps = c.get(field)
            if isinstance(steps, list):
                c[field] = [_wrap(s) for s in steps]
        for field in MAPS:
            block = c.get(field)
            if isinstance(block, dict) and not i18n.is_bundle(block):
                c[field] = {k: _wrap(v) for k, v in block.items()}
        criteria.append(c)
    out["criteria"] = criteria
    return out


# ------------------------------------------------------------------ check

def _canon(obj) -> str:
    return json.dumps(obj, sort_keys=True, ensure_ascii=False)


def check_rubric(rubric: dict, name: str) -> tuple[list[str], list[str]]:
    """Trả về (lỗi, cảnh báo)."""
    errors: list[str] = []
    warns: list[str] = []
    criteria = [c for c in (rubric.get("criteria") or []) if isinstance(c, dict)]

    if not criteria:
        errors.append("không có criteria — sẽ rơi xuống grader legacy-text")
        return errors, warns

    # 1. Tổng trọng số phải bằng max_score
    total = sum(int(c.get("weight") or 0) for c in criteria)
    declared = int(rubric.get("max_score") or 100)
    if total != declared:
        errors.append(f"tổng weight = {total}, nhưng max_score = {declared}")

    # 2. Không hai criterion nào trùng predicate
    seen: dict[str, str] = {}
    for c in criteria:
        key = _canon(c.get("predicate") or {})
        cid = str(c.get("id") or "?")
        if key in seen:
            errors.append(f"{cid} trùng predicate với {seen[key]} — không phân biệt được hai kỹ năng")
        else:
            seen[key] = cid

    # 3. Predicate yếu không được gánh điểm lớn.
    #    Có thể miễn trừ bằng "weak_ok": "<lý do>" khi kết quả CHÍNH LÀ chuỗi
    #    cần đo (ví dụ chèn ký hiệu ® — gõ ra đúng ký tự là đạt mục tiêu).
    for c in criteria:
        ptype = str((c.get("predicate") or {}).get("type") or "")
        weight = int(c.get("weight") or 0)
        if ptype in WEAK_PREDICATES and weight > WEAK_WEIGHT_LIMIT:
            if str(c.get("weak_ok") or "").strip():
                warns.append(f"{c.get('id')} miễn trừ predicate yếu: «{c['weak_ok']}»")
            else:
                errors.append(
                    f"{c.get('id')} dùng {ptype} cho {weight} điểm — chỉ dò chuỗi, "
                    f"học sinh gõ tay cũng đỗ (ngưỡng {WEAK_WEIGHT_LIMIT}). "
                    f"Nếu cố ý, thêm \"weak_ok\": \"<lý do>\""
                )

    # 7. action_sequence không có nguồn bằng chứng => điểm chết
    dead = 0
    for c in criteria:
        if c.get("kind") != "action_sequence":
            continue
        pred = c.get("predicate") or {}
        sel = c.get("selector") or {}
        ptype = str(pred.get("type") or sel.get("action") or "").casefold()
        if ptype and ptype not in PRODUCIBLE_ACTIONS:
            weight = int(c.get("weight") or 0)
            dead += weight
            errors.append(
                f"{c.get('id')} ({weight} điểm) dùng thao tác «{ptype}» — "
                f"WordActionProbe không sinh bằng chứng cho loại này, nên tiêu chí "
                f"vĩnh viễn unverified. Chuyển sang dạng artifact hoặc bổ sung bộ ghi nhận"
            )
    if dead:
        errors.append(f"tổng {dead}/{declared} điểm không có nguồn bằng chứng nào")

    # 8. Tiêu chí bị bao trùm bởi tiêu chí khác (chỉ khác ngưỡng)
    for i, a in enumerate(criteria):
        for b in criteria[i + 1:]:
            pa, pb = a.get("predicate") or {}, b.get("predicate") or {}
            if pa.get("type") != pb.get("type") or not pa.get("type"):
                continue
            base_a = {k: v for k, v in pa.items() if k not in THRESHOLD_KEYS}
            base_b = {k: v for k, v in pb.items() if k not in THRESHOLD_KEYS}
            if base_a != base_b or _canon(pa) == _canon(pb):
                continue
            warns.append(
                f"{a.get('id')} và {b.get('id')} chỉ khác ngưỡng "
                f"({pa.get('type')}) — cái chặt hơn đỗ thì cái lỏng hơn luôn đỗ"
            )

    # 4. Mỗi criterion phải có feedback.fail riêng
    fails: dict[str, list[str]] = {}
    for c in criteria:
        fail = i18n.pick((c.get("feedback") or {}).get("fail"), "vi").strip()
        if not fail:
            errors.append(f"{c.get('id')} thiếu feedback.fail")
            continue
        fails.setdefault(fail, []).append(str(c.get("id")))
    for text, ids in fails.items():
        if len(ids) > 1:
            warns.append(f"feedback.fail dùng chung cho {', '.join(ids)}: «{text[:60]}»")

    # 5. action_sequence phải khai báo evidence_policy
    for c in criteria:
        if c.get("kind") == "action_sequence" and not c.get("evidence_policy"):
            warns.append(f"{c.get('id')} là action_sequence nhưng thiếu evidence_policy")

    # 6. Gắn KC cho Bản đồ Năng lực
    no_kc = [str(c.get("id")) for c in criteria if not c.get("kc")]
    if no_kc:
        warns.append(f"chưa gắn kc[]: {', '.join(no_kc)}")

    return errors, warns


# ------------------------------------------------------------------- main

def load(path: Path) -> dict | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"  ✗ {path.name}: không đọc được — {exc}")
        return None


BASELINE_FILE = "rubric-baseline.json"


def load_baseline(path: Path | None) -> dict[str, list[str]]:
    if path is None or not path.is_file():
        return {}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}
    known = data.get("known_errors") if isinstance(data, dict) else None
    if not isinstance(known, dict):
        return {}
    return {str(k): [str(x) for x in (v or [])] for k, v in known.items()}


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("command", choices=["migrate", "check", "baseline"])
    ap.add_argument("folder")
    ap.add_argument("--strict", action="store_true", help="có lỗi thì thoát 1 (dùng trong CI)")
    ap.add_argument(
        "--baseline",
        metavar="FILE",
        help=f"bỏ qua lỗi đã chốt trong FILE (mặc định {BASELINE_FILE} khi chạy lệnh baseline)",
    )
    args = ap.parse_args()

    folder = Path(args.folder)
    files = sorted(folder.glob("*.json"))
    if not files:
        print(f"Không thấy rubric nào trong {folder}")
        return 1

    if args.command == "migrate":
        for path in files:
            rubric = load(path)
            if rubric is None:
                continue
            path.write_text(
                json.dumps(migrate_rubric(rubric), ensure_ascii=False, indent=2) + "\n",
                encoding="utf-8",
            )
            print(f"  ✓ {path.name}")
        print(f"\nĐã chuyển {len(files)} rubric sang khung song ngữ.")
        return 0

    found: dict[str, list[str]] = {}
    rows: list[tuple] = []
    unreadable = 0
    for path in files:
        rubric = load(path)
        if rubric is None:
            unreadable += 1
            continue
        errors, warns = check_rubric(rubric, path.name)
        found[path.name] = errors
        rows.append((path.name, i18n.coverage(rubric, "en"), errors, warns))

    if args.command == "baseline":
        dest = Path(args.baseline or BASELINE_FILE)
        payload = {
            "_comment": (
                "Lỗi rubric đang tồn đọng, đã chốt để CI chỉ gãy khi có lỗi MỚI. "
                "Sửa xong một lỗi thì chạy lại: python3 scripts/rubric_tool.py baseline app/rubrics"
            ),
            "known_errors": {k: v for k, v in sorted(found.items()) if v},
        }
        dest.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        total = sum(len(v) for v in found.values())
        print(f"Đã chốt {total} lỗi tồn đọng của {sum(1 for v in found.values() if v)} rubric vào {dest}")
        return 0

    baseline = load_baseline(Path(args.baseline) if args.baseline else None)
    new_err = 0
    fixed = 0
    for name, _cov, errors, warns in rows:
        known = baseline.get(name, [])
        fresh = [e for e in errors if e not in known]
        fixed += len([e for e in known if e not in errors])
        new_err += len(fresh)
        if errors or warns:
            print(f"\n{name}")
            for e in errors:
                mark = "✗" if e in fresh else "·"   # · = đã chốt trong baseline
                print(f"  {mark} {e}")
            for w in warns:
                print(f"  ! {w}")

    print(f"\n{'rubric':<34}{'EN %':>7}{'lỗi':>6}{'mới':>6}{'cảnh báo':>10}")
    print("-" * 63)
    for name, cov, errors, warns in rows:
        fresh = len([e for e in errors if e not in baseline.get(name, [])])
        print(f"{name:<34}{cov:>7}{len(errors):>6}{fresh:>6}{len(warns):>10}")
    print("-" * 63)
    total_err = sum(len(e) for _, _, e, _ in rows)
    print(f"{len(rows)} rubric, {total_err} lỗi" + (f", {new_err} lỗi MỚI" if baseline else ""))
    if baseline and fixed:
        print(f"{fixed} lỗi trong baseline đã được sửa — chạy lại `baseline` để chốt mức mới")

    blocking = new_err if baseline else total_err
    return 1 if (args.strict and (blocking or unreadable)) else 0


if __name__ == "__main__":
    raise SystemExit(main())
