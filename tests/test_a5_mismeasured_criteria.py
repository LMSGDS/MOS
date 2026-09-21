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


# ============================================================ W42C-H01, W42A
# Hai ca còn lại của mục 4: predicate hợp lệ nhưng không đo đúng thứ cần đo.

OLD.update({
    "W42C-H01": {"type": "contains_text", "text": "References"},
    "W42A-T01": {"type": "field_contains", "text": "TOC"},
    "W42A-E01": {"type": "style_used", "style": "TOC1", "min": 1},
})


def _styled_doc(paras: list[tuple[str, str]], fields: list[str] | None = None) -> Path:
    """paras = [(style, text)]; fields = danh sách instrText."""
    body = "".join(
        (f'<w:p><w:pPr><w:pStyle w:val="{style}"/></w:pPr>' if style else "<w:p>")
        + f"<w:r><w:t>{text}</w:t></w:r></w:p>"
        for style, text in paras
    )
    for instr in fields or []:
        body += f'<w:p><w:r><w:instrText>{instr}</w:instrText></w:r></w:p>'
    return _docx({"word/document.xml": f"<w:document {NS}><w:body>{body}</w:body></w:document>"})


# ------------------------------------------------------------ W42C-H01

def test_w42ch01_old_predicate_passes_the_word_anywhere_in_the_body():
    """References là tên một tab ribbon và là từ rất thường gặp."""
    prose = _styled_doc([
        ("Heading1", "About the Brothers Grimm"),
        ("", "See the References tab on the ribbon for more details."),
    ])
    crit = _criterion("word-objective-4-2c", "W42C-H01")
    assert _status(_with_old_predicate(crit), prose) == "pass"   # 30 điểm cho không
    assert _status(crit, prose) == "fail"


def test_w42ch01_new_predicate_needs_a_real_heading():
    titled = _styled_doc([("Heading1", "About the Brothers Grimm"), ("Heading1", "References")])
    assert _status(_criterion("word-objective-4-2c", "W42C-H01"), titled) == "pass"


def test_w42ch01_does_not_duplicate_w42cb01():
    """Đề xuất ban đầu dùng lại style_used Bibliography — sẽ trùng predicate
    với W42C-B01 và bị chính luật 2 của rubric_tool bắt."""
    import json as _json
    rubric = _json.loads((RUBRICS / "word-objective-4-2c.json").read_text(encoding="utf-8"))
    preds = [_json.dumps(c["predicate"], sort_keys=True) for c in rubric["criteria"]]
    assert len(preds) == len(set(preds))


def test_w42c_answer_file_has_no_bibliography_field():
    """Chốt lý do không dùng field_contains BIBLIOGRAPHY: field đó không tồn tại."""
    facts = extract_word_facts(FIXTURES / "word-objective-4-2c" / "Word_4-2c_results.docx")
    assert facts["fields"], "đáp án phải có field CITATION"
    assert all("BIBLIOGRAPHY" not in f.upper() for f in facts["fields"])


# --------------------------------------------------------------- W42A

def test_w42at01_old_predicate_matches_a_pageref_field():
    """field_contains "TOC" viết hoa hai phía nên khớp cả PAGEREF _Toc…"""
    cross_ref_only = _styled_doc([("Heading1", "General Administration")],
                                 fields=['PAGEREF _Toc26916297 \\h'])
    crit = _criterion("word-objective-4-2a", "W42A-T01")
    assert _status(_with_old_predicate(crit), cross_ref_only) == "pass"   # false positive
    assert _status(crit, cross_ref_only) == "fail"


def test_w42at01_new_predicate_accepts_a_real_toc_field():
    real = _styled_doc([("TOCHeading", "Contents")], fields=['TOC \\o "1-3" \\h \\z \\u'])
    assert _status(_criterion("word-objective-4-2a", "W42A-T01"), real) == "pass"


def test_w42ae01_old_predicate_passes_a_single_level_toc():
    """TOC1 có mặt ở MỌI mục lục, kể cả loại chỉ 1 cấp — không đo được
    yêu cầu "cấp 1-3" mà chính đề bài nêu."""
    one_level = _styled_doc(
        [("TOCHeading", "Contents"), ("TOC1", "General Administration2"), ("TOC1", "Accounting5")],
        fields=['TOC \\o "1-1" \\h \\z \\u'],
    )
    crit = _criterion("word-objective-4-2a", "W42A-E01")
    assert _status(_with_old_predicate(crit), one_level) == "pass"   # 30 điểm cho không
    assert _status(crit, one_level) == "fail"


def _grade_42a(path: Path) -> dict[str, str]:
    rubric = load_rubric(RUBRICS / "word-objective-4-2a.json")
    from app.grade import evaluate_facts
    graded = evaluate_facts(extract_word_facts(path), rubric)
    return {c["criterion_id"]: c["status"] for c in graded["criteria"]}


def test_w42a_three_criteria_can_now_disagree():
    """Trước đây cả ba chỉ cùng đỗ hoặc cùng trượt trên đường làm đúng.
    Giờ mỗi lựa chọn sai trong hộp thoại Insert TOC cho một điểm khác nhau."""
    hand_typed = _styled_doc([("", "Contents"), ("", "General Administration....2")])
    assert _grade_42a(hand_typed) == {"W42A-T01": "fail", "W42A-H01": "fail", "W42A-E01": "fail"}

    # Manual Table: có style TOC nhưng KHÔNG có trường TOC.
    manual = _styled_doc([("TOCHeading", "Contents"), ("TOC1", "Type chapter title (level 1)"),
                          ("TOC2", "Type chapter title (level 2)"), ("TOC3", "Type chapter title (level 3)")])
    assert _grade_42a(manual) == {"W42A-T01": "fail", "W42A-H01": "pass", "W42A-E01": "pass"}

    # Custom TOC chỉ 1 cấp: có trường và heading, nhưng không phủ cấp 3.
    one_level = _styled_doc([("TOCHeading", "Contents"), ("TOC1", "General Administration2")],
                            fields=['TOC \\o "1-1" \\h \\z \\u'])
    assert _grade_42a(one_level) == {"W42A-T01": "pass", "W42A-H01": "pass", "W42A-E01": "fail"}

    # Custom TOC không kèm heading.
    no_heading = _styled_doc([("TOC1", "General Administration2"), ("TOC3", "Office2")],
                             fields=['TOC \\o "1-3" \\h \\z \\u'])
    assert _grade_42a(no_heading) == {"W42A-T01": "pass", "W42A-H01": "fail", "W42A-E01": "pass"}


def test_w42a_and_w42c_answer_files_still_score_100():
    for n in ("4-2a", "4-2c"):
        rubric = load_rubric(RUBRICS / f"word-objective-{n}.json")
        folder = FIXTURES / f"word-objective-{n}"
        assert grade_path(folder / f"Word_{n}.docx", rubric)["score"] == 0.0, n
        assert grade_path(folder / f"Word_{n}_results.docx", rubric)["score"] == 100.0, n


# ------------------------------------------------------------ W42C-C01

OLD["W42C-C01"] = {"type": "contains_text", "text": "Grimm, Jacob, and Wilhelm Grimm"}


def test_w42cc01_old_predicate_passes_the_string_typed_into_ordinary_prose():
    """contains_text quét cả thân bài: gõ tay vào đoạn văn thường cũng đỗ,
    mà kỹ năng cần đo là để Word dựng lại danh mục theo kiểu citation mới."""
    typed = _styled_doc([
        ("Heading1", "References"),
        ("", "Grimm, Jacob, and Wilhelm Grimm. 2013. The Complete Grimm's Fairy Tales."),
    ])
    crit = _criterion("word-objective-4-2c", "W42C-C01")
    assert _status(_with_old_predicate(crit), typed) == "pass"   # 30 điểm cho không
    assert _status(crit, typed) == "fail"


def test_w42cc01_new_predicate_needs_the_string_inside_a_bibliography_paragraph():
    generated = _styled_doc([
        ("Heading1", "References"),
        ("Bibliography", "Grimm, Jacob, and Wilhelm Grimm. 2013. The Complete Grimm's Fairy Tales."),
    ])
    assert _status(_criterion("word-objective-4-2c", "W42C-C01"), generated) == "pass"


def test_w42cc01_rejects_a_bibliography_still_in_the_old_citation_style():
    """Có danh mục nhưng chưa đổi kiểu — đúng ca cần phân biệt."""
    old_style = _styled_doc([
        ("Heading1", "References"),
        ("Bibliography", "Grimm, J., and W. Grimm. 2013. The Complete Grimm's Fairy Tales."),
    ])
    assert _status(_criterion("word-objective-4-2c", "W42C-C01"), old_style) == "fail"


def test_styled_text_needs_both_style_and_text():
    doc = _styled_doc([("Bibliography", "Grimm, Jacob, and Wilhelm Grimm. 2013.")])
    base = {"id": "X", "kind": "artifact", "weight": 10, "feedback": {"fail": "f"}}
    assert _status({**base, "predicate": {"type": "styled_text", "text": "Grimm, Jacob"}}, doc) == "fail"
    assert _status({**base, "predicate": {"type": "styled_text", "style": "Bibliography"}}, doc) == "fail"
    assert _status({**base, "predicate": {"type": "styled_text", "style": "Bibliography",
                                          "text": "Grimm, Jacob"}}, doc) == "pass"


def test_styled_text_honours_min():
    doc = _styled_doc([("Bibliography", "Grimm, Jacob. 2013."), ("Bibliography", "Grimm, Jacob. 2019.")])
    base = {"id": "X", "kind": "artifact", "weight": 10, "feedback": {"fail": "f"}}
    pred = {"type": "styled_text", "style": "Bibliography", "text": "Grimm, Jacob"}
    assert _status({**base, "predicate": {**pred, "min": 2}}, doc) == "pass"
    assert _status({**base, "predicate": {**pred, "min": 3}}, doc) == "fail"


def test_word_42c_has_no_remaining_rule_errors():
    """4-2c nay sạch hoàn toàn — không còn predicate yếu nào."""
    import json as _json
    from scripts.rubric_tool import check_rubric
    rubric = _json.loads((RUBRICS / "word-objective-4-2c.json").read_text(encoding="utf-8"))
    errors, _warns = check_rubric(rubric, "word-objective-4-2c")
    assert errors == [], errors
