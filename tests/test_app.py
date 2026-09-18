from fastapi.testclient import TestClient
from app.main import INSTALLER_DIR, app

def test_login_page():
    c = TestClient(app)
    r = c.get("/dang-nhap")
    assert r.status_code == 200
    assert "Đăng nhập MOS-KulKul" in r.text
    assert "Hệ thống luyện thi MOS" in r.text
    assert "program-menu" not in r.text
    assert "data-program" not in r.text
    assert "program-tile" not in r.text
    assert "Chọn chương trình" not in r.text
    assert "login.js" not in r.text
    assert "/cai-dat" in r.text
    assert "đã cài trên máy" in r.text
    assert "Tải MOS-KulKul" in r.text


def test_api_login_and_programs():
    c = TestClient(app)
    denied = c.get("/api/me")
    assert denied.status_code == 401
    programs = c.get("/api/chuong-trinh").json()
    assert programs["ok"] is True
    ids = [p["id"] for p in programs["programs"]]
    assert ids == ["word", "excel", "powerpoint"]
    bad = c.post("/api/dang-nhap", json={"username": "giaovien", "password": "sai"})
    assert bad.status_code == 401
    r = c.post(
        "/api/dang-nhap",
        json={"username": "giaovien", "password": "Mos@Gds2026", "chuong_trinh": "excel"},
    )
    assert r.status_code == 200
    body = r.json()
    assert body["ok"] is True
    assert body["user"]["username"] == "giaovien"
    assert body["program"]["id"] == "excel"
    me = c.get("/api/me").json()
    assert me["ok"] is True
    assert me["program"]["id"] == "excel"


def test_install_page_lists_windows_and_macos():
    c = TestClient(app)
    r = c.get("/cai-dat")
    assert r.status_code == 200
    assert "Windows" in r.text
    assert "macOS" in r.text
    assert "MOS-KulKul-Setup-Windows.exe" in r.text
    assert "macOS" in r.text
    assert "/cai-dat/windows" in r.text
    assert "/cai-dat/windows-full" in r.text
    assert "/cai-dat/macos" in r.text
    assert "MOS-KulKul-Setup-macOS.zip" in r.text
    assert "Cai MOS-KulKul.command" in r.text
    assert "macos.sh" in r.text
    assert "bộ cài nhỏ" in r.text.lower() or "Bộ cài nhỏ" in r.text
    missing = c.get("/cai-dat/windows")
    win_ready = any(
        (INSTALLER_DIR / name).is_file()
        for name in ("MOS-KulKul-Setup-Windows.exe", "MOS-Dock-Setup-Windows.exe")
    )
    if win_ready:
        assert missing.status_code == 200
        assert missing.headers.get("content-disposition", "").lower().find("windows") >= 0 or "kulkul" in missing.headers.get("content-disposition", "").lower()
    else:
        assert missing.status_code == 404
    missing_mac = c.get("/cai-dat/macos")
    mac_ready = any(
        (INSTALLER_DIR / name).is_file()
        for name in (
            "MOS-KulKul-Setup-macOS.zip",
            "MOS-Dock-Setup-macOS.zip",
            "MOS-Dock-Setup-macOS.pkg",
        )
    )
    if mac_ready:
        assert missing_mac.status_code == 200
    else:
        assert missing_mac.status_code == 404
    sh = c.get("/cai-dat/macos.sh")
    assert sh.status_code == 200
    assert "osacompile" in sh.text
    assert "MOS-KulKul.app" in sh.text
    src = c.get("/cai-dat/macos-files/mosdock_mac.py")
    assert src.status_code == 200
    assert "17331" in src.text
    assert c.get("/cai-dat/macos-files/secret").status_code == 404
    full = c.get("/cai-dat/windows-full")
    full_ready = (INSTALLER_DIR / "MOS-KulKul-Setup-Windows-Full.exe").is_file()
    if full_ready:
        assert full.status_code == 200
        assert "full" in (full.headers.get("content-disposition") or "").lower()
    else:
        assert full.status_code == 404


def test_windows_web_stub_iss_downloads_full_from_server():
    from pathlib import Path

    folder = Path(__file__).resolve().parent.parent / "desktop" / "installer" / "windows"
    stub = (folder / "mosdock-web.iss").read_text(encoding="utf-8")
    full = (folder / "mosdock.iss").read_text(encoding="utf-8")
    assert "https://mos.gds.edu.vn/cai-dat/windows-full" in stub
    assert "CreateAppDir=no" in stub
    assert "Uninstallable=no" in stub
    assert "CreateDownloadPage" in stub
    assert "{#Dist}" not in stub
    assert "OutputBaseFilename=MOS-KulKul-Setup-Windows-Full" in full
    assert "{#Dist}\\*" in full


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
    assert mini["dock"]["h"] == 108
    assert mini["dock"]["w"] == 260
    assert mini["word"]["h"] == 1040
    compact = c.get("/api/layout", params={"state": "bottom", "w": 1920, "h": 1040, "compact": 1}).json()
    assert compact["compact"] is True
    assert compact["dock"]["h"] == 108
    assert compact["word"]["h"] == 1040


def test_kulkul_home_after_login():
    c = TestClient(app)
    r = c.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"}, follow_redirects=True)
    assert r.status_code == 200
    assert "Xin chào" in r.text
    assert "Cài MOS-KulKul" in r.text
    assert "program-menu" not in r.text
    assert "word-sim" not in r.text
    assert "office.com" not in r.text.lower()
    assert "KulKul" in r.text
    assert "GMetrix" not in r.text
    assert "/quan-tri" in r.text
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
        "/api/dang-nhap",
        json={"username": "giaovien", "password": "Mos@Gds2026", "chuong_trinh": "excel"},
    )
    assert r.status_code == 200
    assert r.json()["program"]["id"] == "excel"
    inner = c.get("/khung/office")
    assert inner.status_code == 200
    assert "Mở Excel trên máy" in inner.text
    assert "office.com" not in inner.text.lower()
    c.get("/?chuong-trinh=powerpoint")
    assert "Mở PowerPoint trên máy" in c.get("/khung/office").text


def test_dock_mode_hides_simulated_word():
    c = TestClient(app)
    c.get("/dang-nhap?che-do=dock")
    r = c.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"}, follow_redirects=True)
    assert r.status_code == 200
    assert "KulKul" in r.text
    assert "GMetrix" not in r.text
    assert "word-sim" not in r.text
