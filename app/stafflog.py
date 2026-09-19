"""Dấu vết số giáo viên trên web — phiên, trang, heartbeat (không ghi phím)."""
from __future__ import annotations

import secrets
from datetime import datetime, timezone

from app.db import cursor

STAFF_ROLES = ("admin", "teacher", "leadership")


def _now():
    return datetime.now(timezone.utc)


def open_staff_session(user_id: int, client: str = "web") -> str | None:
    if not user_id:
        return None
    sid = secrets.token_hex(12)
    try:
        with cursor() as cur:
            cur.execute(
                """
                INSERT INTO staff_sessions (id, user_id, client)
                VALUES (%s, %s, %s)
                """,
                (sid, user_id, client),
            )
            cur.execute(
                """
                INSERT INTO staff_events (user_id, session_id, event, path)
                VALUES (%s, %s, 'login', '/dang-nhap')
                """,
                (user_id, sid),
            )
        return sid
    except Exception:
        return None


def record_staff_event(user_id: int, event: str, path: str = "", session_id: str | None = None) -> None:
    if not user_id or not event:
        return
    try:
        with cursor() as cur:
            cur.execute(
                """
                INSERT INTO staff_events (user_id, session_id, event, path)
                VALUES (%s, %s, %s, %s)
                """,
                (user_id, session_id, event, path or ""),
            )
            if session_id:
                cur.execute(
                    "UPDATE staff_sessions SET last_seen_at = now() WHERE id = %s",
                    (session_id,),
                )
            else:
                cur.execute(
                    """
                    UPDATE staff_sessions
                    SET last_seen_at = now()
                    WHERE user_id = %s AND ended_at IS NULL
                    """,
                    (user_id,),
                )
    except Exception:
        return


def touch_from_username(username: str, event: str, path: str) -> None:
    uname = (username or "").strip().lower()
    if not uname:
        return
    try:
        with cursor() as cur:
            cur.execute(
                "SELECT id, role FROM users WHERE username = %s",
                (uname,),
            )
            row = cur.fetchone()
        if not row or row.get("role") not in STAFF_ROLES:
            return
        record_staff_event(row["id"], event, path)
    except Exception:
        return
