"""Rubric evaluator — artifact facts + optional action evidence."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

from app.word_xml import extract_word_facts, norm, same

GRADER_VERSION = "1.3.0"
RUBRIC_DIR = Path(__file__).resolve().parent / "rubrics"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_rubric(source: str | Path | dict | None) -> dict:
    if isinstance(source, dict):
        return source
    if source is None:
        return {}
    path = Path(source)
    if not path.is_file() and not path.suffix:
        alt = RUBRIC_DIR / f"{path.name}.json"
        if alt.is_file():
            path = alt
    if not path.is_file():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def _feedback(criterion: dict, status: str) -> str:
    block = criterion.get("feedback") or {}
    return str(block.get(status) or criterion.get("prompt") or "")


def _result(criterion: dict, status: str, reason: str, refs: list[str] | None = None) -> dict:
    possible = float(criterion.get("weight") or 0)
    earned = possible if status == "pass" else 0.0
    pending = possible if status == "unverified" else 0.0
    return {
        "criterion_id": criterion["id"],
        "status": status,
        "earned": earned,
        "possible": possible,
        "pending": pending,
        "reason_code": reason,
        "evidence_refs": refs or [],
        "message": _feedback(criterion, status),
        "kind": criterion.get("kind") or "artifact",
        "prompt": criterion.get("prompt") or "",
    }


def _bookmark_range(facts: dict, criterion: dict) -> dict:
    pred = criterion.get("predicate") or {}
    name = pred.get("name") or (criterion.get("selector") or {}).get("bookmark")
    expected = pred.get("text") or ""
    bookmarks = facts.get("bookmarks") or {}
    found = bookmarks.get(name) if name else None
    if not found:
        return _result(criterion, "fail", "bookmark_missing")
    text = found.get("text") or ""
    refs = [f"word/document.xml:{name}"]
    if same(text, expected):
        return _result(criterion, "pass", "bookmark_range_matches", refs)
    return _result(criterion, "fail", "bookmark_range_mismatch", refs)


def _internal_hyperlink(facts: dict, criterion: dict) -> dict:
    pred = criterion.get("predicate") or {}
    label = pred.get("text") or (criterion.get("selector") or {}).get("toc_label") or ""
    heading = pred.get("heading") or label
    internals = facts.get("internal_hyperlinks") or []
    externals = facts.get("external_hyperlinks") or []
    matches = [h for h in internals if same(h.get("text"), label)]
    if not matches:
        if any(same(h.get("text"), label) for h in externals):
            return _result(criterion, "fail", "hyperlink_external_only")
        return _result(criterion, "fail", "hyperlink_missing")
    good = [h for h in matches if same(h.get("target_heading"), heading)]
    if not good:
        refs = [f"word/document.xml:{m.get('anchor')}" for m in matches]
        return _result(criterion, "fail", "hyperlink_wrong_target", refs)
    anchors = {h.get("anchor") for h in good if h.get("anchor")}
    refs = [f"word/document.xml:{a}" for a in sorted(anchors)]
    return _result(criterion, "pass", "internal_hyperlink_matches", refs)


def _hex(value: str | None) -> str:
    return re.sub(r"[^0-9A-F]", "", (value or "").upper())


def _page_background(facts: dict, criterion: dict) -> dict:
    expected = _hex((criterion.get("predicate") or {}).get("color") or (criterion.get("selector") or {}).get("color"))
    got = _hex(facts.get("page_background") or "")
    if expected and got == expected:
        return _result(criterion, "pass", "page_background_matches")
    return _result(criterion, "fail", "page_background_mismatch")


def _watermark_text(facts: dict, criterion: dict) -> dict:
    expected = norm((criterion.get("predicate") or {}).get("text") or "")
    found = any(same(w, expected) or expected.casefold() in norm(w).casefold() for w in facts.get("watermarks") or [])
    if expected and found:
        return _result(criterion, "pass", "watermark_matches")
    return _result(criterion, "fail", "watermark_missing")


def _page_border(facts: dict, criterion: dict) -> dict:
    if facts.get("page_border"):
        return _result(criterion, "pass", "page_border_present")
    return _result(criterion, "fail", "page_border_missing")


def _header_contains(facts: dict, criterion: dict) -> dict:
    needle = norm((criterion.get("predicate") or {}).get("text") or "")
    blob = " ".join(facts.get("header_texts") or [])
    if needle and needle.casefold() in blob.casefold():
        return _result(criterion, "pass", "header_text_matches")
    return _result(criterion, "fail", "header_text_missing")


def _header_instruction(facts: dict, criterion: dict) -> dict:
    needle = ((criterion.get("predicate") or {}).get("text") or "PAGE").upper()
    found = any(needle in (instr or "").upper() for instr in facts.get("header_instructions") or [])
    if found:
        return _result(criterion, "pass", "header_field_matches")
    return _result(criterion, "fail", "header_field_missing")


def _first_page_header(facts: dict, criterion: dict) -> dict:
    if facts.get("first_page_header") and (facts.get("header_texts") or facts.get("watermarks") or facts.get("header_instructions")):
        return _result(criterion, "pass", "first_page_header_present")
    return _result(criterion, "fail", "first_page_header_missing")


def _style_size(facts: dict, criterion: dict) -> dict:
    expected = str((criterion.get("predicate") or {}).get("sz") or "")
    got = str(facts.get("heading1_sz") or "")
    if expected and got == expected:
        return _result(criterion, "pass", "style_size_matches")
    return _result(criterion, "fail", "style_size_mismatch")


def _core_property(facts: dict, criterion: dict) -> dict:
    pred = criterion.get("predicate") or {}
    name = pred.get("name") or ""
    expected = pred.get("text") or ""
    got = (facts.get("core_properties") or {}).get(name) or ""
    if name and same(got, expected):
        return _result(criterion, "pass", "core_property_matches")
    return _result(criterion, "fail", "core_property_mismatch")


def _comments_absent(facts: dict, criterion: dict) -> dict:
    if int(facts.get("comment_count") or 0) == 0:
        return _result(criterion, "pass", "comments_removed")
    return _result(criterion, "fail", "comments_remain")


def _revisions_cleared(facts: dict, criterion: dict) -> dict:
    if int(facts.get("revision_count") or 0) == 0 and not facts.get("track_revisions"):
        return _result(criterion, "pass", "revisions_cleared")
    return _result(criterion, "fail", "revisions_remain")


def _hidden_text_absent(facts: dict, criterion: dict) -> dict:
    if int(facts.get("vanish_count") or 0) == 0:
        return _result(criterion, "pass", "hidden_text_cleared")
    return _result(criterion, "fail", "hidden_text_remains")


def _phrase_in(blob: str, needle: str) -> bool:
    needle = norm(needle)
    blob = blob or ""
    if not needle:
        return False
    return re.search(r"(?<!\w)" + re.escape(needle) + r"(?!\w)", blob, flags=re.IGNORECASE) is not None


def _contains_text(facts: dict, criterion: dict) -> dict:
    pred = criterion.get("predicate") or {}
    needle = norm(pred.get("text") or "")
    blob = facts.get("document_text") or ""
    if needle and _phrase_in(blob, needle):
        return _result(criterion, "pass", "text_present")
    return _result(criterion, "fail", "text_missing")


def _not_contains_text(facts: dict, criterion: dict) -> dict:
    pred = criterion.get("predicate") or {}
    needle = norm(pred.get("text") or "")
    blob = facts.get("document_text") or ""
    if needle and _phrase_in(blob, needle):
        return _result(criterion, "fail", "text_still_present")
    return _result(criterion, "pass", "text_absent")


def _paragraph_style(facts: dict, criterion: dict) -> dict:
    pred = criterion.get("predicate") or {}
    needle = norm(pred.get("text") or "")
    style = pred.get("style") or ""
    for para in facts.get("paragraphs") or []:
        text = para.get("text") or ""
        if not same(para.get("style"), style):
            continue
        if same(text, needle) or (needle and norm(text).casefold().startswith(needle.casefold())):
            return _result(criterion, "pass", "paragraph_style_matches")
    return _result(criterion, "fail", "paragraph_style_mismatch")


def _text_effect(facts: dict, criterion: dict) -> dict:
    needle = norm((criterion.get("predicate") or {}).get("text") or "")
    blob = " ".join(facts.get("text_effects") or [])
    if needle and needle.casefold() in blob.casefold():
        return _result(criterion, "pass", "text_effect_present")
    return _result(criterion, "fail", "text_effect_missing")


def _section_columns(facts: dict, criterion: dict) -> dict:
    minimum = int((criterion.get("predicate") or {}).get("min") or 2)
    if any(int(s.get("cols") or 1) >= minimum for s in facts.get("sections") or []):
        return _result(criterion, "pass", "section_columns_present")
    return _result(criterion, "fail", "section_columns_missing")


def _page_orientation(facts: dict, criterion: dict) -> dict:
    expected = ((criterion.get("predicate") or {}).get("orient") or "landscape").casefold()
    if any((s.get("orient") or "portrait").casefold() == expected for s in facts.get("sections") or []):
        return _result(criterion, "pass", "page_orientation_matches")
    return _result(criterion, "fail", "page_orientation_mismatch")


def _break_present(facts: dict, criterion: dict) -> dict:
    kind = (criterion.get("predicate") or {}).get("name") or "page"
    minimum = int((criterion.get("predicate") or {}).get("min") or 1)
    got = int((facts.get("breaks") or {}).get(kind) or 0)
    if got >= minimum:
        return _result(criterion, "pass", "break_present")
    return _result(criterion, "fail", "break_missing")


def _section_count(facts: dict, criterion: dict) -> dict:
    minimum = int((criterion.get("predicate") or {}).get("min") or 2)
    if len(facts.get("sections") or []) >= minimum:
        return _result(criterion, "pass", "section_count_ok")
    return _result(criterion, "fail", "section_count_low")


def _table_count(facts: dict, criterion: dict) -> dict:
    minimum = int((criterion.get("predicate") or {}).get("min") or 1)
    if len(facts.get("tables") or []) >= minimum:
        return _result(criterion, "pass", "table_count_ok")
    return _result(criterion, "fail", "table_count_low")


def _table_has_text(facts: dict, criterion: dict) -> dict:
    pred = criterion.get("predicate") or {}
    needle = norm(pred.get("text") or "")
    exact = bool(pred.get("exact"))
    for tbl in facts.get("tables") or []:
        for row in tbl.get("cells") or []:
            for cell in row:
                if not needle:
                    continue
                if exact and same(cell, needle):
                    return _result(criterion, "pass", "table_text_present")
                if not exact and needle.casefold() in norm(cell).casefold():
                    return _result(criterion, "pass", "table_text_present")
    return _result(criterion, "fail", "table_text_missing")


def _table_lacks_text(facts: dict, criterion: dict) -> dict:
    needle = norm((criterion.get("predicate") or {}).get("text") or "")
    for tbl in facts.get("tables") or []:
        for row in tbl.get("cells") or []:
            for cell in row:
                if needle and same(cell, needle):
                    return _result(criterion, "fail", "table_text_still_present")
    return _result(criterion, "pass", "table_text_absent")


def _table_dim(facts: dict, criterion: dict) -> dict:
    pred = criterion.get("predicate") or {}
    rows = int(pred.get("rows") or 0)
    cols = int(pred.get("cols") or 0)
    for tbl in facts.get("tables") or []:
        ok_rows = rows == 0 or int(tbl.get("rows") or 0) == rows
        ok_cols = cols == 0 or int(tbl.get("cols") or 0) == cols
        if ok_rows and ok_cols:
            return _result(criterion, "pass", "table_dim_matches")
    return _result(criterion, "fail", "table_dim_mismatch")


def _table_data_starts(facts: dict, criterion: dict) -> dict:
    needle = norm((criterion.get("predicate") or {}).get("text") or "")
    headers = {"id", "customer", "appointment", "lastname", "firstname", "address", "city", "state", "date", "time"}
    for tbl in facts.get("tables") or []:
        if int(tbl.get("rows") or 0) < 4:
            continue
        for row in tbl.get("cells") or []:
            first = norm(row[0] if row else "")
            if not first or first.casefold() in headers:
                continue
            if same(first, needle):
                return _result(criterion, "pass", "table_sorted")
            return _result(criterion, "fail", "table_not_sorted")
    return _result(criterion, "fail", "table_not_sorted")


def _table_repeat_header(facts: dict, criterion: dict) -> dict:
    if any(t.get("header") for t in facts.get("tables") or []):
        return _result(criterion, "pass", "table_header_repeat")
    return _result(criterion, "fail", "table_header_missing")


def _table_merged(facts: dict, criterion: dict) -> dict:
    if any(t.get("merged") for t in facts.get("tables") or []):
        return _result(criterion, "pass", "table_merged")
    return _result(criterion, "fail", "table_not_merged")


def _list_format(facts: dict, criterion: dict) -> dict:
    pred = criterion.get("predicate") or {}
    needle = norm(pred.get("text") or "")
    fmt = (pred.get("fmt") or "").casefold()
    for para in facts.get("paragraphs") or []:
        if needle and not same(para.get("text"), needle) and needle.casefold() not in norm(para.get("text")).casefold():
            continue
        if fmt and (para.get("num_fmt") or "").casefold() == fmt:
            return _result(criterion, "pass", "list_format_matches")
        if not fmt and para.get("num_fmt"):
            return _result(criterion, "pass", "list_present")
    if not needle and fmt and fmt in [f.casefold() for f in facts.get("numbering_formats") or []]:
        return _result(criterion, "pass", "list_format_present")
    return _result(criterion, "fail", "list_format_missing")


def _footnote_min(facts: dict, criterion: dict) -> dict:
    minimum = int((criterion.get("predicate") or {}).get("min") or 1)
    if int(facts.get("footnote_count") or 0) >= minimum:
        return _result(criterion, "pass", "footnotes_present")
    return _result(criterion, "fail", "footnotes_missing")


def _field_contains(facts: dict, criterion: dict) -> dict:
    needle = ((criterion.get("predicate") or {}).get("text") or "").upper()
    if any(needle in (f or "").upper() for f in facts.get("fields") or []):
        return _result(criterion, "pass", "field_present")
    return _result(criterion, "fail", "field_missing")


def _style_used(facts: dict, criterion: dict) -> dict:
    pred = criterion.get("predicate") or {}
    style = pred.get("style") or pred.get("name") or ""
    minimum = int(pred.get("min") or 1)
    got = int((facts.get("style_counts") or {}).get(style) or 0)
    if style and got >= minimum:
        return _result(criterion, "pass", "style_used")
    return _result(criterion, "fail", "style_unused")


def _drawing_kind(facts: dict, criterion: dict) -> dict:
    kind = ((criterion.get("predicate") or {}).get("kind") or "").casefold()
    kinds = [k.casefold() for k in facts.get("drawing_kinds") or []]
    if kind == "model3d" and (facts.get("has_3d") or kind in kinds):
        return _result(criterion, "pass", "drawing_present")
    if kind == "smartart" and (facts.get("has_smartart") or kind in kinds):
        return _result(criterion, "pass", "drawing_present")
    if kind in kinds:
        return _result(criterion, "pass", "drawing_present")
    return _result(criterion, "fail", "drawing_missing")


def _drawing_text(facts: dict, criterion: dict) -> dict:
    pred = criterion.get("predicate") or {}
    needle = norm(pred.get("text") or "")
    minimum = int(pred.get("min") or 1)
    hits = [t for t in facts.get("drawing_texts") or [] if needle and needle.casefold() in norm(t).casefold()]
    if len(hits) >= minimum:
        return _result(criterion, "pass", "drawing_text_present")
    return _result(criterion, "fail", "drawing_text_missing")


def _artistic_effect(facts: dict, criterion: dict) -> dict:
    needle = ((criterion.get("predicate") or {}).get("name") or "").casefold()
    found = [e.casefold() for e in facts.get("artistic_effects") or []]
    if needle and any(needle in e for e in found):
        return _result(criterion, "pass", "artistic_effect_present")
    if not needle and found:
        return _result(criterion, "pass", "artistic_effect_present")
    return _result(criterion, "fail", "artistic_effect_missing")


def _picture_effect(facts: dict, criterion: dict) -> dict:
    needle = ((criterion.get("predicate") or {}).get("name") or "").casefold()
    found = [e.casefold() for e in facts.get("picture_effects") or []]
    if needle and any(needle in e for e in found):
        return _result(criterion, "pass", "picture_effect_present")
    if not needle and found:
        return _result(criterion, "pass", "picture_effect_present")
    return _result(criterion, "fail", "picture_effect_missing")


def _alt_text(facts: dict, criterion: dict) -> dict:
    needle = norm((criterion.get("predicate") or {}).get("text") or "")
    blob = " ".join(facts.get("alt_texts") or [])
    if needle and needle.casefold() in blob.casefold():
        return _result(criterion, "pass", "alt_text_present")
    return _result(criterion, "fail", "alt_text_missing")


def _wrap_type(facts: dict, criterion: dict) -> dict:
    needle = ((criterion.get("predicate") or {}).get("wrap") or "").casefold()
    found = [w.casefold() for w in facts.get("wraps") or []]
    if needle and any(needle in w for w in found):
        return _result(criterion, "pass", "wrap_present")
    return _result(criterion, "fail", "wrap_missing")


def _comment_text(facts: dict, criterion: dict) -> dict:
    needle = norm((criterion.get("predicate") or {}).get("text") or "")
    if any(needle and needle.casefold() in norm(c.get("text")).casefold() for c in facts.get("comments") or []):
        return _result(criterion, "pass", "comment_present")
    return _result(criterion, "fail", "comment_missing")


def _comment_author(facts: dict, criterion: dict) -> dict:
    needle = norm((criterion.get("predicate") or {}).get("author") or "")
    if any(same(c.get("author"), needle) for c in facts.get("comments") or []):
        return _result(criterion, "pass", "comment_author_present")
    return _result(criterion, "fail", "comment_author_missing")


def _comment_absent_text(facts: dict, criterion: dict) -> dict:
    needle = norm((criterion.get("predicate") or {}).get("text") or "")
    if any(needle and needle.casefold() in norm(c.get("text")).casefold() for c in facts.get("comments") or []):
        return _result(criterion, "fail", "comment_still_present")
    return _result(criterion, "pass", "comment_removed")


def _comment_resolved(facts: dict, criterion: dict) -> dict:
    minimum = int((criterion.get("predicate") or {}).get("min") or 1)
    if int(facts.get("resolved_count") or 0) >= minimum:
        return _result(criterion, "pass", "comment_resolved")
    return _result(criterion, "fail", "comment_unresolved")


def _comment_reply(facts: dict, criterion: dict) -> dict:
    minimum = int((criterion.get("predicate") or {}).get("min") or 1)
    if int(facts.get("reply_count") or 0) >= minimum:
        return _result(criterion, "pass", "comment_reply_present")
    return _result(criterion, "fail", "comment_reply_missing")


def _revision_max(facts: dict, criterion: dict) -> dict:
    maximum = int((criterion.get("predicate") or {}).get("max") or 0)
    if int(facts.get("revision_count") or 0) <= maximum:
        return _result(criterion, "pass", "revisions_within_max")
    return _result(criterion, "fail", "revisions_too_many")


def _document_protection(facts: dict, criterion: dict) -> dict:
    if facts.get("document_protection"):
        return _result(criterion, "pass", "tracking_lock_present")
    return _result(criterion, "fail", "tracking_lock_missing")


def _hdphoto(facts: dict, criterion: dict) -> dict:
    if facts.get("has_hdphoto"):
        return _result(criterion, "pass", "hdphoto_present")
    return _result(criterion, "fail", "hdphoto_missing")


def _action_unverified(criterion: dict) -> dict:
    return _result(criterion, "unverified", "missing_observer")


def _field(event: dict, *keys):
    for key in keys:
        if not isinstance(event, dict):
            return None
        if key in event and event[key] is not None and event[key] != "":
            return event[key]
    return None


def _as_bool(value) -> bool:
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return bool(value)
    return str(value or "").strip().lower() in {"1", "true", "yes", "on"}


def _key(value: str | None) -> str:
    return re.sub(r"[\s_\-]+", "", (value or "")).casefold()


def _query_same(got, expected, match_case: bool) -> bool:
    got = str(got or "").strip()
    expected = str(expected or "").strip()
    if not expected:
        return True
    if match_case:
        return got == expected
    return got.casefold() == expected.casefold()


def _style_same(got, expected) -> bool:
    want = _key(expected)
    if not want:
        return True
    return _key(got) == want


def _source_ok(required, got, action: str) -> bool:
    want = _key(required)
    if not want:
        return True
    have = _key(got)
    if want in {"navigationpane", "nav", "pane"}:
        return have in {"navigationpane", "nav", "pane", "find"} or action in {"find", "navigation_pane"}
    return have == want or want in have


def _coerce_evidence(evidence) -> list[dict]:
    if evidence is None:
        return []
    if isinstance(evidence, str):
        evidence = evidence.strip()
        if not evidence:
            return []
        evidence = json.loads(evidence)
    if isinstance(evidence, dict):
        evidence = evidence.get("events") or evidence.get("actions") or evidence.get("evidence") or []
    if not isinstance(evidence, list):
        return []
    out: list[dict] = []
    for item in evidence:
        if not isinstance(item, dict):
            continue
        detail = item.get("detail")
        if isinstance(detail, dict):
            merged = dict(detail)
            for key, value in item.items():
                if key == "detail":
                    continue
                merged.setdefault(key, value)
            out.append(merged)
        else:
            out.append(item)
    return out


def _grade_action(criterion: dict, events: list[dict]) -> dict:
    if not events:
        return _action_unverified(criterion)
    pred = criterion.get("predicate") or {}
    sel = criterion.get("selector") or {}
    kind = str(pred.get("type") or sel.get("action") or "").casefold()
    query = pred.get("query") or sel.get("query") or pred.get("text")
    style = pred.get("style") or sel.get("style")
    name = pred.get("name") or sel.get("bookmark")
    page = pred.get("page") if pred.get("page") is not None else sel.get("page")
    min_hits = int(pred.get("min_hits") or pred.get("min") or 1)
    match_case = _as_bool(pred.get("match_case") or sel.get("match_case"))
    whole_word = _as_bool(pred.get("whole_word") or sel.get("whole_word"))
    source = sel.get("source")
    for ev in events:
        if ev.get("ok") is False:
            continue
        action = str(_field(ev, "action", "type") or "").casefold()
        ev_query = _field(ev, "query", "text")
        ev_style = _field(ev, "style")
        ev_name = _field(ev, "name", "bookmark", "query")
        ev_page = _field(ev, "page")
        ev_hits = int(ev.get("hits") or 1)
        ev_source = _field(ev, "source")
        if kind in {"search_query", "find"}:
            if action not in {"find", "search", "navigation_pane"}:
                continue
            if not _query_same(ev_query, query, match_case):
                continue
            if match_case and not _as_bool(ev.get("match_case") or ev.get("matchCase")):
                continue
            if whole_word and not _as_bool(ev.get("whole_word") or ev.get("wholeWord")):
                continue
            if not _source_ok(source, ev_source, action):
                continue
            return _result(criterion, "pass", "action_observed", [f"action:{action}"])
        if kind == "results_tab" and action in {"results_tab", "results"}:
            return _result(criterion, "pass", "action_observed", ["action:results_tab"])
        if kind in {"search_navigate", "find_navigate"}:
            if action not in {"find_navigate", "find", "search"}:
                continue
            if not _query_same(ev_query, query, False):
                continue
            if ev_hits >= min_hits:
                return _result(criterion, "pass", "action_observed", [f"action:{action}"])
        if kind == "advanced_find":
            if action not in {"advanced_find", "find"}:
                continue
            if not _query_same(ev_query, query, False):
                continue
            if not _style_same(ev_style, style):
                continue
            return _result(criterion, "pass", "action_observed", ["action:advanced_find"])
        if kind == "goto_graphic" and (action == "goto_graphic" or (action == "goto" and _style_same(_field(ev, "detail", "what"), "graphic"))):
            return _result(criterion, "pass", "action_observed", ["action:goto_graphic"])
        if kind == "goto_page":
            if action not in {"goto_page", "goto"}:
                continue
            if page is None or str(ev_page) == str(page) or ev_page == page:
                return _result(criterion, "pass", "action_observed", ["action:goto_page"])
        if kind == "goto_bookmark":
            if action not in {"goto_bookmark", "goto"}:
                continue
            if _query_same(ev_name, name, False):
                return _result(criterion, "pass", "action_observed", ["action:goto_bookmark"])
        if kind == "save_alternate_format" and action in {"save_alternate_format", "save_as", "export_pdf"}:
            return _result(criterion, "pass", "action_observed", ["action:save_as"])
        if kind == "print_settings" and action in {"print_settings", "print"}:
            return _result(criterion, "pass", "action_observed", ["action:print"])
        if kind == "share_electronic" and action in {"share_electronic", "share"}:
            return _result(criterion, "pass", "action_observed", ["action:share"])
        if kind == "inspect_document" and action in {"inspect_document", "inspect"}:
            return _result(criterion, "pass", "action_observed", ["action:inspect"])
        if kind == "compatibility_check" and action in {"compatibility_check", "compatibility"}:
            return _result(criterion, "pass", "action_observed", ["action:compatibility"])
    return _result(criterion, "fail", "action_missing")


def evaluate_facts(facts: dict, rubric: dict, evidence: list | None = None) -> dict:
    criteria = list(rubric.get("criteria") or [])
    results: list[dict] = []
    parse_error = not facts.get("ok")
    error_code = str(facts.get("error") or "parse_error")
    events = _coerce_evidence(evidence)
    for criterion in criteria:
        kind = criterion.get("kind") or "artifact"
        if kind == "action_sequence":
            results.append(_grade_action(criterion, events))
            continue
        if parse_error:
            results.append(_result(criterion, "error", error_code))
            continue
        pred = (criterion.get("predicate") or {}).get("type")
        dispatch = {
            "bookmark_range": _bookmark_range,
            "internal_hyperlink": _internal_hyperlink,
            "page_background": _page_background,
            "watermark_text": _watermark_text,
            "page_border": _page_border,
            "header_contains": _header_contains,
            "header_instruction": _header_instruction,
            "first_page_header": _first_page_header,
            "style_size": _style_size,
            "core_property": _core_property,
            "comments_absent": _comments_absent,
            "revisions_cleared": _revisions_cleared,
            "hidden_text_absent": _hidden_text_absent,
            "contains_text": _contains_text,
            "not_contains_text": _not_contains_text,
            "paragraph_style": _paragraph_style,
            "text_effect": _text_effect,
            "section_columns": _section_columns,
            "page_orientation": _page_orientation,
            "break_present": _break_present,
            "section_count": _section_count,
            "table_count": _table_count,
            "table_has_text": _table_has_text,
            "table_lacks_text": _table_lacks_text,
            "table_data_starts": _table_data_starts,
            "table_dim": _table_dim,
            "table_repeat_header": _table_repeat_header,
            "table_merged": _table_merged,
            "list_format": _list_format,
            "footnote_min": _footnote_min,
            "field_contains": _field_contains,
            "style_used": _style_used,
            "drawing_kind": _drawing_kind,
            "drawing_text": _drawing_text,
            "artistic_effect": _artistic_effect,
            "picture_effect": _picture_effect,
            "alt_text": _alt_text,
            "wrap_type": _wrap_type,
            "comment_text": _comment_text,
            "comment_author": _comment_author,
            "comment_absent_text": _comment_absent_text,
            "comment_resolved": _comment_resolved,
            "comment_reply": _comment_reply,
            "revision_max": _revision_max,
            "document_protection": _document_protection,
            "hdphoto": _hdphoto,
        }
        handler = dispatch.get(pred)
        if handler:
            results.append(handler(facts, criterion))
        else:
            results.append(_result(criterion, "error", "unknown_predicate"))

    verified = round(sum(r["earned"] for r in results), 1)
    pending = round(sum(r["pending"] for r in results), 1)
    possible = round(sum(r["possible"] for r in results) or float(rubric.get("max_score") or 100), 1)
    errors = any(r["status"] == "error" for r in results)
    complete = pending == 0 and not errors
    checks = [
        {
            "ok": r["status"] == "pass",
            "label": f"{r['criterion_id']} {r['message']}".strip(),
            "weight": r["possible"],
            "status": r["status"],
            "reason_code": r["reason_code"],
        }
        for r in results
    ]
    return {
        "score": verified,
        "verified": verified,
        "pending": pending,
        "max_score": possible or 100,
        "complete": complete,
        "status": "final" if complete else "provisional",
        "criteria": results,
        "checks": checks,
        "grader_version": GRADER_VERSION,
        "rubric_version": rubric.get("rubric_version") or "",
        "error": error_code if parse_error else None,
    }


def grade_path(path: Path | None, rubric: dict | None = None, evidence: list | None = None) -> dict:
    rubric = load_rubric(rubric)
    if path is None:
        facts = extract_word_facts(Path(""))
    else:
        facts = extract_word_facts(Path(path))
    return evaluate_facts(facts, rubric, evidence)
