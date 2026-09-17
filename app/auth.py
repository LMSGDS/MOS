"""PBKDF2 password hashing and JSON user store."""
from __future__ import annotations

import base64
import hashlib
import hmac
import json
from pathlib import Path

USERS_PATH = Path(__file__).resolve().parent.parent / "data" / "users.json"


def verify_password(password: str, stored: str) -> bool:
    try:
        algo, rounds_s, salt_b64, hash_b64 = stored.split("$", 3)
    except ValueError:
        return False
    if algo != "pbkdf2_sha256":
        return False
    rounds = int(rounds_s)
    salt = base64.b64decode(salt_b64.encode())
    expected = base64.b64decode(hash_b64.encode())
    got = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, rounds)
    return hmac.compare_digest(got, expected)


def load_users() -> list[dict]:
    if not USERS_PATH.exists():
        return []
    data = json.loads(USERS_PATH.read_text(encoding="utf-8"))
    return data.get("users", [])


def find_user(username: str) -> dict | None:
    uname = (username or "").strip().lower()
    for user in load_users():
        if user.get("username", "").lower() == uname:
            return user
    return None


def authenticate(username: str, password: str) -> dict | None:
    user = find_user(username)
    if not user:
        return None
    if not verify_password(password, user.get("password_hash", "")):
        return None
    return {
        "username": user["username"],
        "name": user.get("name") or user["username"],
        "role": user.get("role") or "user",
    }
