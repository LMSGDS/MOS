"""Cổng MOS — đăng nhập và mở Microsoft Word trên máy cá nhân."""
from __future__ import annotations

import hashlib
import os
import secrets
import zipfile
from pathlib import Path

from contextlib import asynccontextmanager

from fastapi import FastAPI, Form, Request
from fastapi.responses import FileResponse, HTMLResponse, JSONResponse, PlainTextResponse, RedirectResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from starlette.middleware.sessions import SessionMiddleware

from app.admin import router as admin_router
from app.auth import authenticate
from app.client_v1 import router as client_v1_router
from app.hooks import router as hooks_router
from app.kulkul_layout import Rect, compute, grow_for_help, measure
from app.progress_api import router as progress_router
from app.programs import MENU, normalize, resolve

ROOT = Path(__file__).resolve().parent.parent
TEMPLATES = Jinja2Templates(directory=str(ROOT / "app" / "templates"))
STATIC = ROOT / "app" / "static"

def _session_secret() -> str:
    env = os.environ.get("MOS_SESSION_SECRET")
    if env:
        return env
    path = ROOT / "data" / "session.secret"
    if path.exists():
        return path.read_text(encoding="utf-8").strip()
    value = secrets.token_hex(32)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(value + "\n", encoding="utf-8")
    path.chmod(0o600)
    return value


ASSET_V = os.environ.get("MOS_ASSET_V", "kulkul9")
SESSION_SECRET = _session_secret()

@asynccontextmanager
async def lifespan(_app: FastAPI):
    try:
        from app.db import init_schema
        from app.seed import seed

        init_schema()
        seed()
    except Exception as exc:
        print("PostgreSQL chua san sang:", exc)
    yield


app = FastAPI(title="MOS-KulKul", docs_url=None, redoc_url=None, lifespan=lifespan)
app.include_router(client_v1_router)
app.include_router(progress_router)
app.include_router(hooks_router)
app.include_router(admin_router)
app.add_middleware(
    SessionMiddleware,
    secret_key=SESSION_SECRET,
    session_cookie="mos_session",
    same_site="lax",
    https_only=os.environ.get("MOS_HTTPS_ONLY", "0") == "1",
    max_age=60 * 60 * 12,
)
app.mount("/static", StaticFiles(directory=str(STATIC)), name="static")


@app.middleware("http")
async def frame_same_origin(request, call_next):
    response = await call_next(request)
    response.headers["X-Frame-Options"] = "SAMEORIGIN"
    response.headers["Content-Security-Policy"] = "frame-ancestors 'self'"
    return response


def current_user(request: Request) -> dict | None:
    user = request.session.get("user")
    return user if isinstance(user, dict) else None


def current_program(request: Request) -> dict:
    q = request.query_params.get("chuong-trinh") or request.query_params.get("app")
    if q:
        request.session["chuong_trinh"] = normalize(q)
    return resolve(request.session.get("chuong_trinh"))


def _ctx(request: Request, extra: dict | None = None) -> dict:
    data = {
        "user": current_user(request),
        "host": request.headers.get("host", "mos.gds.edu.vn"),
        "program": current_program(request),
        "programs": MENU,
        "asset_v": ASSET_V,
    }
    if extra:
        data.update(extra)
    return data


@app.get("/healthz")
def healthz():
    postgres = False
    try:
        from app.db import connect

        with connect() as conn:
            conn.execute("SELECT 1")
        postgres = True
    except Exception:
        postgres = False
    return {"ok": True, "service": "mos", "postgres": postgres}


def _is_dock(request: Request) -> bool:
    q = request.query_params.get("che-do")
    if q:
        request.session["che_do"] = q
    return request.session.get("che_do") == "dock"


@app.get("/api/chuong-trinh")
def api_programs():
    return {"ok": True, "programs": MENU}


@app.get("/api/me")
def api_me(request: Request):
    user = current_user(request)
    if not user:
        return JSONResponse({"ok": False, "error": "chua-dang-nhap"}, status_code=401)
    return {"ok": True, "user": user, "program": current_program(request)}


@app.post("/api/dang-nhap")
async def api_login(request: Request):
    username = ""
    password = ""
    chuong = "word"
    ctype = (request.headers.get("content-type") or "").lower()
    if "json" in ctype:
        data = await request.json()
        if isinstance(data, dict):
            username = str(data.get("username") or "")
            password = str(data.get("password") or "")
            chuong = str(data.get("chuong_trinh") or "word")
    else:
        form = await request.form()
        username = str(form.get("username") or "")
        password = str(form.get("password") or "")
        chuong = str(form.get("chuong_trinh") or "word")
    request.session["chuong_trinh"] = normalize(chuong)
    request.session["che_do"] = "dock"
    user = authenticate(username, password)
    if not user:
        return JSONResponse({"ok": False, "error": "sai"}, status_code=401)
    request.session["user"] = user
    return {"ok": True, "user": user, "program": resolve(chuong)}


@app.get("/api/layout")
def api_layout(
    state: str = "bottom",
    x: int = 0,
    y: int = 0,
    w: int = 1920,
    h: int = 1040,
    compact: bool = False,
):
    dock, word = compute(Rect(x, y, w, h), state, compact=compact)
    nav = measure(Rect(x, y, w, h))
    compact_on = compact or state == "minimized"
    help_box = grow_for_help(dock, Rect(x, y, w, h), state) if compact_on else dock
    return {
        "state": state,
        "compact": compact_on,
        "fit": nav.fit,
        "dock": {"x": dock.x, "y": dock.y, "w": dock.w, "h": dock.h},
        "word": {"x": word.x, "y": word.y, "w": word.w, "h": word.h},
        "help": {"x": help_box.x, "y": help_box.y, "w": help_box.w, "h": help_box.h},
        "nav": {
            "cluster_w": nav.cluster_w,
            "cluster_h": nav.cluster_h,
            "help_w": nav.help_w,
            "help_h": nav.help_h,
            "icon": nav.icon,
            "margin": nav.margin,
        },
    }


@app.get("/", response_class=HTMLResponse)
def home(request: Request):
    user = current_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    template = "dock_content.html" if _is_dock(request) else "portal.html"
    return TEMPLATES.TemplateResponse(request, template, _ctx(request))


@app.get("/khung/word", response_class=HTMLResponse)
@app.get("/khung/office", response_class=HTMLResponse)
def office_frame(request: Request):
    user = current_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    return TEMPLATES.TemplateResponse(request, "word.html", _ctx(request))


@app.get("/dang-nhap", response_class=HTMLResponse)
def login_form(request: Request, loi: str | None = None):
    che_do = request.query_params.get("che-do")
    if che_do:
        request.session["che_do"] = che_do
    program = current_program(request)
    if current_user(request):
        dest = "/?che-do=dock" if request.session.get("che_do") == "dock" else "/"
        dest += f"{'&' if '?' in dest else '?'}chuong-trinh={program['id']}"
        return RedirectResponse(dest, status_code=303)
    return TEMPLATES.TemplateResponse(
        request,
        "login.html",
        _ctx(request, {"error": loi}),
    )


@app.post("/dang-nhap")
def login(
    request: Request,
    username: str = Form(...),
    password: str = Form(...),
    chuong_trinh: str = Form("word"),
):
    request.session["chuong_trinh"] = normalize(chuong_trinh)
    user = authenticate(username, password)
    if not user:
        return RedirectResponse("/dang-nhap?loi=sai", status_code=303)
    request.session["user"] = user
    dest = "/?che-do=dock" if request.session.get("che_do") == "dock" else "/"
    dest += f"{'&' if '?' in dest else '?'}chuong-trinh={normalize(chuong_trinh)}"
    return RedirectResponse(dest, status_code=303)


@app.get("/cai-dat", response_class=HTMLResponse)
def install_page(request: Request):
    return TEMPLATES.TemplateResponse(
        request,
        "install.html",
        _ctx(request, {"installers": _installer_meta()}),
    )


INSTALLER_DIR = ROOT / "data" / "installers"
_HASH_CACHE: dict[str, tuple[float, int, str]] = {}

INSTALL_README = """MOS-KulKul — Trường GDS (mos.gds.edu.vn)

Không tải .exe/.zip bằng Chrome/Edge: trình duyệt luôn quét virus vì bộ cài
chưa mua chữ ký Authenticode (không phải mã độc).

Cách nên dùng — PowerShell (không đi qua thanh tải trình duyệt):
  powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://mos.gds.edu.vn/cai-dat/windows.ps1 | iex"

Nếu đã giải nén file này:
1. Chuột phải file .exe → Thuộc tính → bỏ chọn Chặn / Bỏ chặn → OK.
   PowerShell: Unblock-File .\\MOS-KulKul-Setup-Windows.exe
2. Nếu SmartScreen «Windows đã bảo vệ máy tính»: Thông tin thêm → Chạy anyway.
3. Chrome «Tệp không phổ biến»: Giữ lại / Keep.

Không tắt antivirus của nhà trường. Chỉ tải từ https://mos.gds.edu.vn/cai-dat
"""


def sha256_path(path: Path) -> str:
    st = path.stat()
    key = str(path.resolve())
    hit = _HASH_CACHE.get(key)
    if hit and hit[0] == st.st_mtime and hit[1] == st.st_size:
        return hit[2]
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    hexed = digest.hexdigest()
    _HASH_CACHE[key] = (st.st_mtime, st.st_size, hexed)
    return hexed


def wrap_installer_zip(source: Path, dest: Path | None = None) -> Path:
    dest = dest or source.with_suffix(".zip")
    if dest.is_file() and dest.stat().st_mtime >= source.stat().st_mtime and dest.stat().st_size > 22:
        return dest
    dest.parent.mkdir(parents=True, exist_ok=True)
    tmp = dest.with_name(dest.name + ".partial")
    with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED) as zf:
        zf.write(source, source.name)
        zf.writestr("HUONG-DAN-CAI.txt", INSTALL_README)
    tmp.replace(dest)
    return dest


def _installer_file(*names: str) -> Path | None:
    for name in names:
        path = INSTALLER_DIR / name
        if path.is_file() and path.stat().st_size > 0:
            return path
    return None


def _installer_meta() -> list[dict]:
    names = (
        "MOS-KulKul-Setup-Windows.zip",
        "MOS-KulKul-Setup-Windows.exe",
        "MOS-KulKul-Setup-Windows-Full.zip",
        "MOS-KulKul-Setup-Windows-Full.exe",
        "MOS-KulKul-Setup-macOS.zip",
    )
    rows = []
    for name in names:
        path = INSTALLER_DIR / name
        if not path.is_file() or path.stat().st_size <= 0:
            continue
        rows.append({"name": name, "size": path.stat().st_size, "sha256": sha256_path(path)})
    return rows


def _send_installer(*names: str, media: str | None = None, as_zip: bool = False):
    path = _installer_file(*names)
    if path is None:
        listed = " / ".join(names)
        return HTMLResponse(
            f"Chưa có {listed} trên server. Copy artifact CI vào data/installers/.",
            status_code=404,
        )
    if as_zip and path.suffix.lower() != ".zip":
        path = wrap_installer_zip(path)
    chosen = media
    if path.suffix == ".zip":
        chosen = "application/zip"
    elif path.suffix == ".exe":
        chosen = "application/octet-stream"
    elif path.suffix == ".pkg":
        chosen = "application/octet-stream"
    download_name = path.name
    if path.suffix.lower() == ".exe":
        download_name = path.stem + "-GDS.exe"
    elif path.suffix.lower() == ".zip" and "Windows" in path.name:
        download_name = path.stem + "-GDS.zip"
    return FileResponse(
        path,
        media_type=chosen or "application/octet-stream",
        filename=download_name,
        headers={
            "X-Content-Type-Options": "nosniff",
            "X-Download-Options": "noopen",
            "Cache-Control": "private, max-age=120",
        },
    )


@app.get("/cai-dat/windows")
def install_windows():
    """Bộ cài nhỏ (web stub). Khi chạy sẽ tải bản đầy đủ từ /cai-dat/windows-full."""
    return _send_installer(
        "MOS-KulKul-Setup-Windows.exe",
        "MOS-Dock-Setup-Windows.exe",
    )


@app.get("/cai-dat/windows.zip")
def install_windows_zip():
    """Gói ZIP — trình duyệt không chặn .exe khi tải."""
    return _send_installer(
        "MOS-KulKul-Setup-Windows.zip",
        "MOS-KulKul-Setup-Windows.exe",
        "MOS-Dock-Setup-Windows.exe",
        as_zip=True,
    )


@app.get("/cai-dat/windows-full")
def install_windows_full():
    """Bản cài đầy đủ (~50MB) — stub và máy offline tải từ đây."""
    return _send_installer(
        "MOS-KulKul-Setup-Windows-Full.exe",
    )


@app.get("/cai-dat/windows-full.zip")
def install_windows_full_zip():
    return _send_installer(
        "MOS-KulKul-Setup-Windows-Full.zip",
        "MOS-KulKul-Setup-Windows-Full.exe",
        as_zip=True,
    )


@app.get("/cai-dat/checksums")
def install_checksums():
    return JSONResponse({"ok": True, "files": _installer_meta()})


@app.get("/cai-dat/macos")
def install_macos():
    return _send_installer(
        "MOS-KulKul-Setup-macOS.zip",
        "MOS-Dock-Setup-macOS.zip",
        "MOS-Dock-Setup-macOS.pkg",
        media="application/zip",
    )


@app.get("/cai-dat/macos-pkg")
def install_macos_pkg():
    return _send_installer(
        "MOS-Dock-Setup-macOS.pkg",
        media="application/octet-stream",
    )


@app.get("/cai-dat/macos.sh")
def install_macos_sh():
    path = ROOT / "desktop" / "installer" / "macos" / "install.sh"
    return PlainTextResponse(path.read_text(encoding="utf-8"), media_type="text/plain; charset=utf-8")


@app.get("/cai-dat/windows.ps1")
def install_windows_ps1():
    path = ROOT / "desktop" / "installer" / "windows" / "bootstrap.ps1"
    return PlainTextResponse(
        path.read_text(encoding="utf-8"),
        media_type="text/plain; charset=utf-8",
        headers={"X-Content-Type-Options": "nosniff"},
    )


_MACOS_FILES = {
    "mosdock_mac.py": ROOT / "desktop" / "MosDockMac" / "mosdock_mac.py",
    "handler.applescript": ROOT / "desktop" / "MosDockMac" / "handler.applescript",
    "Info.plist": ROOT / "desktop" / "MosDockMac" / "Info.plist.url.fragment",
}


@app.get("/cai-dat/macos-files/{name}")
def install_macos_file(name: str):
    path = _MACOS_FILES.get(name)
    if path is None or not path.is_file():
        return HTMLResponse("Not found", status_code=404)
    return FileResponse(path, filename=name)


@app.post("/dang-xuat")
def logout(request: Request):
    request.session.clear()
    return RedirectResponse("/dang-nhap", status_code=303)


@app.get("/files/mau-van-ban.docx")
@app.get("/files/mau")
def download_template(request: Request):
    if not current_user(request):
        return RedirectResponse("/dang-nhap", status_code=303)
    program = current_program(request)
    path = STATIC / program["template"]
    return FileResponse(
        path,
        media_type=program["mime"],
        filename=program["download"],
        headers={"Cache-Control": "no-store"},
    )
