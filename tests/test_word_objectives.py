"""MOS Word Objective 2–6 training packs — starter 0, results 100."""
from __future__ import annotations

from pathlib import Path

from app.grade import grade_path, load_rubric

ROOT = Path(__file__).resolve().parent.parent
RUBRICS = ROOT / "app" / "rubrics"
FIXTURES = ROOT / "tests" / "fixtures"
PACKS = [
    "2-1",
    "2-2",
    "2-3",
    "3-1",
    "3-2",
    "3-3",
    "4-1",
    "4-2a",
    "4-2b",
    "4-2c",
    "5-1",
    "5-2",
    "5-3",
    "5-4",
    "6-1",
    "6-2",
]


def _pack(key: str):
    pid = f"word-objective-{key}"
    folder = FIXTURES / pid
    starter = next(p for p in folder.glob("Word_*.docx") if "_results" not in p.name)
    results = next(folder.glob("Word_*_results.docx"))
    return load_rubric(RUBRICS / f"{pid}.json"), starter, results


def test_all_new_packs_have_training_help():
    for key in PACKS:
        rubric, _, _ = _pack(key)
        assert rubric["criteria"], key
        assert abs(sum(c["weight"] for c in rubric["criteria"]) - 100) < 0.1, key
        for item in rubric["criteria"]:
            steps = item.get("help_steps") or []
            assert len(steps) >= 2, item["id"]
            assert any("**" in step for step in steps), item["id"]


def test_all_new_packs_starter_zero_results_100():
    for key in PACKS:
        rubric, starter, results = _pack(key)
        start = grade_path(starter, rubric)
        done = grade_path(results, rubric)
        assert start["verified"] == 0, (key, start)
        assert done["verified"] == 100, (key, done)
        assert done["pending"] == 0, key
        assert done["complete"] is True, key
        assert all(c["status"] == "pass" for c in done["criteria"]), key
        assert all(c["status"] == "fail" for c in start["criteria"]), key


def test_starters_exist_in_projects():
    for key in PACKS:
        path = ROOT / "data" / "projects" / f"word-objective-{key}" / f"Word_{key}.docx"
        assert path.is_file(), path
