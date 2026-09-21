"""Bộ luật chất lượng rubric — cổng chặn của CI."""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

from app import i18n  # noqa: E402
from scripts.rubric_tool import (  # noqa: E402
    PRODUCIBLE_ACTIONS,
    WEAK_PREDICATES,
    WEAK_WEIGHT_LIMIT,
    check_rubric,
    migrate_rubric,
)

RUBRICS = ROOT / "app" / "rubrics"
PROBE = ROOT / "desktop" / "MosDock" / "WordActionProbe.cs"


def _rubric(criteria: list[dict], **extra) -> dict:
    return {"project_id": "t", "max_score": 100, "criteria": criteria, **extra}


def _crit(cid: str, weight: int, predicate: dict, **extra) -> dict:
    base = {
        "id": cid,
        "kind": "artifact",
        "weight": weight,
        "predicate": predicate,
        "feedback": {"fail": f"fail-{cid}"},
    }
    base.update(extra)
    return base


# ---------------------------------------------------------------- luật 1–6

def test_rule1_weight_must_match_max_score():
    errors, _ = check_rubric(_rubric([_crit("A", 60, {"type": "x"})]), "t")
    assert any("tổng weight = 60" in e for e in errors)


def test_rule2_duplicate_predicate_is_an_error():
    pred = {"type": "layout_named", "name": "My Slides"}
    errors, _ = check_rubric(_rubric([_crit("A", 50, pred), _crit("B", 50, dict(pred))]), "t")
    assert any("B trùng predicate với A" in e for e in errors)


def test_rule3_weak_predicate_over_the_limit_is_an_error():
    errors, _ = check_rubric(
        _rubric([_crit("A", 50, {"type": "contains_text", "text": "Item 2"}),
                 _crit("B", 50, {"type": "sheet_named", "name": "S"})]), "t")
    assert any("A dùng contains_text cho 50 điểm" in e for e in errors)


def test_rule3_weak_predicate_at_the_limit_is_allowed():
    errors, _ = check_rubric(
        _rubric([_crit("A", WEAK_WEIGHT_LIMIT, {"type": "contains_text", "text": "x"}),
                 _crit("B", 80, {"type": "other"})]), "t")
    assert not [e for e in errors if "contains_text" in e]


def test_rule3_weak_ok_downgrades_the_error_to_a_warning():
    """Chèn ký hiệu ® — kết quả CHÍNH LÀ chuỗi, nên miễn trừ là hợp lý."""
    crit = _crit("A", 40, {"type": "contains_text", "text": "Microsoft®"},
                 weak_ok="mục tiêu objective là ký tự ® xuất hiện đúng chỗ")
    errors, warns = check_rubric(_rubric([crit, _crit("B", 60, {"type": "other"})]), "t")
    assert not [e for e in errors if "contains_text" in e]
    assert any("A miễn trừ predicate yếu" in w for w in warns)


def test_rule3_blank_weak_ok_does_not_excuse_anything():
    crit = _crit("A", 40, {"type": "contains_text", "text": "x"}, weak_ok="   ")
    errors, _ = check_rubric(_rubric([crit, _crit("B", 60, {"type": "other"})]), "t")
    assert any("A dùng contains_text" in e for e in errors)


def test_rule4_rubric_without_criteria_would_fall_back_to_legacy_text():
    errors, _ = check_rubric(_rubric([]), "t")
    assert any("legacy-text" in e for e in errors)


def test_rule5_shared_fail_feedback_is_a_warning():
    a = _crit("A", 50, {"type": "p1"}); a["feedback"] = {"fail": "Chưa khớp file kết quả Study Guide."}
    b = _crit("B", 50, {"type": "p2"}); b["feedback"] = {"fail": "Chưa khớp file kết quả Study Guide."}
    _, warns = check_rubric(_rubric([a, b]), "t")
    assert any("feedback.fail dùng chung cho A, B" in w for w in warns)


def test_rule6_missing_kc_is_a_warning():
    _, warns = check_rubric(_rubric([_crit("A", 100, {"type": "p"})]), "t")
    assert any("chưa gắn kc[]: A" in w for w in warns)


# ------------------------------------------------------------- luật 7 và 8

def test_rule7_unproducible_action_is_a_dead_point_error():
    crit = _crit("A", 45, {"type": "goto_bookmark"}, kind="action_sequence",
                 evidence_policy="required_method")
    errors, _ = check_rubric(_rubric([crit, _crit("B", 55, {"type": "other"})]), "t")
    assert any("«goto_bookmark»" in e and "vĩnh viễn unverified" in e for e in errors)
    assert any("tổng 45/100 điểm không có nguồn bằng chứng nào" in e for e in errors)


def test_rule7_producible_action_is_clean():
    crit = _crit("A", 45, {"type": "search_query", "query": "to"}, kind="action_sequence",
                 evidence_policy="required_method")
    errors, _ = check_rubric(_rubric([crit, _crit("B", 55, {"type": "other"})]), "t")
    assert not [e for e in errors if "không có nguồn bằng chứng" in e]


def test_rule8_threshold_only_difference_is_a_warning():
    a = _crit("A", 50, {"type": "drawing_text", "min": 1})
    b = _crit("B", 50, {"type": "drawing_text", "min": 2})
    _, warns = check_rubric(_rubric([a, b]), "t")
    assert any("A và B chỉ khác ngưỡng" in w for w in warns)


def test_rule8_does_not_fire_on_genuinely_different_predicates():
    a = _crit("A", 50, {"type": "drawing_text", "text": "alpha", "min": 1})
    b = _crit("B", 50, {"type": "drawing_text", "text": "beta", "min": 2})
    _, warns = check_rubric(_rubric([a, b]), "t")
    assert not [w for w in warns if "chỉ khác ngưỡng" in w]


# ----------------------------------------- luật 7 phải khớp mã C# thật

def test_producible_actions_match_what_the_probe_actually_emits():
    """Luật 7 chỉ đúng khi danh sách này khớp WordActionProbe.cs.

    Nếu ai đó thêm bộ ghi nhận mới bên C# mà quên cập nhật PRODUCIBLE_ACTIONS,
    test này gãy — tránh để rubric hợp lệ bị báo là điểm chết.
    """
    emitted = set(re.findall(r'ActionEvidence\.Add\("([a-z_]+)"', PROBE.read_text(encoding="utf-8")))
    assert emitted, "không đọc được WordActionProbe.cs"
    assert emitted <= PRODUCIBLE_ACTIONS, f"probe sinh thêm {emitted - PRODUCIBLE_ACTIONS}"


def test_demo_only_actions_are_not_treated_as_producible():
    """WordActionDemo.cs là bộ sinh demo, KHÔNG phải nguồn bằng chứng của học sinh."""
    demo = ROOT / "desktop" / "MosDock" / "WordActionDemo.cs"
    emitted = set(re.findall(r'ActionEvidence\.Add\(\s*"([a-z_]+)"', demo.read_text(encoding="utf-8")))
    for action in ("goto_graphic", "goto_page", "goto_bookmark", "inspect_document"):
        assert action in emitted, action
        assert action not in PRODUCIBLE_ACTIONS, action


# ---------------------------------------------------------------- migrate

def test_migrate_wraps_plain_strings_and_adds_kc():
    src = {
        "title": "Tiêu đề",
        "max_score": 100,
        "criteria": [{
            "id": "A", "kind": "artifact", "weight": 100,
            "prompt": "Câu hỏi",
            "feedback": {"pass": "Đạt", "fail": "Chưa đạt"},
            "help_steps": ["Bước 1", "Bước 2"],
        }],
    }
    out = migrate_rubric(src)
    assert out["langs"] == ["vi", "en"] and out["default_lang"] == "vi"
    assert out["title"] == {"vi": "Tiêu đề", "en": ""}
    crit = out["criteria"][0]
    assert crit["kc"] == []
    assert crit["prompt"] == {"vi": "Câu hỏi", "en": ""}
    assert crit["feedback"]["fail"] == {"vi": "Chưa đạt", "en": ""}
    assert crit["help_steps"][0] == {"vi": "Bước 1", "en": ""}
    # dữ liệu máy đọc không bị đụng
    assert crit["weight"] == 100 and crit["kind"] == "artifact"


def test_migrate_is_idempotent_and_keeps_existing_translations():
    src = json.loads((RUBRICS / "word-objective-1-1.json").read_text(encoding="utf-8"))
    once = migrate_rubric(src)
    twice = migrate_rubric(once)
    assert once == twice
    assert i18n.coverage(once, "en") == 100.0
    assert once["criteria"][0]["prompt"]["en"].startswith("From the Navigation pane")
    assert once["criteria"][0]["kc"] == ["KC-1.1.1"]


# ------------------------------------------------ trạng thái thật của repo

def test_word_12_is_fully_clean():
    rubric = json.loads((RUBRICS / "word-objective-1-2.json").read_text(encoding="utf-8"))
    errors, warns = check_rubric(rubric, "word-objective-1-2")
    assert errors == [] and warns == [], (errors, warns)
    assert i18n.coverage(rubric, "en") == 100.0


def test_word_11_is_clean_except_the_known_go_to_dead_points():
    """1-1 là bản mẫu song ngữ, nhưng 18 điểm Go To vẫn chưa có nguồn bằng chứng.

    Khi B4 (chuyển 3 nhiệm vụ Go To sang artifact) xong thì test này phải đổi
    thành errors == [].
    """
    rubric = json.loads((RUBRICS / "word-objective-1-1.json").read_text(encoding="utf-8"))
    errors, warns = check_rubric(rubric, "word-objective-1-1")
    assert warns == [], warns
    assert i18n.coverage(rubric, "en") == 100.0
    assert any("tổng 18/100 điểm không có nguồn bằng chứng nào" in e for e in errors)
    assert {"goto_graphic", "goto_page", "goto_bookmark"} == {
        m for e in errors for m in ("goto_graphic", "goto_page", "goto_bookmark") if m in e
    }


def test_every_rubric_declares_a_total_weight_of_max_score():
    """Điểm sáng của repo: không file nào lệch trọng số. Giữ nguyên như vậy."""
    off = []
    for path in sorted(RUBRICS.glob("*.json")):
        rubric = json.loads(path.read_text(encoding="utf-8"))
        total = sum(int(c.get("weight") or 0) for c in rubric.get("criteria") or [])
        if total != int(rubric.get("max_score") or 100):
            off.append(f"{path.name}: {total}")
    assert off == [], off


def test_known_real_defects_are_still_detected():
    """Chốt lại các lỗi thật đã tìm ra — nếu ai sửa rubric thì test này phải đổi theo."""
    excel = json.loads((RUBRICS / "excel-objective-2-1.json").read_text(encoding="utf-8"))
    assert any("E21-F01 dùng contains_text cho 50 điểm" in e for e in check_rubric(excel, "e")[0])

    ppt = json.loads((RUBRICS / "powerpoint-objective-1-1.json").read_text(encoding="utf-8"))
    errors = check_rubric(ppt, "p")[0]
    assert any("P11-L02 trùng predicate với P11-L01" in e for e in errors)
    assert any("P11-L03 trùng predicate với P11-L01" in e for e in errors)

    w13 = json.loads((RUBRICS / "word-objective-1-3.json").read_text(encoding="utf-8"))
    assert any("tổng 45/100 điểm không có nguồn bằng chứng nào" in e for e in check_rubric(w13, "w")[0])

    w14 = json.loads((RUBRICS / "word-objective-1-4.json").read_text(encoding="utf-8"))
    assert any("tổng 30/100 điểm không có nguồn bằng chứng nào" in e for e in check_rubric(w14, "w")[0])


# ------------------------------------------------------- bánh cóc baseline

def test_baseline_file_covers_every_current_error():
    """rubric-baseline.json phải khớp trạng thái repo, nếu không CI sẽ gãy oan."""
    baseline = json.loads((ROOT / "rubric-baseline.json").read_text(encoding="utf-8"))
    known = baseline["known_errors"]
    stale = []
    fresh = []
    for path in sorted(RUBRICS.glob("*.json")):
        errors = check_rubric(json.loads(path.read_text(encoding="utf-8")), path.name)[0]
        recorded = known.get(path.name, [])
        fresh += [(path.name, e) for e in errors if e not in recorded]
        stale += [(path.name, e) for e in recorded if e not in errors]
    assert fresh == [], f"lỗi mới chưa chốt — chạy: python3 scripts/rubric_tool.py baseline app/rubrics\n{fresh}"
    assert stale == [], f"lỗi đã sửa nhưng baseline chưa hạ — chạy lại lệnh baseline\n{stale}"


def test_baseline_records_the_expected_debt():
    baseline = json.loads((ROOT / "rubric-baseline.json").read_text(encoding="utf-8"))
    total = sum(len(v) for v in baseline["known_errors"].values())
    # 52 lúc chốt lần đầu; W42C-H01 hạ còn 51, W42C-C01 hạ còn 50. Con số chỉ
    # được PHÉP giảm — tăng nghĩa là có rubric mới yếu lọt vào.
    assert total == 50, f"số lỗi tồn đọng đổi từ 50 thành {total} — cập nhật kỳ vọng nếu là chủ ý"


def test_load_baseline_tolerates_a_missing_or_broken_file(tmp_path):
    from scripts.rubric_tool import load_baseline
    assert load_baseline(None) == {}
    assert load_baseline(tmp_path / "khong-co.json") == {}
    bad = tmp_path / "bad.json"
    bad.write_text("{ khong phai json", encoding="utf-8")
    assert load_baseline(bad) == {}
