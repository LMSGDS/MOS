"""JWT for MOS-KulKul desktop clients."""
from __future__ import annotations

import os
import time
from pathlib import Path
from typing import Any

import jwt

ROOT = Path(__file__).resolve().parent.parent


def _secret() -> str:
    env = os.environ.get("MOS_JWT_SECRET") or os.environ.get("MOS_SESSION_SECRET")
    if env:
        return env
    path = ROOT / "data" / "session.secret"
    if path.exists():
        return path.read_text(encoding="utf-8").strip()
    return "mos-kulkul-dev-jwt"


def issue(user: dict[str, Any], ttl_sec: int = 12 * 3600) -> str:
    now = int(time.time())
    payload = {
        "sub": user["username"],
        "name": user.get("name") or user["username"],
        "role": user.get("role") or "student",
        "uid": user.get("id"),
        "iat": now,
        "exp": now + ttl_sec,
    }
    return jwt.encode(payload, _secret(), algorithm="HS256")


def decode(token: str) -> dict[str, Any] | None:
    try:
        data = jwt.decode(token, _secret(), algorithms=["HS256"])
    except jwt.PyJWTError:
        return None
    if not isinstance(data, dict) or not data.get("sub"):
        return None
    return data
