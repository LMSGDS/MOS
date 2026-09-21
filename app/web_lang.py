"""Chọn ngôn ngữ giao diện web (vi/en) và bộ lọc Jinja đọc rubric song ngữ.

Slug URL (/tien-do, /dang-nhap…) là định danh, KHÔNG đổi theo ngôn ngữ.
Ngôn ngữ giao diện chọn theo thứ tự:

    ?lang=en / ?ngon-ngu=EN-us  →  session đã lưu  →  cookie  →  Accept-Language  →  vi

Nối vào ứng dụng bằng ba dòng:

    from app.web_lang import install as install_lang, lang_ctx
    install_lang(TEMPLATES)
    data = {..., **lang_ctx(request)}          # trong _ctx()

rồi base.html đổi `<html lang="{{ lang }}">`.
"""
from __future__ import annotations

from urllib.parse import urlencode

from fastapi import Request
from fastapi.templating import Jinja2Templates

from app import i18n

LANGS = i18n.LANGS
DEFAULT_LANG = i18n.DEFAULT_LANG
QUERY_KEYS = ("lang", "ngon-ngu")
SESSION_KEY = "lang"
COOKIE_NAME = "mos_lang"
COOKIE_MAX_AGE = 60 * 60 * 24 * 365

LANG_LABELS = {"vi": "VI", "en": "EN"}
LANG_NAMES = {"vi": "Tiếng Việt", "en": "English"}


def _from_accept_language(header: str | None) -> str | None:
    if not header:
        return None
    best: tuple[float, str] | None = None
    for part in header.split(","):
        piece = part.strip()
        if not piece:
            continue
        code, _, q = piece.partition(";")
        weight = 1.0
        q = q.strip()
        if q.startswith("q="):
            try:
                weight = float(q[2:])
            except ValueError:
                weight = 0.0
        base = code.strip().casefold().replace("_", "-").split("-", 1)[0]
        if base in LANGS and (best is None or weight > best[0]):
            best = (weight, base)
    return best[1] if best else None


def resolve_lang(request: Request) -> str:
    """Trả về 'vi' hoặc 'en'. Lưu lựa chọn tường minh (?lang=) vào session."""
    for key in QUERY_KEYS:
        raw = request.query_params.get(key)
        if raw:
            lang = i18n.normalize_lang(raw)
            _remember(request, lang)
            return lang

    session = getattr(request, "session", None) if "session" in request.scope else None
    if isinstance(session, dict):
        saved = session.get(SESSION_KEY)
        if isinstance(saved, str) and saved in LANGS:
            return saved

    cookie = request.cookies.get(COOKIE_NAME)
    if cookie and cookie in LANGS:
        return cookie

    accepted = _from_accept_language(request.headers.get("accept-language"))
    if accepted:
        return accepted
    return DEFAULT_LANG


def _remember(request: Request, lang: str) -> None:
    if "session" in request.scope and isinstance(request.scope.get("session"), dict):
        request.scope["session"][SESSION_KEY] = lang
    request.state.lang_cookie = lang


def lang_urls(request: Request) -> dict[str, str]:
    """URL của trang hiện tại với ?lang=<code> cho từng ngôn ngữ."""
    out: dict[str, str] = {}
    params = [(k, v) for k, v in request.query_params.multi_items() if k not in QUERY_KEYS]
    for code in LANGS:
        query = urlencode(params + [("lang", code)])
        out[code] = f"{request.url.path}?{query}"
    return out


def lang_ctx(request: Request) -> dict:
    lang = resolve_lang(request)
    return {
        "lang": lang,
        "langs": list(LANGS),
        "lang_labels": LANG_LABELS,
        "lang_names": LANG_NAMES,
        "lang_urls": lang_urls(request),
    }


# ------------------------------------------------------------- Jinja filters
def t(value, lang: str = DEFAULT_LANG) -> str:
    """{{ criterion.prompt | t(lang) }} — chuỗi thuần hoặc khối {'vi','en'}."""
    return i18n.pick(value, lang)


def tlist(value, lang: str = DEFAULT_LANG) -> list[str]:
    """{% for s in criterion.help_steps | tlist(lang) %}"""
    return i18n.pick_list(value, lang)


def tmap(value, lang: str = DEFAULT_LANG) -> dict[str, str]:
    """{{ (criterion.feedback | tmap(lang)).fail }}"""
    return i18n.pick_map(value, lang)


def install(templates: Jinja2Templates) -> None:
    templates.env.filters["t"] = t
    templates.env.filters["tlist"] = tlist
    templates.env.filters["tmap"] = tmap
    from app.explore import bold

    templates.env.filters["bold"] = bold
    templates.env.globals.setdefault("LANGS", list(LANGS))
