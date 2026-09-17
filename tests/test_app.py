from fastapi.testclient import TestClient
from app.main import app

def test_login_page():
    c = TestClient(app)
    r = c.get("/dang-nhap")
    assert r.status_code == 200
    assert "Đăng nhập MOS" in r.text

def test_home_requires_login():
    c = TestClient(app)
    r = c.get("/", follow_redirects=False)
    assert r.status_code == 303
    assert "/dang-nhap" in r.headers["location"]

def test_login_and_open_word_page():
    c = TestClient(app)
    r = c.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"}, follow_redirects=True)
    assert r.status_code == 200
    assert 'src="/khung/word"' in r.text
    assert "office.com" not in r.text.lower()
    assert "officeapps.live.com" not in r.text.lower()
    inner = c.get("/khung/word")
    assert inner.status_code == 200
    assert "Mở Word trên máy" in inner.text
    assert "Office Online" in inner.text
    assert "ms-word:" in c.get("/static/mos.js").text
    assert inner.headers.get("x-frame-options") == "SAMEORIGIN"

def test_template_docx():
    c = TestClient(app)
    r = c.get("/static/mau-van-ban.docx")
    assert r.status_code == 200
    assert r.content[:2] == b"PK"
