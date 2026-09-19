"""Q-matrix: locate → tool → configure, first break wins."""
from __future__ import annotations

from pathlib import Path

from app.grade import GRADER_VERSION, grade_path, load_rubric
from app.qmatrix import attach, evaluate, nodes_for, skill_label

ROOT = Path(__file__).resolve().parent.parent
RUBRICS = ROOT / "app" / "rubrics"
FIXTURES = ROOT / "tests" / "fixtures"


def _pack(key: str):
    pid = f"word-objective-{key}"
    folder = FIXTURES / pid
    starter = next(p for p in folder.glob("Word_*.docx") if "_results" not in p.name)
    results = next(folder.glob("Word_*_results.docx"))
    return load_rubric(RUBRICS / f"{pid}.json"), starter, results


def test_grader_version_qmatrix():
    assert GRADER_VERSION == "1.4.0"


def test_skill_labels():
    assert skill_label("Navigation") == "Định vị"
    assert skill_label("Tool_Usage") == "Công cụ"
    assert skill_label("Execution") == "Tham số"
    assert skill_label("Configuration") == "Tham số"


def test_footnote_explicit_nodes_and_tool_break():
    rubric, starter, results = _pack("4-1")
    item = next(c for c in rubric["criteria"] if c["id"] == "W41-F01")
    nodes = nodes_for(item)
    assert [n["step_id"] for n in nodes] == ["q1", "q2", "q3"]
    assert nodes[1]["error_feedback"].startswith("Sai công cụ")

    start = grade_path(starter, rubric)
    foot = next(c for c in start["criteria"] if c["criterion_id"] == "W41-F01")
    assert foot["status"] == "fail"
    assert foot["break_skill"] == "Tool_Usage"
    assert foot["break_step"] == "q2"
    assert "Insert Footnote" in foot["message"]
    trace = {n["step_id"]: n["status"] for n in foot["q_matrix"]}
    assert trace["q1"] == "pass"
    assert trace["q2"] == "fail"
    assert trace["q3"] == "skip"

    done = grade_path(results, rubric)
    foot_ok = next(c for c in done["criteria"] if c["criterion_id"] == "W41-F01")
    assert foot_ok["status"] == "pass"
    assert all(n["status"] == "pass" for n in foot_ok["q_matrix"])


def test_toc_update_configure_break():
    rubric, starter, results = _pack("4-2b")
    start = grade_path(starter, rubric)
    toc = next(c for c in start["criteria"] if c["criterion_id"] == "W42B-T01")
    assert toc["status"] == "fail"
    assert toc["q_matrix"]
    assert toc["break_step"] in {"q1", "q2", "q3"}
    first = next(n for n in toc["q_matrix"] if n["status"] == "fail")
    assert first["step_id"] == toc["break_step"]
    assert first["detail"]

    done = grade_path(results, rubric)
    toc_ok = next(c for c in done["criteria"] if c["criterion_id"] == "W42B-T01")
    assert toc_ok["status"] == "pass"
    assert all(n["status"] == "pass" for n in toc_ok["q_matrix"])


def test_synthesized_nodes_when_rubric_omits_matrix():
    item = {
        "id": "X",
        "prompt": "Chèn header",
        "help_steps": ["Click header", "Insert > Header", "Gõ MOS"],
        "predicate": {"type": "header_contains", "text": "MOS"},
        "feedback": {"fail": "Chưa có header."},
    }
    nodes = nodes_for(item)
    assert len(nodes) == 3
    fake = {"ok": True, "document_text": "hello", "header_texts": []}
    result = attach(item, fake, {"status": "fail", "message": "Chưa có header."}, [])
    assert result["break_skill"] == "Tool_Usage"
    assert result["q_matrix"][0]["status"] == "pass"
    assert result["q_matrix"][1]["status"] == "fail"
    assert result["q_matrix"][2]["status"] == "skip"


def test_pass_marks_every_node():
    item = {"id": "X", "help_steps": ["a", "b", "c"], "predicate": {"type": "contains_text"}}
    trace = evaluate(item, {"ok": True, "document_text": "x"}, "pass", [])
    assert [n["status"] for n in trace] == ["pass", "pass", "pass"]
