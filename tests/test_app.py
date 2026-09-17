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


def test_template_docx():
    c = TestClient(app)
    r = c.get("/static/mau-van-ban.docx")
    assert r.status_code == 200
    assert r.content[:2] == b"PK"


def test_layout_api_bottom_no_overlap():
    c = TestClient(app)
    r = c.get("/api/layout", params={"state": "bottom", "x": 0, "y": 0, "w": 1920, "h": 1040})
    assert r.status_code == 200
    dock, word = r.json()["dock"], r.json()["word"]
    assert word["y"] + word["h"] == dock["y"]
    assert dock["y"] + dock["h"] == 1040


def test_layout_api_side_docks_leave_word_visible():
    c = TestClient(app)
    left = c.get("/api/layout", params={"state": "left", "w": 1920, "h": 1040}).json()
    assert left["word"]["x"] == left["dock"]["w"]
    assert left["word"]["w"] + left["dock"]["w"] == 1920
    right = c.get("/api/layout", params={"state": "right", "w": 1920, "h": 1040}).json()
    assert right["word"]["x"] == 0
    assert right["word"]["w"] + right["dock"]["w"] == 1920
    mini = c.get("/api/layout", params={"state": "minimized", "w": 1920, "h": 1040}).json()
    assert mini["dock"]["h"] == 48
    assert mini["word"]["h"] == 1040 - 48


def test_gmetrix_home_after_login():
    c = TestClient(app)
    r = c.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"}, follow_redirects=True)
    assert r.status_code == 200
    assert "Thu nhỏ" in r.text
    assert "Đính trái" in r.text
    assert "Đính phải" in r.text
    assert "Đặt Word vào chỗ đã chọn" in r.text
    assert "word-sim" in r.text
    assert "office.com" not in r.text.lower()
    assert "gmetrix.js" in r.text
    inner = c.get("/khung/word")
    assert inner.status_code == 200
    assert "Mở Word trên máy" in inner.text
    assert "vị trí đã chọn" in inner.text
    assert inner.headers.get("x-frame-options") == "SAMEORIGIN"
    js = c.get("/static/mos.js").text
    assert "mos-place-word" in js
    assert "mosdock:place" in js
    assert "17331" in js


def test_dock_mode_hides_simulated_word():
    c = TestClient(app)
    c.get("/dang-nhap?che-do=dock")
    r = c.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"}, follow_redirects=True)
    assert r.status_code == 200
    assert "mini-browser" in r.text
    assert "word-sim" not in r.text
