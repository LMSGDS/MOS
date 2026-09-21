"""A5 — bốn tiêu chí đo nhầm thứ cần đo.

Mỗi test dựng một tài liệu "làm SAI nhưng predicate cũ vẫn cho đỗ", rồi chứng
minh predicate mới bắt được. Đây là bằng chứng cho việc sửa, không phải mô tả.
"""
from __future__ import annotations

import io
import json
import zipfile
from pathlib import Path

from app.grade import evaluate_facts, grade_path, load_rubric
from app.word_xml import extract_word_facts

ROOT = Path(__file__).resolve().parent.parent
RUBRICS = ROOT / "app" / "rubrics"
FIXTURES = ROOT / "tests" / "fixtures"

NS = (
    'xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" '
    'xmlns:w14="http://schemas.microsoft.com/office/word/2010/wordml" '
    'xmlns:w15="http://schemas.microsoft.com/office/word/2012/wordml"'
)


def _docx(parts: dict[str, str]) -> Path:
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as z:
        z.writestr("[Content_Types].xml", "<Types/>")
        for name, body in parts.items():
            z.writestr(name, body)
    out = Path("/tmp") / f"a5-{abs(hash(json.dumps(parts, sort_keys=True)))}.docx"
    out.write_bytes(buf.getvalue())
    return out


def _criterion(project: str, cid: str) -> dict:
    rubric = load_rubric(RUBRICS / f"{project}.json")
    return next(c for c in rubric["criteria"] if c["id"] == cid)


def _status(criterion: dict, path: Path) -> str:
    facts = extract_word_facts(path)
    graded = evaluate_facts(facts, {"criteria": [criterion], "max_score": criterion["weight"]})
    return graded["criteria"][0]["status"]


OLD = {
    "W32-D01": {"type": "table_has_text", "text": "Customer"},
    "W33-R01": {"type": "style_used", "style": "ListParagraph", "min": 20},
    "W61-A01": {"type": "comment_author", "author": "Joan Lambert"},
    "W62-L01": {"type": "document_protection"},
}


def _with_old_predicate(criterion: dict) -> dict:
    return {**criterion, "predicate": OLD[criterion["id"]]}


# ------------------------------------------------------------ W32-D01

def _table_doc(header_rows: list[list[str]], data: list[list[str]]) -> Path:
    def row(cells, header=False):
        trpr = "<w:trPr><w:tblHeader/></w:trPr>" if header else ""
        tcs = "".join(f"<w:tc><w:p><w:r><w:t>{c}</w:t></w:r></w:p></w:tc>" for c in cells)
        return f"<w:tr>{trpr}{tcs}</w:tr>"

    body = "".join(row(r, header=True) for r in header_rows) + "".join(row(r) for r in data)
    return _docx({"word/document.xml": f"<w:document {NS}><w:body><w:tbl>{body}</w:tbl></w:body></w:document>"})


def test_w32d01_old_predicate_passes_a_document_that_never_deleted_the_id_column():
    """Gộp ô tiêu đề (nhiệm vụ M01) sinh ra chữ Customer. Cột ID vẫn nguyên."""
    wrong = _table_doc([["Customer", "Appointment"], ["ID", "LastName", "State"]],
                       [["1", "Bedecs", "WA"]])
    crit = _criterion("word-objective-3-2", "W32-D01")
    assert _status(_with_old_predicate(crit), wrong) == "pass"   # 15 điểm cho không
    assert _status(crit, wrong) == "fail"                        # luật mới bắt được


def test_w32d01_new_predicate_does_not_trip_on_idaho_in_a_state_cell():
    """ID cũng là mã bang Idaho — quét cả bảng sẽ báo sai."""
    right = _table_doc([["Customer", "Appointment"], ["LastName", "FirstName", "State"]],
                       [["Xie", "Ming-Yang", "ID"]])
    assert _status(_criterion("word-objective-3-2", "W32-D01"), right) == "pass"


# ------------------------------------------------------------ W33-R01

def _list_doc(blocks: list[tuple[str, str, int]]) -> Path:
    """blocks = [(text, numFmt, numId)] — mỗi numId là một danh sách độc lập."""
    paras = "".join(
        f'<w:p><w:pPr><w:pStyle w:val="ListParagraph"/>'
        f'<w:numPr><w:ilvl w:val="0"/><w:numId w:val="{nid}"/></w:numPr></w:pPr>'
        f"<w:r><w:t>{text}</w:t></w:r></w:p>"
        for text, _fmt, nid in blocks
    )
    nums, abstracts = "", ""
    for nid, fmt in sorted({(nid, fmt) for _t, fmt, nid in blocks}):
        abstracts += f'<w:abstractNum w:abstractNumId="{nid}"><w:lvl w:ilvl="0"><w:numFmt w:val="{fmt}"/></w:lvl></w:abstractNum>'
        nums += f'<w:num w:numId="{nid}"><w:abstractNumId w:val="{nid}"/></w:num>'
    return _docx({
        "word/document.xml": f"<w:document {NS}><w:body>{paras}</w:body></w:document>",
        "word/numbering.xml": f"<w:numbering {NS}>{abstracts}{nums}</w:numbering>",
    })


def test_w33r01_old_predicate_passes_one_long_list_with_no_restart_at_all():
    """Đếm đoạn ListParagraph chỉ đo ĐỘ DÀI: 24 mục một mạch cũng đủ 20."""
    one_list = _list_doc([(f"The Journey {i}", "decimal", 1) for i in range(24)])
    crit = _criterion("word-objective-3-3", "W33-R01")
    assert _status(_with_old_predicate(crit), one_list) == "pass"   # 20 điểm cho không
    assert _status(crit, one_list) == "fail"


def test_w33r01_new_predicate_needs_three_independent_lists():
    two = _list_doc([("The Journey", "upperLetter", 12), ("The Journey", "decimal", 14)])
    three = _list_doc([("The Journey", "upperLetter", 12), ("The Journey", "decimal", 14),
                       ("The Journey", "bullet", 17)])
    crit = _criterion("word-objective-3-3", "W33-R01")
    assert _status(crit, two) == "fail"
    assert _status(crit, three) == "pass"


# ------------------------------------------------------------ W61-A01

def _comment_doc(entries: list[tuple[str, str, str, str]]) -> Path:
    """entries = [(author, text, paraId, paraIdParent)]"""
    comments = "".join(
        f'<w:comment w:author="{a}"><w:p w14:paraId="{pid}"><w:r><w:t>{t}</w:t></w:r></w:p></w:comment>'
        for a, t, pid, _p in entries
    )
    ext = "".join(
        f'<w15:commentEx w15:paraId="{pid}" w15:done="0"'
        + (f' w15:paraIdParent="{parent}"' if parent else "")
        + "/>"
        for _a, _t, pid, parent in entries
    )
    return _docx({
        "word/document.xml": f"<w:document {NS}><w:body><w:p/></w:body></w:document>",
        "word/comments.xml": f"<w:comments {NS}>{comments}</w:comments>",
        "word/commentsExtended.xml": f"<w15:commentsEx {NS}>{ext}</w15:commentsEx>",
    })


def test_w61a01_old_predicate_fails_a_student_who_replied_correctly():
    """Word gắn tên tài khoản của chính học sinh, không bao giờ là Joan Lambert."""
    student = _comment_doc([
        ("Mike Nash", "But slow", "AA01", ""),
        ("Nguyen Van An", "Please provide details", "AA02", "AA01"),   # reply đúng
    ])
    crit = _criterion("word-objective-6-1", "W61-A01")
    assert _status(_with_old_predicate(crit), student) == "fail"   # làm đúng vẫn 0 điểm
    assert _status(crit, student) == "pass"


def test_w61a01_new_predicate_rejects_a_standalone_comment():
    """Tạo comment mới rời không phải là Reply."""
    standalone = _comment_doc([
        ("Mike Nash", "But slow", "AA01", ""),
        ("Nguyen Van An", "Please provide details", "AA02", ""),
    ])
    assert _status(_criterion("word-objective-6-1", "W61-A01"), standalone) == "fail"


def test_w61a01_old_predicate_passes_the_sample_file_without_any_reply():
    """Ngược lại: file có sẵn comment của Joan Lambert thì đỗ dù chưa reply gì."""
    sample = _comment_doc([("Joan Lambert", "bất kỳ", "AA01", "")])
    crit = _criterion("word-objective-6-1", "W61-A01")
    assert _status(_with_old_predicate(crit), sample) == "pass"
    assert _status(crit, sample) == "fail"


# ------------------------------------------------------------ W62-L01

def _protection_doc(edit: str | None, enforcement: str = "0") -> Path:
    node = "" if edit is None else f'<w:documentProtection w:edit="{edit}" w:enforcement="{enforcement}"/>'
    return _docx({
        "word/document.xml": f"<w:document {NS}><w:body><w:p/></w:body></w:document>",
        "word/settings.xml": f"<w:settings {NS}>{node}</w:settings>",
    })


def test_w62l01_old_predicate_passes_any_restrict_editing_mode():
    crit = _criterion("word-objective-6-2", "W62-L01")
    for mode in ("readOnly", "comments", "forms"):
        wrong = _protection_doc(mode)
        assert _status(_with_old_predicate(crit), wrong) == "pass", mode   # 15 điểm cho không
        assert _status(crit, wrong) == "fail", mode


def test_w62l01_new_predicate_accepts_lock_tracking_without_a_password():
    """Word 2019 ghi enforcement="0" khi Lock Tracking không đặt mật khẩu —
    đòi enforcement=1 sẽ đánh trượt chính file đáp án của Study Guide."""
    assert _status(_criterion("word-objective-6-2", "W62-L01"), _protection_doc("trackedChanges", "0")) == "pass"
    assert _status(_criterion("word-objective-6-2", "W62-L01"), _protection_doc(None)) == "fail"


def test_w62l01_enforced_flag_is_available_but_unused_by_the_rubric():
    crit = dict(_criterion("word-objective-6-2", "W62-L01"))
    crit["predicate"] = {"type": "document_protection", "edit": "trackedChanges", "enforced": True}
    assert _status(crit, _protection_doc("trackedChanges", "0")) == "fail"
    assert _status(crit, _protection_doc("trackedChanges", "1")) == "pass"


# ------------------------------------- đáp án chính thức vẫn phải đạt 100

def test_the_four_study_guide_answer_files_still_score_100():
    for n in ("3-2", "3-3", "6-1", "6-2"):
        rubric = load_rubric(RUBRICS / f"word-objective-{n}.json")
        folder = FIXTURES / f"word-objective-{n}"
        assert grade_path(folder / f"Word_{n}.docx", rubric)["score"] == 0.0, n
        assert grade_path(folder / f"Word_{n}_results.docx", rubric)["score"] == 100.0, n


def test_legacy_document_protection_predicate_still_works_without_edit():
    """Rubric khác vẫn dùng document_protection trần — không được đổi hành vi."""
    crit = {"id": "X", "kind": "artifact", "weight": 10,
            "predicate": {"type": "document_protection"}, "feedback": {"fail": "f"}}
    assert _status(crit, _protection_doc("readOnly")) == "pass"
    assert _status(crit, _protection_doc(None)) == "fail"
