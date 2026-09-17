"""Rubric evaluator — artifact facts + optional action evidence."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

from app.word_xml import extract_word_facts, norm, same

GRADER_VERSION = "1.1.0"
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


def _action_unverified(criterion: dict) -> dict:
    return _result(criterion, "unverified", "missing_observer")


def evaluate_facts(facts: dict, rubric: dict, evidence: list | None = None) -> dict:
    criteria = list(rubric.get("criteria") or [])
    results: list[dict] = []
    parse_error = not facts.get("ok")
    error_code = str(facts.get("error") or "parse_error")
    for criterion in criteria:
        kind = criterion.get("kind") or "artifact"
        if kind == "action_sequence":
            if evidence:
                results.append(_result(criterion, "unverified", "observer_not_capable"))
            else:
                results.append(_action_unverified(criterion))
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
