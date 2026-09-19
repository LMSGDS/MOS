"""JWT HS256 for MOS-KulKul desktop clients. Stdlib only — no PyJWT required to boot."""
from __future__ import annotations

import base64
import hashlib
import hmac
import json
import os
import time
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parent.parent


def _secret() -> str:
    env = os.environ.get("MOS_JWT_SECRET") or os.environ.get("MOS_SESSION_SECRET")
    if env:
        return env
    path = ROOT / "data" / "session.secret"
    if path.exists():
        return path.read_text(encoding="utf-8").strip()
    return "mos-kulkul-dev-jwt"


def _b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode("ascii")


def _b64url_json(obj: Any) -> str:
    return _b64url(json.dumps(obj, separators=(",", ":"), ensure_ascii=False).encode("utf-8"))


def _b64url_decode(part: str) -> bytes:
    pad = "=" * (-len(part) % 4)
    return base64.urlsafe_b64decode(part + pad)


def issue(user: dict[str, Any], ttl_sec: int = 12 * 3600) -> str:
    now = int(time.time())
    header = _b64url_json({"alg": "HS256", "typ": "JWT"})
    payload = _b64url_json(
        {
            "sub": user["username"],
            "name": user.get("name") or user["username"],
            "role": user.get("role") or "student",
            "uid": user.get("id"),
            "iat": now,
            "exp": now + ttl_sec,
        }
    )
    sig = hmac.new(
        _secret().encode("utf-8"),
        f"{header}.{payload}".encode("ascii"),
        hashlib.sha256,
    ).digest()
    return f"{header}.{payload}.{_b64url(sig)}"


def decode(token: str) -> dict[str, Any] | None:
    try:
        header_b64, payload_b64, sig_b64 = token.split(".")
        expected = hmac.new(
            _secret().encode("utf-8"),
            f"{header_b64}.{payload_b64}".encode("ascii"),
            hashlib.sha256,
        ).digest()
        if not hmac.compare_digest(expected, _b64url_decode(sig_b64)):
            return None
        header = json.loads(_b64url_decode(header_b64))
        if header.get("alg") != "HS256":
            return None
        data = json.loads(_b64url_decode(payload_b64))
    except (ValueError, json.JSONDecodeError, UnicodeDecodeError):
        return None
    if not isinstance(data, dict) or not data.get("sub"):
        return None
    exp = data.get("exp")
    if isinstance(exp, (int, float)) and int(exp) < int(time.time()):
        return None
    return data
