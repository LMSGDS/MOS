#!/usr/bin/env python3
"""Kiểm kê rubric Word: trọng số, điểm thao tác, điểm chết, predicate yếu.

Đọc THẲNG app/rubrics/word-objective-*.json nên bảng luôn khớp repo — chạy lại
sau mỗi lần sửa rubric là có số mới, không phải cập nhật bảng bằng tay.

    python3 scripts/audit_word.py                 # 20 rubric Word
    python3 scripts/audit_word.py --program excel # đổi chương trình
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from app import i18n  # noqa: E402
from scripts.rubric_tool import (  # noqa: E402
    PRODUCIBLE_ACTIONS,
    WEAK_PREDICATES,
    WEAK_WEIGHT_LIMIT,
)

RUBRIC_DIR = Path(__file__).resolve().parent.parent / "app" / "rubrics"


def _ptype(crit: dict) -> str:
    pred = crit.get("predicate") or {}
    sel = crit.get("selector") or {}
    return str(pred.get("type") or sel.get("action") or "").casefold()


def audit(path: Path) -> dict:
    rubric = json.loads(path.read_text(encoding="utf-8"))
    crits = [c for c in rubric.get("criteria") or [] if isinstance(c, dict)]
    total = sum(int(c.get("weight") or 0) for c in crits)
    action = sum(int(c.get("weight") or 0) for c in crits if c.get("kind") == "action_sequence")
    dead = sum(
        int(c.get("weight") or 0)
        for c in crits
        if c.get("kind") == "action_sequence" and _ptype(c) not in PRODUCIBLE_ACTIONS
    )
    weak = sum(
        int(c.get("weight") or 0)
        for c in crits
        if _ptype(c) in WEAK_PREDICATES and int(c.get("weight") or 0) > WEAK_WEIGHT_LIMIT
    )
    dead_kinds = sorted(
        {
            _ptype(c)
            for c in crits
            if c.get("kind") == "action_sequence" and _ptype(c) not in PRODUCIBLE_ACTIONS
        }
    )
    return {
        "file": path.name,
        "label": f"{rubric.get('objective') or path.stem} {i18n.pick(rubric.get('title'), 'en')}",
        "n": len(crits),
        "total": total,
        "declared": int(rubric.get("max_score") or 100),
        "action": action,
        "dead": dead,
        "weak": weak,
        "dead_kinds": dead_kinds,
        "en": i18n.coverage(rubric, "en"),
    }


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--program", default="word")
    args = ap.parse_args()

    rows = [audit(p) for p in sorted(RUBRIC_DIR.glob(f"{args.program}-objective-*.json"))]
    if not rows:
        print(f"Không thấy rubric {args.program} nào trong {RUBRIC_DIR}")
        return 1

    label_w = max(len(r["label"]) for r in rows) + 2
    print(f"{'rubric':<{label_w}}{'TC':>4}{'Σđ':>5}{'thao tác':>10}{'điểm chết':>11}{'yếu':>6}{'EN %':>7}")
    print("-" * (label_w + 43))
    for r in rows:
        flag = "" if r["total"] == r["declared"] else f"  ← lệch max_score {r['declared']}"
        print(
            f"{r['label']:<{label_w}}{r['n']:>4}{r['total']:>5}{r['action']:>10}"
            f"{r['dead']:>11}{r['weak']:>6}{r['en']:>7}{flag}"
        )
    print("-" * (label_w + 43))
    agg = {k: sum(r[k] for r in rows) for k in ("n", "total", "action", "dead", "weak")}
    print(
        f"{f'TỔNG ({len(rows)} rubric)':<{label_w}}{agg['n']:>4}{agg['total']:>5}"
        f"{agg['action']:>10}{agg['dead']:>11}{agg['weak']:>6}"
    )
    print()
    live = agg["action"] - agg["dead"]
    print(f"Điểm action_sequence           : {agg['action']}/{agg['total']}")
    print(f"  — có nguồn bằng chứng        : {live}")
    print(f"  — KHÔNG có nguồn (điểm chết) : {agg['dead']}")
    print(f"Điểm gánh bởi predicate yếu    : {agg['weak']}")
    print(f"Đã dịch EN                     : {sum(1 for r in rows if r['en'] == 100.0)}/{len(rows)} rubric")

    dead_rows = [r for r in rows if r["dead"]]
    if dead_rows:
        print("\nRubric có điểm chết (trần điểm thật mà học sinh có thể đạt):")
        for r in dead_rows:
            kinds = ", ".join(r["dead_kinds"])
            print(f"  {r['label']:<{label_w}} trần {r['total'] - r['dead']:>3}/{r['declared']}  ({kinds})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
