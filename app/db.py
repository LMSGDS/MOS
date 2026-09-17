"""PostgreSQL connection and schema bootstrap for MOS-KulKul."""
from __future__ import annotations

import os
from contextlib import contextmanager
from pathlib import Path

import psycopg
from psycopg.rows import dict_row

ROOT = Path(__file__).resolve().parent.parent
SCHEMA = Path(__file__).resolve().parent / "schema.sql"


def database_url() -> str:
    return os.environ.get(
        "DATABASE_URL",
        "postgresql://mos:mos@127.0.0.1:5432/mos",
    )


def connect() -> psycopg.Connection:
    return psycopg.connect(
        database_url(),
        row_factory=dict_row,
        autocommit=False,
        client_encoding="UTF8",
    )


@contextmanager
def cursor():
    conn = connect()
    try:
        with conn.cursor() as cur:
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
