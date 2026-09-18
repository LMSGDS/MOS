"""Action-sequence grading from demo / observer evidence (no Word required)."""
from __future__ import annotations

import json

from app.grade import GRADER_VERSION, evaluate_facts, grade_path
from app.word_xml import extract_word_facts
from tests.test_word_11 import RESULTS, RUBRIC, STARTER
from tests.test_word_objective1 import _pack

DEMO_W11 = [
    {"action": "find", "query": "to", "source": "navigation_pane", "hits": 3},
    {"action": "results_tab", "query": "to", "source": "navigation_pane"},
    {"action": "find_navigate", "query": "toy", "hits": 2, "source": "navigation_pane"},
    {"action": "find", "query": "Toymakers", "match_case": True, "whole_word": True, "hits": 1},
    {"action": "advanced_find", "query": "toy", "style": "Heading 2"},
    {"action": "goto_graphic"},
    {"action": "goto_page", "page": 3},
    {"action": "goto_bookmark", "name": "SalesManager"},
]


def test_grader_version_action_kit():
    assert GRADER_VERSION == "1.3.1"


def test_word_11_demo_evidence_makes_results_complete():
    graded = grade_path(RESULTS, RUBRIC, evidence=DEMO_W11)
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    for cid in (
        "W11-S01",
        "W11-S02",
        "W11-S03",
        "W11-S04",
        "W11-S05",
        "W11-N01",
        "W11-N02",
        "W11-N03",
        "W11-B01",
        "W11-H01",
    ):
        assert by_id[cid]["status"] == "pass", cid
    assert graded["verified"] == 100
    assert graded["pending"] == 0
    assert graded["complete"] is True
    assert graded["status"] == "final"


def test_word_11_demo_evidence_wrapped_events_object():
    graded = evaluate_facts(extract_word_facts(RESULTS), RUBRIC, evidence={"events": DEMO_W11})
    assert graded["pending"] == 0
    assert graded["verified"] == 100


def test_word_11_incomplete_find_fails_actions_not_unverified():
    graded = grade_path(STARTER, RUBRIC, evidence=[{"action": "find", "query": "hello"}])
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    assert by_id["W11-S01"]["status"] == "fail"
    assert by_id["W11-S01"]["reason_code"] == "action_missing"
    assert by_id["W11-B01"]["status"] == "fail"
    assert graded["pending"] == 0


def test_word_11_toymakers_does_not_count_as_to():
    graded = grade_path(
        RESULTS,
        RUBRIC,
        evidence=[{"action": "find", "query": "Toymakers", "match_case": True, "whole_word": True, "source": "navigation_pane"}],
    )
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    assert by_id["W11-S04"]["status"] == "pass"
    assert by_id["W11-S01"]["status"] == "fail"


def test_word_13_demo_evidence_completes_save_print_share():
    rubric, _starter, results = _pack("3")
    evidence = [
        {"action": "save_alternate_format", "format": "pdf"},
        {"action": "print_settings"},
        {"action": "share_electronic", "source": "demo"},
    ]
    graded = grade_path(results, rubric, evidence=evidence)
    assert graded["verified"] == 100
    assert graded["pending"] == 0
    assert graded["complete"] is True


def test_word_14_demo_evidence_completes_inspect():
    rubric, _starter, results = _pack("4")
    evidence = [
        {"action": "inspect_document"},
        {"action": "compatibility_check"},
    ]
    graded = grade_path(results, rubric, evidence=evidence)
    assert graded["verified"] == 100
    assert graded["pending"] == 0


def test_nested_telemetry_detail_is_flattened():
    evidence = [
        {
            "action": "find",
            "skill": "W11-S01",
            "detail": {"query": "to", "source": "navigation_pane", "hits": 2},
        }
    ]
    graded = grade_path(RESULTS, RUBRIC, evidence=evidence)
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    assert by_id["W11-S01"]["status"] == "pass"
    assert by_id["W11-S02"]["status"] == "fail"


def test_demo_evidence_json_roundtrip():
    raw = json.dumps({"events": DEMO_W11})
    graded = grade_path(RESULTS, RUBRIC, evidence=raw)
    assert graded["verified"] == 100
    assert graded["pending"] == 0
    assert graded["grader_version"] == "1.3.1"


def test_goto_page_accepts_int_float_and_string():
    for page in (3, 3.0, "3", "3.0"):
        graded = grade_path(RESULTS, RUBRIC, evidence=[{"action": "goto_page", "page": page}])
        by_id = {c["criterion_id"]: c for c in graded["criteria"]}
        assert by_id["W11-N02"]["status"] == "pass", page
