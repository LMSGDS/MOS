"""Thiết kế web UDL: token màu, tiếp cận, song ngữ, Không gian Khám phá, trần điểm."""
from __future__ import annotations

import io
import re
import zipfile
from pathlib import Path

from fastapi.testclient import TestClient

from app.main import app

ROOT = Path(__file__).resolve().parent.parent
TPL = ROOT / "app" / "templates"
STUDENT = {"username": "hocsinh", "password": "Mos@Gds2026"}


def _student_client() -> TestClient:
    c = TestClient(app)
    r = c.post("/api/dang-nhap", json=STUDENT)
    assert r.status_code == 200 and r.json()["ok"]
    return c


# ------------------------------------------------------------- W1 / W2 / W7
def test_tokens_css_loaded_after_mos_css_and_fixes_a11y():
    base = (TPL / "base.html").read_text(encoding="utf-8")
    assert base.index("/static/mos.css") < base.index("/static/tokens.css")
    assert "family=Calibri" not in base  # Calibri không có trên Google Fonts
    assert '<html lang="{{ lang' in base

    tokens = (ROOT / "app" / "static" / "tokens.css").read_text(encoding="utf-8")
    assert ".ic-app-header__menu-list-item:focus-visible" in tokens and "outline: 3px solid" in tokens
    assert "prefers-reduced-motion" in tokens
    assert "prefers-color-scheme: dark" in tokens and '[data-contrast="high"]' in tokens
    assert ".coach" in tokens and ".lang-switch" in tokens and ".skill-node" in tokens
    sub = tokens.split(".ic-app-header__menu-list-item-sub", 1)[1].split("}", 1)[0]
    assert "opacity: 1" in sub


def test_adaptive_path_uses_coach_not_error_red():
    for name in ("progress.html", "portal.html"):
        html = (TPL / name).read_text(encoding="utf-8")
        assert "heat-red" not in html, name
        assert 'class="coach"' in html and "coach__why" in html, name


def test_word_frame_does_not_embed_itself():
    # 6.1 trong bản đánh giá: kiểm chứng — word.html không có iframe nào.
    assert "<iframe" not in (TPL / "word.html").read_text(encoding="utf-8")
    kulkul = (TPL / "kulkul.html").read_text(encoding="utf-8")
    assert 'src="/khung/office' in kulkul


def test_security_headers_and_https_default():
    main_src = (ROOT / "app" / "main.py").read_text(encoding="utf-8")
    assert 'os.environ.get("MOS_HTTPS_ONLY", "1") != "0"' in main_src
    assert 'MOS_HTTPS_ONLY="${MOS_HTTPS_ONLY:-0}"' in (ROOT / "scripts" / "run-mos-web.sh").read_text(encoding="utf-8")
    c = TestClient(app)
    r = c.get("/dang-nhap")
    csp = r.headers["content-security-policy"]
    for directive in ("default-src 'self'", "frame-ancestors 'self'", "font-src 'self' https://fonts.gstatic.com", "form-action 'self'", "base-uri 'self'"):
        assert directive in csp
    assert r.headers["x-frame-options"] == "SAMEORIGIN"
    assert r.headers["x-content-type-options"] == "nosniff"


# ------------------------------------------------------------------- W4 lang
def test_lang_resolution_order_query_session_cookie_header_default():
    c = TestClient(app)
    assert '<html lang="vi">' in c.get("/dang-nhap").text
    assert '<html lang="en">' in c.get("/dang-nhap", headers={"accept-language": "en-US,en;q=0.9,vi;q=0.5"}).text
    r = c.get("/dang-nhap?ngon-ngu=EN-us")
    assert '<html lang="en">' in r.text
    assert c.cookies.get("mos_lang") == "en"
    # Cookie giữ lựa chọn khi không có session/tham số.
    assert '<html lang="en">' in c.get("/dang-nhap").text
    # Slug tiếng Việt không đổi theo ngôn ngữ.
    assert c.get("/tien-do?lang=en", follow_redirects=False).status_code in (303, 200)


def test_student_nav_has_lang_switch_and_learn_tab():
    c = _student_client()
    r = c.get("/tien-do?lang=en")
    assert r.status_code == 200
    assert 'class="lang-switch"' in r.text
    assert 'aria-current="true" lang="en"' in r.text
    assert 'href="/hoc"' in r.text
    r = c.get("/tien-do?lang=vi")
    assert 'aria-current="true" lang="vi"' in r.text
    assert "Bản đồ Năng lực — Word" in r.text
    assert r.text.count('class="skill-node"') >= 20


# ------------------------------------------------------------- W8 / W9 /hoc
def test_explore_requires_login():
    c = TestClient(app)
    r = c.get("/hoc", follow_redirects=False)
    assert r.status_code == 303 and r.headers["location"] == "/dang-nhap"


def test_explore_map_and_lesson_bilingual_without_scores():
    c = _student_client()
    r = c.get("/hoc")
    assert r.status_code == 200
    assert "Bản đồ Năng lực" in r.text
    assert r.text.count('class="skill-node"') == 20  # 20 rubric Word
    assert "1. Quản lý tài liệu" in r.text and "6. Quản lý cộng tác trên tài liệu" in r.text
    assert "/1000" not in r.text and "Điểm TB" not in r.text

    r = c.get("/hoc/word-objective-1-2?lang=en")
    assert '<html lang="en">' in r.text
    assert "Format documents" in r.text
    assert "Open the <strong>Design</strong> tab." in r.text
    assert "1. Set the page color to the light grey DFDBD5." in r.text

    r = c.get("/hoc/word-objective-1-2?lang=vi")
    assert "Mở tab <strong>Design</strong>." in r.text
    assert 'aria-current="page"' in r.text  # node đang xem được tô

    assert c.get("/hoc?chuong-trinh=excel").text.count("excel-objective-") >= 19
    assert c.get("/hoc/word-objective-9-9").status_code == 404
    assert c.get("/hoc/..%2Fusers").status_code == 404


def test_explore_lesson_states_real_grading_ceiling():
    c = _student_client()
    r = c.get("/hoc/word-objective-1-3?lang=vi")
    assert "Chấm tự động: 55/100 điểm. 45 điểm còn lại cô/thầy chấm tay." in r.text
    r = c.get("/hoc/word-objective-1-3?lang=en")
    assert "Auto-graded: 55/100 points." in r.text
    # Bài chấm được trọn điểm thì không có lời cảnh báo.
    assert "Chấm tự động" not in c.get("/hoc/word-objective-1-2?lang=vi").text


# ---------------------------------------------------------------- W5 / A6
def test_ceiling_matches_audit_numbers():
    from app.ceiling import PRODUCIBLE_ACTIONS, ceiling, ceiling_label
    from app.grade import load_rubric

    assert ceiling(load_rubric("word-objective-1-3")) == {
        "max": 100, "auto": 55, "manual": 45, "manual_ids": ["W13-S01", "W13-S02", "W13-S03"],
    }
    assert ceiling(load_rubric("word-objective-1-4"))["auto"] == 70
    assert ceiling(load_rubric("word-objective-1-1"))["auto"] == 82
    assert ceiling(load_rubric("word-objective-1-2"))["manual"] == 0
    assert ceiling_label(ceiling(load_rubric("word-objective-1-2"))) == ""
    assert "find" in PRODUCIBLE_ACTIONS

    from scripts import rubric_tool

    assert rubric_tool.PRODUCIBLE_ACTIONS is PRODUCIBLE_ACTIONS


def test_task_card_template_shows_ceiling_and_learn_link():
    html = (TPL / "progress.html").read_text(encoding="utf-8")
    assert 'class="ceiling"' in html and "cô/thầy chấm tay" in html
    assert 'href="/hoc/{{ ex.project_id }}"' in html


# ----------------------------------------------------- B5 facts + predicates
W_NS = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"


def _docx(tmp_path: Path, *, settings: str, creator: str) -> Path:
    doc = (
        f'<?xml version="1.0" encoding="UTF-8"?><w:document xmlns:w="{W_NS}"><w:body>'
        "<w:p><w:r><w:t>Hello</w:t></w:r></w:p></w:body></w:document>"
    )
    core = (
        '<?xml version="1.0" encoding="UTF-8"?>'
        '<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" '
        'xmlns:dc="http://purl.org/dc/elements/1.1/">'
        f"<dc:creator>{creator}</dc:creator></cp:coreProperties>"
    )
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as z:
        z.writestr("[Content_Types].xml", "<Types/>")
        z.writestr("word/document.xml", doc)
        z.writestr("word/settings.xml", f'<?xml version="1.0"?><w:settings xmlns:w="{W_NS}">{settings}</w:settings>')
        z.writestr("docProps/core.xml", core)
    path = tmp_path / "t.docx"
    path.write_bytes(buf.getvalue())
    return path


def test_inspect_and_compatibility_facts_and_predicates(tmp_path):
    from app.grade import evaluate_facts
    from app.word_xml import extract_word_facts

    rubric = {
        "max_score": 100,
        "criteria": [
            {"id": "I", "kind": "artifact", "weight": 50, "predicate": {"type": "personal_info_removed"}, "prompt": "inspect"},
            {"id": "X", "kind": "artifact", "weight": 50, "predicate": {"type": "compatibility_mode", "min": 15}, "prompt": "compat"},
        ],
    }
    dirty = _docx(
        tmp_path,
        settings='<w:compatSetting w:name="compatibilityMode" w:uri="u" w:val="14"/>',
        creator="Joan Lambert",
    )
    facts = extract_word_facts(dirty)
    assert facts["personal_info_removed"] is False and facts["compatibility_mode"] == "14"
    by_id = {c["criterion_id"]: c["status"] for c in evaluate_facts(facts, rubric)["criteria"]}
    assert by_id == {"I": "fail", "X": "fail"}

    clean = _docx(
        tmp_path,
        settings='<w:removePersonalInformation/><w:compatSetting w:name="compatibilityMode" w:uri="u" w:val="15"/>',
        creator="",
    )
    facts = extract_word_facts(clean)
    assert facts["personal_info_removed"] is True and facts["compatibility_mode"] == "15"
    by_id = {c["criterion_id"]: c["status"] for c in evaluate_facts(facts, rubric)["criteria"]}
    assert by_id == {"I": "pass", "X": "pass"}


def test_study_guide_starters_already_inspected_so_1_4_keeps_action_kind():
    """Tệp đề của Study Guide đã sẵn removePersonalInformation + compatibilityMode=15,
    nên KHÔNG chuyển W14-I01/X01 sang artifact: học sinh sẽ được điểm miễn phí.
    Trần điểm 70/100 được nói rõ cho học sinh thay vì đổi predicate."""
    from app.word_xml import extract_word_facts

    starter = extract_word_facts(ROOT / "tests" / "fixtures" / "word-objective-1-4" / "Word_1-4.docx")
    assert starter["personal_info_removed"] is True and starter["compatibility_mode"] == "15"
    rubric = (ROOT / "app" / "rubrics" / "word-objective-1-4.json").read_text(encoding="utf-8")
    assert re.search(r'"id":\s*"W14-I01"[\s\S]*?"kind":\s*"action_sequence"', rubric)
