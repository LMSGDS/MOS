"""Word Objective 1.1 — Open XML artifact grader."""
from __future__ import annotations

import io
import zipfile
from pathlib import Path

from app.grade import evaluate_facts, grade_path, load_rubric
from app.word_xml import extract_word_facts, same

ROOT = Path(__file__).resolve().parent.parent
FIXTURES = ROOT / "tests" / "fixtures" / "word-objective-1-1"
STARTER = FIXTURES / "Word_1-1.docx"
RESULTS = FIXTURES / "Word_1-1_results.docx"
RUBRIC = load_rubric(ROOT / "app" / "rubrics" / "word-objective-1-1.json")

W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"


def _docx(document_xml: str) -> Path:
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as z:
        z.writestr(
            "[Content_Types].xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
</Types>""",
        )
        z.writestr(
            "_rels/.rels",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>""",
        )
        z.writestr("word/document.xml", document_xml)
    return buf


def write_docx(path: Path, body: str) -> Path:
    xml = f"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="{W}">
  <w:body>{body}</w:body>
</w:document>"""
    path.write_bytes(_docx(xml).getvalue())
    return path


def test_starter_fails_bookmarks_and_toc_links():
    facts = extract_word_facts(STARTER)
    assert facts["ok"] is True
    assert "SalesManager" not in facts["bookmarks"]
    assert "DesignManager" not in facts["bookmarks"]
    assert facts["internal_hyperlinks"] == []
    graded = grade_path(STARTER, RUBRIC)
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    assert by_id["W11-B01"]["status"] == "fail"
    assert by_id["W11-B02"]["status"] == "fail"
    for hid in ("W11-H01", "W11-H02", "W11-H03", "W11-H04", "W11-H05", "W11-H06"):
        assert by_id[hid]["status"] == "fail"
        assert by_id[hid]["reason_code"] == "hyperlink_missing"
    assert graded["verified"] == 0
    assert graded["pending"] == 38
    assert graded["score"] == 0
    assert graded["complete"] is False


def test_results_earn_62_artifact_and_38_unverified():
    graded = grade_path(RESULTS, RUBRIC)
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    assert by_id["W11-B01"]["status"] == "pass"
    assert by_id["W11-B01"]["earned"] == 10
    assert "Lola Jacobsen" in by_id["W11-B01"]["message"] or by_id["W11-B01"]["reason_code"] == "bookmark_range_matches"
    assert by_id["W11-B02"]["status"] == "pass"
    for hid in ("W11-H01", "W11-H02", "W11-H03", "W11-H04", "W11-H05", "W11-H06"):
        assert by_id[hid]["status"] == "pass", hid
    for sid in ("W11-S01", "W11-S02", "W11-S03", "W11-S04", "W11-S05", "W11-N01", "W11-N02", "W11-N03"):
        assert by_id[sid]["status"] == "unverified"
        assert by_id[sid]["earned"] == 0
        assert "không kết luận học sinh làm sai" in by_id[sid]["message"].casefold()
    assert graded["verified"] == 62
    assert graded["pending"] == 38
    assert graded["score"] == 62
    assert graded["max_score"] == 100
    assert graded["complete"] is False
    assert graded["status"] == "provisional"


def test_bookmark_wrong_person_fails(tmp_path):
    path = write_docx(
        tmp_path / "wrong.docx",
        """
        <w:p>
          <w:bookmarkStart w:id="1" w:name="SalesManager"/>
          <w:r><w:t>Wrong Person</w:t></w:r>
          <w:bookmarkEnd w:id="1"/>
        </w:p>
        <w:p>
          <w:bookmarkStart w:id="2" w:name="DesignManager"/>
          <w:r><w:t>Sarah Jones</w:t></w:r>
          <w:bookmarkEnd w:id="2"/>
        </w:p>
        """,
    )
    graded = grade_path(path, RUBRIC)
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    assert by_id["W11-B01"]["status"] == "fail"
    assert by_id["W11-B01"]["reason_code"] == "bookmark_range_mismatch"
    assert by_id["W11-B02"]["status"] == "pass"


def test_bookmark_split_runs_still_pass(tmp_path):
    path = write_docx(
        tmp_path / "split.docx",
        """
        <w:p>
          <w:bookmarkStart w:id="1" w:name="SalesManager"/>
          <w:r><w:t>Lola </w:t></w:r>
          <w:r><w:t>Jacobsen</w:t></w:r>
          <w:bookmarkEnd w:id="1"/>
        </w:p>
        """,
    )
    facts = extract_word_facts(path)
    assert same(facts["bookmarks"]["SalesManager"]["text"], "Lola Jacobsen")
    graded = grade_path(path, RUBRIC)
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    assert by_id["W11-B01"]["status"] == "pass"


def test_hyperlink_wrong_target_and_external(tmp_path):
    path = write_docx(
        tmp_path / "bad-link.docx",
        """
        <w:p>
          <w:pPr><w:pStyle w:val="Heading1"/></w:pPr>
          <w:bookmarkStart w:id="0" w:name="_Resources"/>
          <w:r><w:t>Resources</w:t></w:r>
          <w:bookmarkEnd w:id="0"/>
        </w:p>
        <w:p>
          <w:hyperlink w:anchor="_Resources">
            <w:r><w:t>New Electronic Favorites</w:t></w:r>
          </w:hyperlink>
        </w:p>
        <w:p>
          <w:hyperlink w:anchor="_Resources">
            <w:r><w:t>Resources</w:t></w:r>
          </w:hyperlink>
        </w:p>
        """,
    )
    graded = grade_path(path, RUBRIC)
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    assert by_id["W11-H01"]["status"] == "fail"
    assert by_id["W11-H01"]["reason_code"] == "hyperlink_wrong_target"
    assert by_id["W11-H06"]["status"] == "pass"


def test_custom_bookmark_name_still_matches_heading(tmp_path):
    path = write_docx(
        tmp_path / "custom.docx",
        """
        <w:p>
          <w:pPr><w:pStyle w:val="Heading1"/></w:pPr>
          <w:bookmarkStart w:id="9" w:name="_student_custom"/>
          <w:r><w:t>Recognition</w:t></w:r>
          <w:bookmarkEnd w:id="9"/>
        </w:p>
        <w:p>
          <w:hyperlink w:anchor="_student_custom">
            <w:r><w:t>Recognition</w:t></w:r>
          </w:hyperlink>
        </w:p>
        """,
    )
    graded = grade_path(path, RUBRIC)
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    assert by_id["W11-H03"]["status"] == "pass"


def test_six_copies_of_same_link_detected(tmp_path):
    links = "".join(
        f'<w:p><w:hyperlink w:anchor="_Resources"><w:r><w:t>{label}</w:t></w:r></w:hyperlink></w:p>'
        for label in (
            "New Electronic Favorites",
            "Why Buy Wingtip Toys?",
            "Recognition",
            "Make It Your Own",
            "Hand-Carved Toys",
            "Resources",
        )
    )
    path = write_docx(
        tmp_path / "dup.docx",
        f"""
        <w:p>
          <w:pPr><w:pStyle w:val="Heading1"/></w:pPr>
          <w:bookmarkStart w:id="0" w:name="_Resources"/>
          <w:r><w:t>Resources</w:t></w:r>
          <w:bookmarkEnd w:id="0"/>
        </w:p>
        {links}
        """,
    )
    graded = grade_path(path, RUBRIC)
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    assert by_id["W11-H06"]["status"] == "pass"
    for hid in ("W11-H01", "W11-H02", "W11-H03", "W11-H04", "W11-H05"):
        assert by_id[hid]["status"] == "fail"
        assert by_id[hid]["reason_code"] == "hyperlink_wrong_target"


def test_broken_file_is_error_not_fail():
    graded = grade_path(Path("/tmp/mos-missing-word-11.docx"), RUBRIC)
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    assert by_id["W11-B01"]["status"] == "error"
    assert by_id["W11-S01"]["status"] == "unverified"
    assert graded["verified"] == 0
    assert graded["pending"] == 38


def test_action_evidence_without_observer_stays_unverified():
    graded = evaluate_facts(extract_word_facts(RESULTS), RUBRIC, evidence=[{"action": "find"}])
    by_id = {c["criterion_id"]: c for c in graded["criteria"]}
    assert by_id["W11-S01"]["status"] == "unverified"
    assert by_id["W11-N01"]["status"] == "unverified"
