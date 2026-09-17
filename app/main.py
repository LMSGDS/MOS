"""Cổng MOS — đăng nhập và mở Microsoft Word trên máy cá nhân."""
from __future__ import annotations

import os
import secrets
from pathlib import Path

from fastapi import FastAPI, Form, Request
from fastapi.responses import FileResponse, HTMLResponse, RedirectResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from starlette.middleware.sessions import SessionMiddleware

from app.auth import authenticate
from app.gmetrix_layout import Rect, compute

ROOT = Path(__file__).resolve().parent.parent
TEMPLATES = Jinja2Templates(directory=str(ROOT / "app" / "templates"))
STATIC = ROOT / "app" / "static"
DOCX = STATIC / "mau-van-ban.docx"

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


SESSION_SECRET = _session_secret()

app = FastAPI(title="MOS GDS", docs_url=None, redoc_url=None)
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


@app.get("/healthz")
def healthz():
    return {"ok": True, "service": "mos"}


def _is_dock(request: Request) -> bool:
    q = request.query_params.get("che-do")
    if q:
        request.session["che_do"] = q
    return request.session.get("che_do") == "dock"


@app.get("/api/layout")
def api_layout(state: str = "bottom", x: int = 0, y: int = 0, w: int = 1920, h: int = 1040):
    dock, word = compute(Rect(x, y, w, h), state)
    return {
        "state": state,
        "dock": {"x": dock.x, "y": dock.y, "w": dock.w, "h": dock.h},
        "word": {"x": word.x, "y": word.y, "w": word.w, "h": word.h},
    }


@app.get("/", response_class=HTMLResponse)
def home(request: Request):
    user = current_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    template = "dock_content.html" if _is_dock(request) else "gmetrix.html"
    return TEMPLATES.TemplateResponse(
        request,
        template,
        {"user": user, "host": request.headers.get("host", "mos.gds.edu.vn")},
    )


@app.get("/khung/word", response_class=HTMLResponse)
def word_frame(request: Request):
    user = current_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    return TEMPLATES.TemplateResponse(request, "word.html", {"user": user})


@app.get("/dang-nhap", response_class=HTMLResponse)
def login_form(request: Request, loi: str | None = None):
    che_do = request.query_params.get("che-do")
    if che_do:
        request.session["che_do"] = che_do
    if current_user(request):
        dest = "/?che-do=dock" if request.session.get("che_do") == "dock" else "/"
        return RedirectResponse(dest, status_code=303)
    return TEMPLATES.TemplateResponse(
        request,
        "login.html",
        {"error": loi},
    )


@app.post("/dang-nhap")
def login(request: Request, username: str = Form(...), password: str = Form(...)):
    user = authenticate(username, password)
    if not user:
        return RedirectResponse("/dang-nhap?loi=sai", status_code=303)
    request.session["user"] = user
    dest = "/?che-do=dock" if request.session.get("che_do") == "dock" else "/"
    return RedirectResponse(dest, status_code=303)


@app.post("/dang-xuat")
def logout(request: Request):
    request.session.clear()
    return RedirectResponse("/dang-nhap", status_code=303)


@app.get("/files/mau-van-ban.docx")
def download_template(request: Request):
    if not current_user(request):
        return RedirectResponse("/dang-nhap", status_code=303)
    return FileResponse(
        DOCX,
        media_type="application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        filename="mau-van-ban-mos.docx",
        headers={"Cache-Control": "no-store"},
    )
