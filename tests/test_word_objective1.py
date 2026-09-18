"""Word Objective 1.2–1.4 — Manage documents artifact graders."""
from __future__ import annotations

from pathlib import Path

from app.grade import grade_path, load_rubric

ROOT = Path(__file__).resolve().parent.parent
RUBRICS = ROOT / "app" / "rubrics"
FIXTURES = ROOT / "tests" / "fixtures"


def _pack(n: str):
    folder = FIXTURES / f"word-objective-1-{n}"
    return (
        load_rubric(RUBRICS / f"word-objective-1-{n}.json"),
        folder / f"Word_1-{n}.docx",
        folder / f"Word_1-{n}_results.docx",
    )


def test_objective_1_training_help_steps():
    for n in ("1", "2", "3", "4"):
        rubric = load_rubric(RUBRICS / f"word-objective-1-{n}.json")
        assert rubric["criteria"], n
        for item in rubric["criteria"]:
            steps = item.get("help_steps") or []
            assert len(steps) >= 2, item["id"]
            assert any("**" in step for step in steps), item["id"]


def test_word_12_starter_fails_results_earn_100():
    rubric, starter, results = _pack("2")
    start = grade_path(starter, rubric)
    done = grade_path(results, rubric)
    assert start["verified"] == 0
    assert start["pending"] == 0
    assert {c["criterion_id"] for c in start["criteria"] if c["status"] == "fail"} == {
        "W12-BG01",
        "W12-WM01",
        "W12-BR01",
        "W12-HD01",
        "W12-HD02",
        "W12-FP01",
        "W12-ST01",
    }
    assert done["verified"] == 100
    assert done["pending"] == 0
    assert done["complete"] is True
    assert all(c["status"] == "pass" for c in done["criteria"])


def test_word_13_properties_55_and_print_share_unverified():
    rubric, starter, results = _pack("3")
    start = grade_path(starter, rubric)
    done = grade_path(results, rubric)
    assert start["verified"] == 0
    assert start["pending"] == 45
    assert done["verified"] == 55
    assert done["pending"] == 45
    assert {c["criterion_id"] for c in done["criteria"] if c["status"] == "pass"} == {
        "W13-P01",
        "W13-P02",
        "W13-P03",
    }
    assert {c["criterion_id"] for c in done["criteria"] if c["status"] == "unverified"} == {
        "W13-S01",
        "W13-S02",
        "W13-S03",
    }


def test_word_14_inspect_70_artifact_30_unverified():
    rubric, starter, results = _pack("4")
    start = grade_path(starter, rubric)
    done = grade_path(results, rubric)
    assert start["verified"] == 0
    by_id = {c["criterion_id"]: c for c in start["criteria"]}
    assert by_id["W14-C01"]["status"] == "fail"
    assert by_id["W14-R01"]["status"] == "fail"
    assert by_id["W14-H01"]["status"] == "fail"
    assert done["verified"] == 70
    assert done["pending"] == 30
    assert {c["criterion_id"] for c in done["criteria"] if c["status"] == "pass"} == {
        "W14-C01",
        "W14-R01",
        "W14-H01",
    }
