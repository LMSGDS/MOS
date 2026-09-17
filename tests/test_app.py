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
    assert "Mở Microsoft Word" in r.text
    assert "ms-word:" in c.get("/static/mos.js").text

def test_template_docx():
    c = TestClient(app)
    r = c.get("/static/mau-van-ban.docx")
    assert r.status_code == 200
    assert r.content[:2] == b"PK"
