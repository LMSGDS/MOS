"""PostgreSQL connection and schema bootstrap for MOS-KulKul."""
from __future__ import annotations

import os
from contextlib import contextmanager
from contextvars import ContextVar
from pathlib import Path

import psycopg
from psycopg.rows import dict_row

ROOT = Path(__file__).resolve().parent.parent
SCHEMA = Path(__file__).resolve().parent / "schema.sql"

# Default service: pytest / seed / engine. HTTP middleware sets student|teacher|admin|anon.
_persona: ContextVar[str] = ContextVar("mos_persona", default="service")
_user_id: ContextVar[str] = ContextVar("mos_user_id", default="")


def database_url() -> str:
    return os.environ.get(
        "DATABASE_URL",
        "postgresql://mos:mos@127.0.0.1:5432/mos",
    )


def bind_request_identity(user: dict | None):
    """Gắn persona RLS cho request hiện tại. Trả token để reset."""
    from app.roles import persona as persona_of

    role = persona_of(user) if user else "anon"
    if not role:
        role = "anon"
    uid = ""
    if user and user.get("id") is not None:
        uid = str(user["id"])
    return _persona.set(role), _user_id.set(uid)


def reset_identity(tokens) -> None:
    _persona.reset(tokens[0])
    _user_id.reset(tokens[1])


@contextmanager
def as_service():
    """Engine chấm / compile payload: đọc ngân hàng dù học sinh đang gọi API."""
    token = _persona.set("service")
    try:
        yield
    finally:
        _persona.reset(token)


def _apply_app_role(conn) -> None:
    """Hạ xuống mos_app để superuser CI không bypass FORCE RLS."""
    try:
        row = conn.execute("SELECT 1 FROM pg_roles WHERE rolname = 'mos_app'").fetchone()
        if not row:
            return
        conn.execute("SET ROLE mos_app")
    except Exception:
        try:
            conn.rollback()
        except Exception:
            pass


def connect() -> psycopg.Connection:
    conn = psycopg.connect(
        database_url(),
        row_factory=dict_row,
        autocommit=False,
        client_encoding="UTF8",
    )
    _apply_app_role(conn)
    return conn


@contextmanager
def cursor():
    conn = connect()
    try:
        with conn.cursor() as cur:
            cur.execute("SELECT set_config('app.persona', %s, true)", (_persona.get(),))
            cur.execute("SELECT set_config('app.user_id', %s, true)", (_user_id.get(),))
            yield cur
        conn.commit()
    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()


def init_schema() -> None:
    sql = SCHEMA.read_text(encoding="utf-8")
    conn = psycopg.connect(database_url(), autocommit=True, client_encoding="UTF8")
    try:
        conn.execute(sql)
    finally:
        conn.close()
