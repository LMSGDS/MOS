from fastapi.testclient import TestClient
from app.main import INSTALLER_DIR, app, app_version

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
    assert "kulkul.png?v=" in r.text


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
    assert "Invoke-WebRequest" in r.text
    assert "https://mos.gds.edu.vn/cai-dat/windows-full" in r.text
    assert "Sao chép lệnh" in r.text
    assert "MOS-KulKul-Setup-Windows.exe" in r.text
    assert "macOS" in r.text
    assert "/cai-dat/windows" in r.text
    assert "/cai-dat/windows.zip" in r.text
    assert "/cai-dat/windows-full" in r.text
    assert "/cai-dat/windows-full.zip" in r.text
    assert "/cai-dat/macos" in r.text
    assert "MOS-KulKul-Setup-macOS.zip" in r.text
    assert "MOS-KulKul.app" in r.text
    assert "MOS-KulKul-Setup-macOS.pkg" in r.text
    assert "Cai MOS-KulKul.command" in r.text
    assert "macos.sh" in r.text
    assert "Sao chép lệnh Terminal" in r.text
    assert "bộ cài nhỏ" in r.text.lower() or "Bộ cài nhỏ" in r.text
    assert "Demo tất cả bài tập" in r.text
    assert "quét virus" in r.text.lower() or "SmartScreen" in r.text
    assert "Giữ lại" in r.text
    assert "Unblock-File" in r.text
    assert "/cai-dat/checksums" in r.text
    assert app_version() in r.text
    assert "bung hết chiều ngang" in r.text
    assert "kéo mép" in r.text
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
    zipped = c.get("/cai-dat/windows.zip")
    if win_ready:
        assert zipped.status_code == 200
        assert zipped.content[:2] == b"PK"
        assert "zip" in (zipped.headers.get("content-type") or "").lower()
        assert "attachment" in (zipped.headers.get("content-disposition") or "").lower()
    else:
        assert zipped.status_code == 404
    sums = c.get("/cai-dat/checksums")
    assert sums.status_code == 200
    assert sums.json()["ok"] is True
    assert "files" in sums.json()
    missing_mac = c.get("/cai-dat/macos")
    mac_ready = any(
        (INSTALLER_DIR / name).is_file()
        for name in (
            "MOS-KulKul-Setup-macOS.zip",
            "MOS-Dock-Setup-macOS.zip",
            "MOS-KulKul-Setup-macOS.pkg",
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
    ps1 = c.get("/cai-dat/windows.ps1")
    assert ps1.status_code == 200
    assert "text/plain" in (ps1.headers.get("content-type") or "")
    assert "mos.gds.edu.vn" in ps1.text
    assert "Unblock-File" in ps1.text
    assert "/cai-dat/windows-full" in ps1.text
    assert "Start-Process" in ps1.text
    src = c.get("/cai-dat/macos-files/mosdock_mac.py")
    assert src.status_code == 200
    assert "17331" in src.text
    ver = c.get("/cai-dat/macos-files/VERSION")
    assert ver.status_code == 200
    assert app_version() in ver.text
    plist = c.get("/cai-dat/macos-files/Info.plist")
    assert plist.status_code == 200
    assert "mos-kulkul" in plist.text
    assert c.get("/cai-dat/macos-files/secret").status_code == 404
    full = c.get("/cai-dat/windows-full")
    full_ready = (INSTALLER_DIR / "MOS-KulKul-Setup-Windows-Full.exe").is_file()
    if full_ready:
        assert full.status_code == 200
        assert "full" in (full.headers.get("content-disposition") or "").lower()
    else:
        assert full.status_code == 404


def test_install_page_checksums_link_without_artifacts(monkeypatch):
    import app.main as main

    monkeypatch.setattr(main, "_installer_meta", lambda: [])
    r = TestClient(app).get("/cai-dat")
    assert r.status_code == 200
    assert "/cai-dat/checksums" in r.text
    assert "SHA-256" in r.text


def test_wrap_installer_zip_includes_readme(tmp_path):
    from app.main import sha256_path, wrap_installer_zip

    exe = tmp_path / "MOS-KulKul-Setup-Windows.exe"
    exe.write_bytes(b"MZ-fake-installer")
    zipped = wrap_installer_zip(exe)
    assert zipped.suffix == ".zip"
    assert zipped.is_file()
    import zipfile

    with zipfile.ZipFile(zipped) as zf:
        names = set(zf.namelist())
        assert "MOS-KulKul-Setup-Windows.exe" in names
        assert "HUONG-DAN-CAI.txt" in names
        guide = zf.read("HUONG-DAN-CAI.txt").decode("utf-8")
        assert "SmartScreen" in guide
        assert "mos.gds.edu.vn" in guide
        assert "cai-dat/windows-full" in guide
    assert wrap_installer_zip(exe) == zipped
    assert len(sha256_path(zipped)) == 64


def test_windows_web_stub_iss_downloads_full_from_server():
    from pathlib import Path

    folder = Path(__file__).resolve().parent.parent / "desktop" / "installer" / "windows"
    stub = (folder / "mosdock-web.iss").read_text(encoding="utf-8")
    full = (folder / "mosdock.iss").read_text(encoding="utf-8")
    assert "https://mos.gds.edu.vn/cai-dat/windows-full" in stub
    assert 'MyAppPublisher "mos.gds.edu.vn"' in stub
    assert 'MyAppPublisher "mos.gds.edu.vn"' in full
    assert "Trường GDS" not in stub
    assert "Trường GDS" not in full
    assert "CreateAppDir=no" in stub
    assert "Uninstallable=no" in stub
    assert "CreateDownloadPage" in stub
    assert "{#Dist}" not in stub
    assert "OutputBaseFilename=MOS-KulKul-Setup-Windows-Full" in full
    assert "{#Dist}\\*" in full
    csproj = (
        Path(__file__).resolve().parent.parent / "desktop" / "MosDock" / "MosDock.csproj"
    ).read_text(encoding="utf-8")
    assert f'MyAppVersion "{app_version()}"' in stub
    assert f'MyAppVersion "{app_version()}"' in full
    assert f"<Version>{app_version()}</Version>" in csproj


def test_windows_sources_include_action_demo():
    from pathlib import Path

    root = Path(__file__).resolve().parent.parent / "desktop" / "MosDock"
    program = (root / "Protocol.cs").read_text(encoding="utf-8")
    demo = (root / "WordActionDemo.cs").read_text(encoding="utf-8")
    form = (root / "MainForm.cs").read_text(encoding="utf-8")
    assert "--demo-actions" in program
    assert "--demo-all" in program
    assert "Selection.Find" in demo
    assert "WdGoToGraphic" in demo
    assert "RunNavigate" in demo
    assert "RunSaveShare" in demo
    assert "RunInspect" in demo
    assert "Demo tất cả bài tập" in form
    assert "1.21.0" in (root / "MosDock.csproj").read_text(encoding="utf-8")
    assert 'TrackAsync("hint"' in (root / "MainForm.cs").read_text(encoding="utf-8")
    assert "HomeRadar" in (root / "MainForm.cs").read_text(encoding="utf-8")
    assert "/api/v1/progress/adaptive" in (root / "HomeRadar.cs").read_text(encoding="utf-8")
    assert "Q-matrix SOP" in (root / "SkillReview.cs").read_text(encoding="utf-8")
    assert "Ma trận kỹ năng" in (root / "SkillReview.cs").read_text(encoding="utf-8")
    assert "LocalExamStore" in (root / "ExamHub.cs").read_text(encoding="utf-8")
    assert "X-MOS-Artifact-SHA256" in (root / "Portal.cs").read_text(encoding="utf-8")
    assert "using System.Text.Json;" in (root / "BackgroundSync.cs").read_text(encoding="utf-8")
    assert "PinToWork" in (root / "MainForm.cs").read_text(encoding="utf-8")
    assert "WithThickness" in (root / "LayoutMath.cs").read_text(encoding="utf-8")
    assert "NudgeNavThickness" in (root / "MainForm.cs").read_text(encoding="utf-8")
    assert "Lỗi thường gặp" in (root / "SkillReview.cs").read_text(encoding="utf-8")
    assert "Thao tác đúng" in (root / "SkillReview.cs").read_text(encoding="utf-8")
    assert "Thao tác của bạn" in (root / "SkillReview.cs").read_text(encoding="utf-8")
    assert "AnalysisBlocks" in (root / "SkillReview.cs").read_text(encoding="utf-8")
    assert "DemoAllAsync" in (root / "ExamHub.cs").read_text(encoding="utf-8")
    assert "kind=results" in (root / "ExamHub.cs").read_text(encoding="utf-8")
    assert "FindLocalResults" in (root / "ExamHub.cs").read_text(encoding="utf-8")
    assert "*_results.docx" in (root / "MosDock.csproj").read_text(encoding="utf-8")
    assert "1.1 / 1.3" not in demo
    grade = (root / "WordGrade.cs").read_text(encoding="utf-8")
    assert "QMatrix.Attach" in grade
    assert "count_footnotes" in (root / "QMatrix.cs").read_text(encoding="utf-8")
    login = (root / "LoginForm.cs").read_text(encoding="utf-8")
    portal = (root / "Portal.cs").read_text(encoding="utf-8")
    assert "Tài khoản nhà trường." not in login
    assert "ClassifyLogin" in portal
    confirm = (root / "ConfirmSubmitForm.cs").read_text(encoding="utf-8")
    assert "Xác nhận nộp bài thi" in confirm
    assert "Quay lại làm tiếp" in confirm
    lockf = (root / "LockedFile.cs").read_text(encoding="utf-8")
    assert "FileShare.ReadWrite" in lockf
    assert "LockedFile.ReadAllBytes" in portal
    assert 'MyAppVersion "1.21.0"' in (
        Path(__file__).resolve().parent.parent / "desktop" / "installer" / "windows" / "mosdock.iss"
    ).read_text(encoding="utf-8")


def _autofit_columns(inner_w: int, min_w: int, gap: int) -> int:
    span = max(1, inner_w)
    cell = max(1, min_w + gap)
    return max(1, (span + gap) // cell)


def _autofit_card_width(inner_w: int, min_w: int, count: int, gap: int) -> int:
    cols = min(max(1, count), _autofit_columns(inner_w, min_w, gap))
    while cols > 1:
        width = (inner_w - gap * (cols - 1)) // cols
        if width >= min_w and cols * width + (cols - 1) * gap <= inner_w:
            return width
        cols -= 1
    return max(min_w, inner_w)


def _widget_stack(inner_w: int) -> bool:
    return inner_w < 1024 or inner_w < 400 + 2 * 300 + 2 * 24


def test_dashboard_autofit_keeps_powerpoint():
    # 3×250 + 2×16 = 782 — PowerPoint stays on row 1
    assert _autofit_columns(800, 250, 16) == 3
    assert _autofit_columns(782, 250, 16) == 3
    assert _autofit_columns(781, 250, 16) == 2
    assert _autofit_card_width(800, 250, 3, 16) >= 250
    assert 3 * _autofit_card_width(800, 250, 3, 16) + 32 <= 800
    # old FitWrapRow bug: 3 theoretical columns but tiles overflow and clip
    assert min(3, _autofit_columns(1200, 250, 16)) == 3
    assert not _widget_stack(1280)
    assert _widget_stack(1024)
    assert _widget_stack(900)


def test_word_window_only_docks_current_exam():
    from pathlib import Path

    root = Path(__file__).resolve().parent.parent / "desktop" / "MosDock"
    win = (root / "WordWindow.cs").read_text(encoding="utf-8")
    com = (root / "WordCom.cs").read_text(encoding="utf-8")
    probe = (root / "WordActionProbe.cs").read_text(encoding="utf-8")
    assert "ResolveExamWindow" in win
    assert "TitleMatchesExam" in win
    assert "CancelPlace" in win
    assert "SWP_NOZORDER" in win
    assert "SW_SHOWNOACTIVATE" in win
    assert "ForceBounds(hwnd, target)" in win
    assert "bool activate" in com
    assert "if (activate)" in com
    assert "ActiveDocument.FullName" in probe
    stem = "Word_6-2"
    assert stem in "Word_6-2 - Saved"
    assert stem not in "Word_1-1 - Word"


def test_brand_icon_is_multi_size_ico():
    from pathlib import Path
    import struct

    root = Path(__file__).resolve().parent.parent
    ico = (root / "app" / "static" / "favicon.ico").read_bytes()
    assert ico[:4] == b"\x00\x00\x01\x00"
    count = struct.unpack_from("<H", ico, 4)[0]
    assert count >= 6
    png = root / "app" / "static" / "kulkul.png"
    assert png.is_file() and png.stat().st_size > 1000
    assert (root / "desktop" / "MosDock" / "Assets" / "kulkul.ico").stat().st_size == len(ico)


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
    assert mini["dock"]["h"] == 68
    assert mini["dock"]["w"] == 1920
    assert mini["word"]["h"] == 972
    compact = c.get("/api/layout", params={"state": "bottom", "w": 1920, "h": 1040, "compact": 1}).json()
    assert compact["compact"] is True
    assert compact["dock"]["h"] == 68
    assert compact["dock"]["w"] == 1920
    assert compact["word"]["h"] == 972
    assert compact["dock"]["y"] == compact["word"]["h"]
    assert compact["fit"] == 1.0
    help_box = compact["help"]
    assert help_box["w"] == 1920
    assert help_box["h"] == 297
    assert help_box["h"] <= 1040 * 36 / 100
    left_c = c.get("/api/layout", params={"state": "left", "w": 1920, "h": 1040, "compact": 1}).json()
    assert left_c["dock"]["w"] == 72
    assert left_c["dock"]["h"] == 1040
    assert left_c["word"]["x"] == 72
    assert left_c["help"]["h"] == 1040
    assert left_c["help"]["w"] <= 1920 * 36 / 100
    sized = c.get(
        "/api/layout",
        params={"state": "bottom", "w": 1920, "h": 1040, "compact": 1, "thickness": 120},
    ).json()
    assert sized["dock"]["h"] == 120
    assert sized["dock"]["w"] == 1920
    assert sized["word"]["h"] == 920
    assert sized["nav"]["thickness"] == 120


def test_kulkul_home_after_login():
    c = TestClient(app)
    r = c.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"}, follow_redirects=True)
    assert r.status_code == 200
    assert "Trung tâm chỉ huy MOS" in r.text
    assert "Trường GDS" not in r.text
    assert "mos.gds.edu.vn" in r.text
    assert "program-menu" not in r.text
    assert "word-sim" not in r.text
    assert "office.com" not in r.text.lower()
    assert "KulKul" in r.text
    assert "GMetrix" not in r.text
    assert "/quan-tri" in r.text
    assert "ic-app-header" in r.text
    assert "menu-canvas" in r.text
    assert "Bảng tin" in r.text
    assert "Lớp học" in r.text
    assert "Giám sát" in r.text
    assert "ic-course-nav" in r.text
    assert "ic-app-header" not in TestClient(app).get("/dang-nhap").text


def test_admin_sees_full_canvas_menus():
    c = TestClient(app)
    landed = c.post("/dang-nhap", data={"username": "admin", "password": "Mos@Gds2026"}, follow_redirects=False)
    assert landed.status_code == 303
    assert landed.headers["location"] == "/quan-tri"
    page = c.get("/quan-tri")
    assert page.status_code == 200
    assert "menu-canvas" in page.text
    assert "ic-app-header" in page.text
    assert "ic-course-nav" in page.text
    for label in ("Bảng tin", "Lớp học", "Bài tập", "Giám sát", "Phân tích", "Điểm", "Trợ giúp"):
        assert label in page.text
    for label in ("Tổng quan", "Học sinh", "Sư phạm lớp", "LTI 1.3"):
        assert label in page.text
    assert "inbox" not in page.text.lower()
    student = TestClient(app)
    student.post("/dang-nhap", data={"username": "hocsinh", "password": "Mos@Gds2026"})
    home = student.get("/")
    assert "menu-canvas" in home.text
    assert "Bảng tin" in home.text
    assert "Giám sát" not in home.text
    assert "ic-course-nav" not in home.text
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
