from fastapi.testclient import TestClient
from app.main import INSTALLER_DIR, app

def test_login_page():
    c = TestClient(app)
    r = c.get("/dang-nhap")
    assert r.status_code == 200
    assert "Đăng nhập MOS" in r.text
    assert "program-menu" in r.text
    assert r.text.index("program-menu") < r.text.index("card login")
    assert "Microsoft Word" in r.text
    assert "Microsoft Excel" in r.text
    assert "Microsoft PowerPoint" in r.text
    assert "login.js" in r.text
    assert "/cai-dat" in r.text


def test_install_page_lists_windows_and_macos():
    c = TestClient(app)
    r = c.get("/cai-dat")
    assert r.status_code == 200
    assert "Windows" in r.text
    assert "macOS" in r.text
    assert "MOS-Dock-Setup-Windows.exe" in r.text
    assert "macOS" in r.text
    assert "/cai-dat/windows" in r.text
    assert "/cai-dat/macos" in r.text
    missing = c.get("/cai-dat/windows")
    if (INSTALLER_DIR / "MOS-Dock-Setup-Windows.exe").is_file():
        assert missing.status_code == 200
        assert missing.headers.get("content-disposition", "").lower().find("windows") >= 0
    else:
        assert missing.status_code == 404
    missing_mac = c.get("/cai-dat/macos")
    mac_ready = any(
        (INSTALLER_DIR / name).is_file()
        for name in ("MOS-Dock-Setup-macOS.pkg", "MOS-Dock-Setup-macOS.zip")
    )
    if mac_ready:
        assert missing_mac.status_code == 200
    else:
        assert missing_mac.status_code == 404


def test_home_requires_login():
    c = TestClient(app)
    r = c.get("/", follow_redirects=False)
    assert r.status_code == 303
    assert "/dang-nhap" in r.headers["location"]


def test_template_office_files():
    c = TestClient(app)
    for path in (
        "/static/mau-van-ban.docx",
        "/static/mau-bang-tinh.xlsx",
        "/static/mau-bai-trinh-bay.pptx",
    ):
        r = c.get(path)
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
    assert mini["dock"]["h"] == 96
    assert mini["word"]["h"] == 1040 - 96
    compact = c.get("/api/layout", params={"state": "bottom", "w": 1920, "h": 1040, "compact": 1}).json()
    assert compact["compact"] is True
    assert compact["dock"]["h"] == 96
    assert compact["word"]["h"] == 1040 - 96


def test_kulkul_home_after_login():
    c = TestClient(app)
    r = c.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"}, follow_redirects=True)
    assert r.status_code == 200
    assert "Thu nhỏ" in r.text
    assert "Đính trái" in r.text
    assert "Đính phải" in r.text
    assert "Đặt Word vào chỗ đã chọn" in r.text
    assert "Mở rộng đề" in r.text
    assert "word-sim" in r.text
    assert "office.com" not in r.text.lower()
    assert "kulkul.js" in r.text
    assert "KulKul" in r.text
    assert "GMetrix" not in r.text
    inner = c.get("/khung/word")
    assert inner.status_code == 200
    assert "Mở Word trên máy" in inner.text
    assert "/cai-dat" in inner.text
    assert inner.headers.get("x-frame-options") == "SAMEORIGIN"
    js = c.get("/static/mos.js").text
    assert "mos-place-word" in js
    assert "mosdock:open" in js
    assert "Macintosh" in js
    assert "hasNativeDock" in js
    assert "mosdock:place" in c.get("/static/kulkul.js").text
    assert "ms-excel:" in js or "data-protocol" in inner.text


def test_login_excel_then_open_excel():
    c = TestClient(app)
    r = c.post(
        "/dang-nhap",
        data={"username": "giaovien", "password": "Mos@Gds2026", "chuong_trinh": "excel"},
        follow_redirects=True,
    )
    assert r.status_code == 200
    assert "Microsoft Excel" in r.text
    assert "Mở Excel trên máy" in c.get("/khung/office").text
    assert "office.com" not in r.text.lower()
    ppt = c.get("/?chuong-trinh=powerpoint")
    assert ppt.status_code == 200
    assert "Microsoft PowerPoint" in ppt.text
    assert "Mở PowerPoint trên máy" in c.get("/khung/office").text


def test_dock_mode_hides_simulated_word():
    c = TestClient(app)
    c.get("/dang-nhap?che-do=dock")
    r = c.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"}, follow_redirects=True)
    assert r.status_code == 200
    assert "KulKul" in r.text
    assert "GMetrix" not in r.text
    assert "word-sim" not in r.text
