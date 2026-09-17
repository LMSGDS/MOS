"""Rubric evaluator — artifact facts + optional action evidence."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from app.word_xml import extract_word_facts, same

GRADER_VERSION = "1.0.0"
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
        if pred == "bookmark_range":
            results.append(_bookmark_range(facts, criterion))
        elif pred == "internal_hyperlink":
            results.append(_internal_hyperlink(facts, criterion))
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
