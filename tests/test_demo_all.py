"""Every MOS Word pack reaches 100 with generated demo evidence."""
from __future__ import annotations

from app.demo_all import evidence_from_rubric, format_report, iter_packs, run_all
from app.grade import GRADER_VERSION


def test_demo_all_twenty_word_packs_complete():
    packs = iter_packs()
    assert len(packs) == 20
    rows = run_all()
    by_id = {row["project_id"]: row for row in rows}
    assert set(by_id) == {p["project_id"] for p in packs}
    for row in rows:
        assert row["complete"] is True, row
        assert float(row["demo_verified"]) == 100, row
        assert float(row["demo_pending"]) == 0, row
        assert float(row["starter_verified"]) == 0, row
        assert not row["failed"], row
        assert row["grader_version"] == GRADER_VERSION
    assert by_id["word-objective-1-1"]["action_count"] == 8
    assert by_id["word-objective-1-2"]["action_count"] == 0
    assert by_id["word-objective-1-3"]["action_count"] == 3
    assert by_id["word-objective-1-4"]["action_count"] == 2
    assert float(by_id["word-objective-1-1"]["artifact_verified"]) == 62
    assert float(by_id["word-objective-1-3"]["artifact_verified"]) == 55
    assert float(by_id["word-objective-1-4"]["artifact_verified"]) == 70
    text = format_report(rows)
    assert "20/20 bài đạt 100 sau demo." in text
    assert "word-objective-6-2" in text


def test_evidence_from_rubric_covers_1_1_find_and_goto():
    pack = next(p for p in iter_packs() if p["project_id"] == "word-objective-1-1")
    actions = {e["action"] for e in evidence_from_rubric(pack["rubric"])}
    assert "find" in actions
    assert "goto_page" in actions
    assert "goto_bookmark" in actions
    assert "advanced_find" in actions
