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


def test_https_only_defaults_to_on():
    """Không đặt biến môi trường thì cờ Secure bật; chỉ '0' mới tắt."""
    from app.main import https_only_from_env

    assert https_only_from_env({}) is True
    assert https_only_from_env({"MOS_HTTPS_ONLY": "1"}) is True
    assert https_only_from_env({"MOS_HTTPS_ONLY": "0"}) is False
    assert https_only_from_env({"MOS_HTTPS_ONLY": ""}) is True


def test_dev_script_runs_over_plain_http(tmp_path):
    """scripts/run-mos-web.sh phải tắt cờ Secure vì nó phục vụ http://127.0.0.1."""
    import os
    import subprocess

    fake_bin = tmp_path / "bin"
    fake_bin.mkdir()
    # Thế uvicorn bằng script in ra biến môi trường rồi thoát.
    (tmp_path / ".venv" / "bin").mkdir(parents=True)
    stub = tmp_path / ".venv" / "bin" / "python"
    stub.write_text("#!/bin/sh\necho \"HTTPS_ONLY=$MOS_HTTPS_ONLY\"\n")
    stub.chmod(0o755)
    (tmp_path / ".venv" / "bin" / "pip").write_text("#!/bin/sh\nexit 0\n")
    (tmp_path / ".venv" / "bin" / "pip").chmod(0o755)
    (tmp_path / "requirements.txt").write_text("")
    (tmp_path / "scripts").mkdir()
    script = tmp_path / "scripts" / "run-mos-web.sh"
    script.write_text((ROOT / "scripts" / "run-mos-web.sh").read_text(encoding="utf-8"))
    script.chmod(0o755)
    env = {k: v for k, v in os.environ.items() if k != "MOS_HTTPS_ONLY"}
    out = subprocess.run(["bash", str(script)], cwd=tmp_path, env=env, capture_output=True, text=True, timeout=30)
    assert out.returncode == 0, out.stderr
    assert "HTTPS_ONLY=0" in out.stdout


def test_security_headers():
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


# ------------------------------------------------ ngưỡng làm chủ theo trần điểm
def test_mastery_threshold_follows_real_ceiling():
    from app.explore import auto_ceiling, level_for

    # 1-3 trần 55: làm hết phần máy chấm được thì phải là mastered
    assert auto_ceiling("word-objective-1-3") == 55
    assert level_for("in_progress", 55, "word-objective-1-3") == "mastered"
    assert level_for("in_progress", 38, "word-objective-1-3") == "learning"
    # rubric trọn vẹn giữ nguyên ngưỡng 70
    assert level_for(None, 70, "word-objective-1-2") == "mastered"
    assert level_for(None, 69, "word-objective-1-2") == "learning"
    # không truyền project_id thì giữ hành vi cũ
    assert level_for(None, 70) == "mastered"
    assert level_for(None, 69) == "learning"
    assert level_for(None, 0) == "new"
    # slug lạ / rubric không tồn tại → coi như trần 100
    assert auto_ceiling("word-objective-9-9") == 100
    assert auto_ceiling("../etc/passwd") == 100


def test_no_rubric_has_unreachable_mastery():
    """Không rubric nào được đặt ngưỡng cao hơn trần điểm của chính nó."""
    import glob
    import json

    from app.ceiling import ceiling
    from app.explore import MASTERY_RATIO, level_for

    for path in glob.glob(str(ROOT / "app" / "rubrics" / "*-objective-*.json")):
        rubric = json.load(open(path, encoding="utf-8"))
        auto = ceiling(rubric)["auto"]
        assert MASTERY_RATIO * auto <= auto, path
        if auto > 0:  # trần 0 (chỉ toàn thao tác chết) thì chỉ giáo viên đánh dấu mastered
            assert level_for("in_progress", auto, Path(path).stem) == "mastered", path


# ------------------------------------------------------- cờ Secure của cookie
def test_lang_cookie_secure_follows_https_only(monkeypatch):
    monkeypatch.setattr("app.main.HTTPS_ONLY", True)
    c = TestClient(app)
    r = c.get("/dang-nhap?lang=en")
    dat = [v for k, v in r.headers.items() if k.lower() == "set-cookie" and "mos_lang" in v]
    assert dat and "Secure" in dat[0]

    monkeypatch.setattr("app.main.HTTPS_ONLY", False)
    c = TestClient(app)
    r = c.get("/dang-nhap?lang=en")
    dat = [v for k, v in r.headers.items() if k.lower() == "set-cookie" and "mos_lang" in v]
    assert dat and "Secure" not in dat[0]


# ------------------------------------------------------------------- Vary
def test_vary_header_present_for_language_negotiation():
    c = TestClient(app)
    vary = c.get("/dang-nhap").headers.get("Vary", "").lower()
    assert "accept-language" in vary and "cookie" in vary
    # Vary sẵn có (StaticFiles/FileResponse) được gộp, không bị ghi đè.
    vary_static = c.get("/static/tokens.css").headers.get("Vary", "").lower()
    assert "accept-language" in vary_static and "cookie" in vary_static


# ------------------------------------------------------------- đổi mật khẩu
def test_doi_mat_khau_requires_login_and_links_in_nav():
    c = TestClient(app)
    assert c.get("/doi-mat-khau", follow_redirects=False).status_code == 303
    assert c.post("/doi-mat-khau", data={}, follow_redirects=False).status_code == 303
    s = _student_client()
    assert 'href="/doi-mat-khau"' in s.get("/tien-do").text
    assert s.get("/doi-mat-khau").status_code == 200


def _login_ok(username: str, password: str) -> bool:
    return TestClient(app).post("/api/dang-nhap", json={"username": username, "password": password}).json()["ok"]


def test_doi_mat_khau_doi_duoc_va_chan_mat_khau_cu_sai():
    """Chạy trên tài khoản dùng một lần để không đụng vào hocsinh/giaovien/admin."""
    from app.accounts import create_account
    from app.db import cursor

    username = "tmp-doi-mk"
    password = "MatKhauGoc123"
    with cursor() as cur:
        cur.execute("DELETE FROM users WHERE username = %s", (username,))
    create_account(username=username, name="Tạm đổi MK", role="student", password=password)
    try:
        c = TestClient(app)
        assert c.post("/api/dang-nhap", json={"username": username, "password": password}).json()["ok"]

        # Sai mật khẩu cũ → 400, không đổi, không lộ gì thêm.
        r = c.post(
            "/doi-mat-khau",
            data={"mat_khau_cu": "sai-bet", "mat_khau_moi": "MatKhauMoi123", "xac_nhan": "MatKhauMoi123"},
            follow_redirects=False,
        )
        assert r.status_code == 400 and "Mật khẩu hiện tại không đúng" in r.text
        assert _login_ok(username, password) and not _login_ok(username, "MatKhauMoi123")

        # Quá ngắn / không khớp / trùng cũ đều bị chặn.
        for body, msg in (
            ({"mat_khau_cu": password, "mat_khau_moi": "ngan", "xac_nhan": "ngan"}, "ít nhất 8"),
            ({"mat_khau_cu": password, "mat_khau_moi": "MatKhauMoi123", "xac_nhan": "KhacHan123"}, "không khớp"),
            ({"mat_khau_cu": password, "mat_khau_moi": password, "xac_nhan": password}, "phải khác"),
        ):
            r = c.post("/doi-mat-khau", data=body, follow_redirects=False)
            assert r.status_code == 400 and msg in r.text, body
        assert _login_ok(username, password)

        # Đúng mật khẩu cũ → đổi được, phiên bị huỷ, đăng nhập lại bằng mật khẩu mới.
        r = c.post(
            "/doi-mat-khau",
            data={"mat_khau_cu": password, "mat_khau_moi": "MatKhauMoi123", "xac_nhan": "MatKhauMoi123"},
            follow_redirects=False,
        )
        assert r.status_code == 303 and r.headers["location"] == "/dang-nhap?doi=ok"
        assert c.get("/doi-mat-khau", follow_redirects=False).status_code == 303  # phiên đã huỷ
        assert _login_ok(username, "MatKhauMoi123") and not _login_ok(username, password)
        assert "Đã đổi mật khẩu" in TestClient(app).get("/dang-nhap?doi=ok").text
    finally:
        with cursor() as cur:
            cur.execute("DELETE FROM users WHERE username = %s", (username,))
