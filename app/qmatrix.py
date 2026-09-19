"""Q-matrix engine: locate → tool → configure, first break wins."""
from __future__ import annotations

import re

NAV = "Navigation"
TOOL = "Tool_Usage"
CFG = "Configuration"

_NAV_ACTIONS = {
    "find",
    "find_navigate",
    "results_tab",
    "goto_page",
    "goto_bookmark",
    "goto_graphic",
    "goto",
    "search_query",
    "search_navigate",
}


def skill_label(skill: str) -> str:
    key = (skill or "").replace(" ", "_")
    if key in {NAV, "locate"}:
        return "Định vị"
    if key in {TOOL, "ToolUsage"}:
        return "Công cụ"
    if key in {CFG, "Execution", "configure"}:
        return "Tham số"
    return skill or "Kỹ năng"


def nodes_for(criterion: dict) -> list[dict]:
    explicit = [n for n in (criterion.get("q_matrix_nodes") or []) if isinstance(n, dict)]
    if explicit:
        return explicit
    steps = [str(s).strip() for s in (criterion.get("help_steps") or []) if str(s).strip()]
    pred = ((criterion.get("predicate") or {}).get("type") or "").strip()
    return [
        {
            "step_id": "q1",
            "skill_type": NAV,
            "validation_rule": "check_cursor_position",
            "success_message": "Đã định vị đúng vị trí.",
            "error_feedback": _nav_fail(steps, criterion),
        },
        {
            "step_id": "q2",
            "skill_type": TOOL,
            "validation_rule": f"check_tool({pred or 'ribbon'})",
            "success_message": "Đã gọi đúng công cụ.",
            "error_feedback": _tool_fail(steps, criterion),
        },
        {
            "step_id": "q3",
            "skill_type": CFG,
            "validation_rule": f"check_config({pred or 'end_state'})",
            "success_message": "Đã cấu hình đúng tham số.",
            "error_feedback": _cfg_fail(steps, criterion),
        },
    ]


def evaluate(criterion: dict, facts: dict, end_status: str, events: list | None = None) -> list[dict]:
    events = events or []
    nodes = nodes_for(criterion)
    out: list[dict] = []
    broken = False
    for node in nodes:
        if end_status == "error":
            status = "skip"
            detail = node.get("error_feedback") or "Không đọc được tệp để chấm bước này."
        elif end_status == "unverified":
            status = "unverified"
            detail = node.get("error_feedback") or "Chưa thu được bằng chứng thao tác."
        elif end_status == "pass":
            status = "pass"
            detail = node.get("success_message") or "Đạt."
        elif broken:
            status = "skip"
            detail = "Chưa tới bước này."
        else:
            ok = _rule_ok(node, criterion, facts, events)
            if ok:
                status = "pass"
                detail = node.get("success_message") or "Đạt."
            else:
                status = "fail"
                detail = node.get("error_feedback") or _cfg_fail([], criterion)
                broken = True
        out.append(
            {
                "step_id": node.get("step_id") or f"q{len(out) + 1}",
                "skill_type": node.get("skill_type") or CFG,
                "status": status,
                "success_message": node.get("success_message") or "",
                "error_feedback": node.get("error_feedback") or "",
                "detail": detail,
                "label": skill_label(str(node.get("skill_type") or "")),
            }
        )
    return out


def attach(criterion: dict, facts: dict, result: dict, events: list | None = None) -> dict:
    trace = evaluate(criterion, facts, result.get("status") or "", events)
    result = dict(result)
    result["q_matrix"] = trace
    first = next((n for n in trace if n["status"] == "fail"), None)
    if result.get("status") == "fail" and first:
        result["message"] = first.get("detail") or first.get("error_feedback") or result.get("message")
        result["break_skill"] = first.get("skill_type")
        result["break_step"] = first.get("step_id")
    return result


def _nav_fail(steps: list[str], criterion: dict) -> str:
    if steps:
        return "Bạn chưa chọn hoặc click đúng vị trí trước khi thao tác. " + _plain(steps[0])
    return "Bạn chưa đặt con trỏ chuột đúng vị trí cho: " + str(criterion.get("prompt") or "nhiệm vụ này") + "."


def _tool_fail(steps: list[str], criterion: dict) -> str:
    if len(steps) >= 2:
        return "Bạn đang sử dụng sai công cụ. Thao tác chuẩn: " + _plain(steps[1])
    return "Sai công cụ. Hãy dùng đúng lệnh trên Ribbon cho: " + str(criterion.get("prompt") or "nhiệm vụ này") + "."


def _cfg_fail(steps: list[str], criterion: dict) -> str:
    fb = ((criterion.get("feedback") or {}).get("fail") or "").strip()
    extra = _plain(steps[2]) if len(steps) >= 3 else fb
    if extra:
        return "Bạn đã mở đúng công cụ nhưng chọn sai tham số hoặc chưa đủ yêu cầu. " + extra
    return "Bạn đã mở đúng công cụ nhưng chọn sai tham số."


def _plain(text: str) -> str:
    return re.sub(r"\*\*", "", text or "").strip()


def _rule_ok(node: dict, criterion: dict, facts: dict, events: list) -> bool:
    rule = str(node.get("validation_rule") or "").casefold()
    skill = str(node.get("skill_type") or "")
    if "cursor" in rule or "position" in rule or skill == NAV:
        return _has_anchor(facts, criterion, events)
    if "count_footnotes" in rule:
        minimum = int((criterion.get("predicate") or {}).get("min") or 1)
        return int(facts.get("footnote_count") or 0) >= minimum
    if "ribbon" in rule or "tool" in rule or skill == TOOL:
        return _has_tool(facts, criterion, events)
    return False


def _has_anchor(facts: dict, criterion: dict, events: list) -> bool:
    if any(str(ev.get("action") or "").casefold() in _NAV_ACTIONS for ev in events):
        return True
    pred = ((criterion.get("predicate") or {}).get("type") or "").casefold()
    text = facts.get("document_text") or ""
    if pred in {"header_contains", "header_instruction", "first_page_header", "watermark_text"}:
        return bool(
            facts.get("header_texts")
            or facts.get("header_instructions")
            or facts.get("watermarks")
            or text.strip()
        )
    if pred.startswith("toc") or (
        pred == "paragraph_style" and str((criterion.get("predicate") or {}).get("style") or "").upper().startswith("TOC")
    ):
        fields = facts.get("fields") or []
        styles = facts.get("style_counts") or {}
        return any("TOC" in (f or "").upper() for f in fields) or any(
            str(k).upper().startswith("TOC") for k in styles
        )
    return bool(str(text).strip() or facts.get("paragraphs") or facts.get("ok"))


def _has_tool(facts: dict, criterion: dict, events: list) -> bool:
    pred = ((criterion.get("predicate") or {}).get("type") or "").casefold()
    if (criterion.get("kind") or "") == "action_sequence":
        return bool(events)
    if pred == "footnote_min":
        return int(facts.get("footnote_count") or 0) >= 1
    if pred == "field_contains":
        return bool(facts.get("fields"))
    if pred == "paragraph_style":
        style = str((criterion.get("predicate") or {}).get("style") or "")
        counts = facts.get("style_counts") or {}
        paras = facts.get("paragraphs") or []
        if style.upper().startswith("TOC"):
            return any(str(k).upper().startswith("TOC") for k in counts) or any(
                "TOC" in (f or "").upper() for f in facts.get("fields") or []
            )
        return any(str(k).upper().startswith("HEADING") or str(k) == style for k in counts) or any(
            str(p.get("style") or "").upper().startswith("HEADING") for p in paras
        )
    if pred == "contains_text":
        needle = str((criterion.get("predicate") or {}).get("text") or "")
        blob = (facts.get("document_text") or "").casefold()
        words = [w for w in re.findall(r"\w+", needle) if len(w) > 3]
        return any(w.casefold() in blob for w in words) if words else bool(blob.strip())
    if pred == "not_contains_text":
        return True
    if pred.startswith("table"):
        return bool(facts.get("tables"))
    if pred == "page_background":
        return bool(facts.get("page_background"))
    if pred == "watermark_text":
        return bool(facts.get("watermarks"))
    if pred == "page_border":
        return bool(facts.get("page_border"))
    if pred.startswith("header"):
        return bool(facts.get("header_texts") or facts.get("header_instructions"))
    if pred == "bookmark_range":
        return bool(facts.get("bookmarks"))
    if pred == "internal_hyperlink":
        return bool(facts.get("internal_hyperlinks") or facts.get("fields"))
    if pred.startswith("comment"):
        return bool(facts.get("comments") or int(facts.get("comment_count") or 0))
    if pred in {"style_used", "style_size"}:
        return bool(facts.get("style_counts"))
    if pred.startswith("drawing") or pred in {"artistic_effect", "picture_effect", "alt_text", "wrap_type", "hdphoto"}:
        return bool(facts.get("drawing_kinds") or facts.get("drawing_texts") or facts.get("alt_texts"))
    if pred in {"section_columns", "page_orientation", "break_present", "section_count"}:
        return bool(facts.get("sections") or facts.get("breaks"))
    if pred == "list_format":
        return bool(facts.get("numbering_formats")) or any(
            p.get("num_fmt") for p in facts.get("paragraphs") or []
        )
    return False
