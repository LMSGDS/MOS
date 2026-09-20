"""MOS PowerPoint MO-300 Study Guide packs — starter 0, results 100."""
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
    "1-4",
    "1-5",
    "2-1",
    "2-1b",
    "2-2",
    "2-3",
    "3-1",
    "3-2",
    "3-3",
    "3-4",
    "3-5",
    "4-1",
    "4-2",
    "4-3",
    "4-4",
    "5-1",
    "5-3",
]


def test_mo300_taxonomy_matches_study_guide():
    leaves = [code for _c, _t, kids in TAXONOMY["MO-300"] for code, _n in kids]
    assert leaves == [
        "1.1",
        "1.2",
        "1.3",
        "1.4",
        "1.5",
        "2.1",
        "2.2",
        "2.3",
        "3.1",
        "3.2",
        "3.3",
        "3.4",
        "3.5",
        "4.1",
        "4.2",
        "4.3",
        "4.4",
        "4.5",
        "5.1",
        "5.2",
        "5.3",
    ]


def test_ppt_packs_have_training_help():
    for key in PACKS + ["1-3", "4-5", "5-2"]:
        rubric = load_rubric(RUBRICS / f"powerpoint-objective-{key}.json")
        assert rubric["program"] == "powerpoint"
        assert abs(sum(c["weight"] for c in rubric["criteria"]) - 100) < 0.1, key
        for item in rubric["criteria"]:
            steps = item.get("help_steps") or []
            assert len(steps) >= 2, item["id"]
            assert any("**" in step for step in steps), item["id"]


def test_ppt_starter_zero_results_100():
    for key in PACKS:
        pid = f"powerpoint-objective-{key}"
        folder = FIXTURES / pid
        starter = folder / f"PowerPoint_{key}.pptx"
        results = folder / f"PowerPoint_{key}_results.pptx"
        rubric = load_rubric(RUBRICS / f"{pid}.json")
        start = grade_path(starter, rubric)
        done = grade_path(results, rubric)
        assert start["verified"] == 0, (key, start)
        assert done["verified"] == 100, (key, done)
        assert done["complete"] is True, key
        assert all(c["status"] == "pass" for c in done["criteria"]), key
        assert all(c["status"] == "fail" for c in start["criteria"]), key


def test_ppt_print_settings_stay_unverified_without_observer():
    rubric = load_rubric(RUBRICS / "powerpoint-objective-1-3.json")
    starter = PROJECTS / "powerpoint-objective-1-3" / "PowerPoint_1-3.pptx"
    start = grade_path(starter, rubric)
    assert start["verified"] == 0
    assert start["pending"] == 100
    assert all(c["status"] == "unverified" for c in start["criteria"])


def test_ppt_starters_exist_in_projects():
    for key in PACKS + ["1-3", "4-5", "5-2"]:
        path = PROJECTS / f"powerpoint-objective-{key}" / f"PowerPoint_{key}.pptx"
        assert path.is_file(), path


def test_ppt_study_guide_extras_travel_with_starter():
    from app.project_files import project_extra_files

    needed = {
        "2-1": ["PowerPoint_2-1a.docx"],
        "3-3": ["PowerPoint_3-3a.jpg", "PowerPoint_3-3b.jpg"],
        "4-1": ["PowerPoint_4-1.xlsx"],
        "4-2": ["PowerPoint_4-2.xlsx"],
        "4-4": ["PowerPoint_4-4.3mf"],
    }
    for key, names in needed.items():
        folder = PROJECTS / f"powerpoint-objective-{key}"
        starter = folder / f"PowerPoint_{key}.pptx"
        extras = {p.name for p in project_extra_files(starter)}
        assert extras.issuperset(names), (key, extras)
