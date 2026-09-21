"""Trần điểm chấm tự động của một rubric.

`action_sequence` chỉ chấm được khi WordActionProbe.cs sinh ra bằng chứng cho
loại thao tác đó. Mọi tiêu chí thao tác NGOÀI danh sách PRODUCIBLE_ACTIONS là
"điểm chết": học sinh làm đúng vẫn ở trạng thái unverified, nên trần điểm thật
của bài thấp hơn max_score. Học sinh phải được nói trước điều này (UDL: không
để các em nghĩ mình làm sai khi hệ thống không đo được).

    >>> ceiling(load_rubric("word-objective-1-3"))
    {'max': 100, 'auto': 55, 'manual': 45, 'manual_ids': ['W13-S01', 'W13-S02', 'W13-S03']}
"""
from __future__ import annotations

# Loại thao tác mà WordActionProbe.cs thật sự sinh ra được bằng chứng.
# scripts/rubric_tool.py và scripts/audit_word.py dùng chung tập này.
PRODUCIBLE_ACTIONS = frozenset(
    {
        "find",
        "search_query",
        "find_navigate",
        "search_navigate",
        "results_tab",
        "advanced_find",
    }
)


def action_type(criterion: dict) -> str:
    pred = criterion.get("predicate") or {}
    sel = criterion.get("selector") or {}
    return str(pred.get("type") or sel.get("action") or "").casefold()


def is_dead(criterion: dict) -> bool:
    """True nếu tiêu chí thao tác không có nguồn bằng chứng nào."""
    return criterion.get("kind") == "action_sequence" and action_type(criterion) not in PRODUCIBLE_ACTIONS


def ceiling(rubric: dict | None) -> dict:
    crits = [c for c in (rubric or {}).get("criteria") or [] if isinstance(c, dict)]
    total = sum(int(c.get("weight") or 0) for c in crits)
    declared = int((rubric or {}).get("max_score") or total or 100)
    dead = [c for c in crits if is_dead(c)]
    manual = sum(int(c.get("weight") or 0) for c in dead)
    return {
        "max": declared,
        "auto": max(0, declared - manual),
        "manual": manual,
        "manual_ids": [str(c.get("id") or "") for c in dead],
    }


def ceiling_label(info: dict, lang: str = "vi") -> str:
    """Một dòng cho thẻ nhiệm vụ. Rỗng nếu chấm tự động được trọn điểm."""
    if not info or not info.get("manual"):
        return ""
    if lang == "en":
        return (
            f"Auto-graded: {info['auto']}/{info['max']} points. "
            f"The remaining {info['manual']} are marked by your teacher."
        )
    return (
        f"Chấm tự động: {info['auto']}/{info['max']} điểm. "
        f"{info['manual']} điểm còn lại cô/thầy chấm tay."
    )
