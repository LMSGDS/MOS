"""MOS Excel MO-200 Study Guide packs — starter 0, results 100."""
from __future__ import annotations

from pathlib import Path

from app.bank import TAXONOMY
from app.grade import grade_path, load_rubric

ROOT = Path(__file__).resolve().parent.parent
RUBRICS = ROOT / "app" / "rubrics"
FIXTURES = ROOT / "tests" / "fixtures"
PROJECTS = ROOT / "data" / "projects"

PACKS = [
    "1-1",
    "1-2",
    "1-3",
    "1-4",
    "1-5a",
    "1-5b",
    "2-1",
    "2-2",
    "2-3",
    "2-4",
    "3-1",
    "3-2",
    "3-3",
    "4-1",
    "4-2",
    "4-3",
    "5-1",
    "5-2",
    "5-3",
]


def test_mo200_taxonomy_matches_study_guide():
    leaves = [code for _c, _t, kids in TAXONOMY["MO-200"] for code, _n in kids]
    assert leaves == [
        "1.1",
        "1.2",
        "1.3",
        "1.4",
        "1.5",
        "2.1",
        "2.2",
        "2.3",
        "2.4",
        "3.1",
        "3.2",
        "3.3",
        "4.1",
        "4.2",
        "4.3",
        "5.1",
        "5.2",
        "5.3",
    ]


def test_excel_packs_have_training_help():
    for key in PACKS:
        rubric = load_rubric(RUBRICS / f"excel-objective-{key}.json")
        assert rubric["program"] == "excel"
        assert abs(sum(c["weight"] for c in rubric["criteria"]) - 100) < 0.1, key
        for item in rubric["criteria"]:
            steps = item.get("help_steps") or []
            assert len(steps) >= 2, item["id"]
            assert any("**" in step for step in steps), item["id"]


def test_excel_starter_zero_results_100():
    for key in PACKS:
        pid = f"excel-objective-{key}"
        folder = FIXTURES / pid
        starter = folder / f"Excel_{key}.xlsx"
        results = folder / f"Excel_{key}_results.xlsx"
        rubric = load_rubric(RUBRICS / f"{pid}.json")
        start = grade_path(starter, rubric)
        done = grade_path(results, rubric)
        assert start["verified"] == 0, (key, start)
        assert done["verified"] == 100, (key, done)
        assert done["complete"] is True, key
        assert all(c["status"] == "pass" for c in done["criteria"]), key
        assert all(c["status"] == "fail" for c in start["criteria"]), key


def test_excel_starters_exist_in_projects():
    for key in PACKS:
        path = PROJECTS / f"excel-objective-{key}" / f"Excel_{key}.xlsx"
        assert path.is_file(), path


def test_excel_import_extras_travel_with_starter():
    from app.project_files import project_extra_files

    starter = PROJECTS / "excel-objective-1-1" / "Excel_1-1.xlsx"
    extras = {p.name for p in project_extra_files(starter)}
    assert extras.issuperset({"Excel_1-1_ContactList.txt", "Excel_1-1_GlobalPopulationData.csv"}), extras
