"""Lớp song ngữ: rubric cũ (chuỗi thuần) và rubric mới (khối {vi,en}) cùng chạy."""
from __future__ import annotations

import json
from pathlib import Path

from app import i18n
from app.grade import grade_path, load_rubric

ROOT = Path(__file__).resolve().parent.parent
RUBRICS = ROOT / "app" / "rubrics"
W11 = RUBRICS / "word-objective-1-1.json"
RESULTS = ROOT / "tests" / "fixtures" / "word-objective-1-1" / "Word_1-1_results.docx"

DISPLAY_FIELDS = ("prompt",)


def test_normalize_lang_accepts_locale_tags():
    for raw, want in [
        ("vi", "vi"), ("VI", "vi"), ("vi-VN", "vi"), ("vi_VN", "vi"),
        ("en", "en"), ("en-US", "en"), ("EN_GB", "en"),
        (None, "vi"), ("", "vi"), ("fr", "vi"), ("  ", "vi"),
    ]:
        assert i18n.normalize_lang(raw) == want, raw


def test_pick_handles_plain_string_and_bundle():
    assert i18n.pick("Chuỗi cũ", "en") == "Chuỗi cũ"      # rubric cũ: dùng nguyên
    bundle = {"vi": "Tiếng Việt", "en": "English"}
    assert i18n.pick(bundle, "en") == "English"
    assert i18n.pick(bundle, "vi") == "Tiếng Việt"
    assert i18n.pick(None) == ""


def test_pick_falls_back_so_ui_is_never_blank():
    assert i18n.pick({"vi": "Chỉ có tiếng Việt", "en": ""}, "en") == "Chỉ có tiếng Việt"
    assert i18n.pick({"en": "Only English"}, "vi") == "Only English"


def test_has_does_not_count_the_fallback():
    assert i18n.has({"vi": "có", "en": ""}, "en") is False
    assert i18n.has({"vi": "có", "en": "yes"}, "en") is True
    assert i18n.has("chuỗi cũ", "vi") is True
    assert i18n.has("chuỗi cũ", "en") is False


def test_pick_list_accepts_all_three_help_step_shapes():
    assert i18n.pick_list(["a", "b"], "en") == ["a", "b"]
    assert i18n.pick_list([{"vi": "a", "en": "A"}], "en") == ["A"]
    assert i18n.pick_list({"vi": ["a"], "en": ["A"]}, "en") == ["A"]
    assert i18n.pick_list(None) == []


def test_localize_flattens_every_display_field():
    rubric = i18n.localize_rubric(load_rubric(W11), "en")
    assert isinstance(rubric["title"], str)
    for crit in rubric["criteria"]:
        for field in DISPLAY_FIELDS:
            assert isinstance(crit[field], str), crit["id"]
        assert all(isinstance(v, str) for v in crit["feedback"].values()), crit["id"]
        assert all(isinstance(s, str) for s in crit["help_steps"]), crit["id"]


def test_localize_leaves_machine_fields_untouched():
    """predicate.text là chuỗi cần tìm trong tài liệu tiếng Anh, không phải chuỗi hiển thị."""
    raw = load_rubric(W11)
    out = i18n.localize_rubric(raw, "en")
    for before, after in zip(raw["criteria"], out["criteria"]):
        assert before["predicate"] == after["predicate"], before["id"]
        assert before["selector"] == after["selector"], before["id"]
        assert before["weight"] == after["weight"]
        assert before.get("evidence_policy") == after.get("evidence_policy")


def test_localize_does_not_mutate_the_source_rubric():
    raw = load_rubric(W11)
    i18n.localize_rubric(raw, "en")
    assert i18n.is_bundle(raw["criteria"][0]["prompt"])
    assert i18n.is_bundle(raw["title"])


def test_word_11_is_fully_translated():
    rubric = load_rubric(W11)
    assert i18n.missing(rubric, "en") == []
    assert i18n.coverage(rubric, "en") == 100.0
    assert i18n.coverage(rubric, "vi") == 100.0


def test_missing_lists_the_untranslated_paths():
    rubric = {
        "title": {"vi": "Tiêu đề", "en": ""},
        "criteria": [{
            "id": "X01",
            "prompt": {"vi": "Câu hỏi", "en": "Question"},
            "feedback": {"pass": {"vi": "Đạt", "en": ""}, "fail": "chỉ có tiếng Việt"},
            "help_steps": [{"vi": "Bước 1", "en": "Step 1"}, {"vi": "Bước 2", "en": ""}],
        }],
    }
    gaps = i18n.missing(rubric, "en")
    assert gaps == ["title", "X01.feedback.pass", "X01.feedback.fail", "X01.help_steps[1]"]
    assert i18n.missing(rubric, "vi") == []


def test_grading_is_identical_in_both_languages():
    rubric = load_rubric(W11)
    vi = grade_path(RESULTS, rubric, lang="vi")
    en = grade_path(RESULTS, rubric, lang="en")
    assert vi["score"] == en["score"] == 62.0
    assert vi["verified"] == en["verified"] == 62.0
    assert vi["pending"] == en["pending"] == 38.0
    assert [c["status"] for c in vi["criteria"]] == [c["status"] for c in en["criteria"]]
    # chỉ phần chữ đổi
    assert vi["criteria"][5]["message"] != en["criteria"][5]["message"]


def test_grading_output_never_leaks_a_bundle():
    """Bất kỳ rubric nào trong repo cũng phải cho ra message/prompt là chuỗi."""
    for path in sorted(RUBRICS.glob("*.json")):
        rubric = load_rubric(path)
        graded = grade_path(None, rubric)
        for item in graded["criteria"]:
            assert isinstance(item["message"], str), (path.name, item["criterion_id"])
            assert isinstance(item["prompt"], str), (path.name, item["criterion_id"])


def test_legacy_rubrics_still_grade_unchanged():
    """59 rubric chưa chuyển vẫn chạy nguyên, không cần sửa một dòng nào."""
    legacy = load_rubric(RUBRICS / "word-objective-2-2.json")
    assert isinstance(legacy["criteria"][0]["prompt"], str), "2-2 đã chuyển song ngữ, đổi mẫu khác"
    folder = ROOT / "tests" / "fixtures" / "word-objective-2-2"
    graded = grade_path(folder / "Word_2-2_results.docx", legacy)
    assert graded["score"] == 100.0


def test_bilingual_rubrics_grade_the_same_as_before_migration():
    """Chuyển song ngữ chỉ đổi chữ, không đổi điểm: file kết quả vẫn phải 100."""
    for name in ("word-objective-1-2",):
        rubric = load_rubric(RUBRICS / f"{name}.json")
        assert i18n.coverage(rubric, "en") == 100.0, name
        tail = name.removeprefix("word-objective-")
        results = ROOT / "tests" / "fixtures" / name / f"Word_{tail}_results.docx"
        for lang in ("vi", "en"):
            assert grade_path(results, rubric, lang=lang)["score"] == 100.0, (name, lang)


def test_every_rubric_file_is_valid_json():
    for path in sorted(RUBRICS.glob("*.json")):
        json.loads(path.read_text(encoding="utf-8"))
