"""Command Center — Roster: import CSV, mã vào lớp, reset mật khẩu hàng loạt.

Không SSO Google/Microsoft riêng: trường có Canvas dùng LTI; phòng máy dùng reset hàng loạt.
"""
from __future__ import annotations

import csv
import io
import secrets
import string

from app.auth import hash_password
from app.db import cursor


def _uname(code: str, name: str) -> str:
    raw = (code or "").strip().lower() or "".join(ch for ch in (name or "").lower() if ch.isalnum())[:12]
    return raw or f"hs{secrets.token_hex(3)}"


def _password(n: int = 8) -> str:
    alphabet = string.ascii_letters + string.digits
    return "".join(secrets.choice(alphabet) for _ in range(n))


def ensure_join_code(class_id: int) -> str:
    with cursor() as cur:
        cur.execute("SELECT join_code FROM classes WHERE id = %s", (class_id,))
        row = cur.fetchone()
        if not row:
            raise ValueError("class")
        if row.get("join_code"):
            return row["join_code"]
        code = secrets.token_hex(3).upper()
        cur.execute("UPDATE classes SET join_code = %s WHERE id = %s", (code, class_id))
        return code


def rotate_join_code(class_id: int) -> str:
    code = secrets.token_hex(3).upper()
    with cursor() as cur:
        cur.execute("UPDATE classes SET join_code = %s WHERE id = %s RETURNING join_code", (code, class_id))
        row = cur.fetchone()
    if not row:
        raise ValueError("class")
    return row["join_code"]


def join_by_code(user_id: int, code: str) -> dict:
    token = (code or "").strip().upper()
    if not token:
        raise ValueError("code")
    with cursor() as cur:
        cur.execute("SELECT id, name FROM classes WHERE upper(join_code) = %s", (token,))
        klass = cur.fetchone()
        if not klass:
            raise ValueError("code")
        cur.execute(
            "INSERT INTO enrollments (class_id, user_id) VALUES (%s, %s) ON CONFLICT DO NOTHING",
            (klass["id"], user_id),
        )
    return {"ok": True, "class_id": klass["id"], "class_name": klass["name"]}


def import_csv(text: str, *, class_id: int | None, default_password: str | None = None) -> dict:
    """One-click: cột tên / mã HS / tài khoản (tuỳ chọn)."""
    sample = text.lstrip("\ufeff")
    first = (sample.splitlines() or [""])[0]
    delimiter = ";" if first.count(";") > first.count(",") and first.count(";") > first.count("\t") else ","
    if first.count("\t") > first.count(delimiter):
        delimiter = "\t"
    reader = csv.DictReader(io.StringIO(sample), delimiter=delimiter)
    created, skipped, creds = [], [], []
    with cursor() as cur:
        for raw in reader:
            row = { (k or "").strip().lower(): (v or "").strip() for k, v in raw.items() }
            name = row.get("họ tên") or row.get("ho ten") or row.get("name") or row.get("ten") or ""
            code = row.get("mã hs") or row.get("ma hs") or row.get("student_code") or row.get("ma") or ""
            uname = (row.get("tài khoản") or row.get("tai khoan") or row.get("username") or "").lower() or _uname(code, name)
            if not name and not uname:
                continue
            cur.execute("SELECT id FROM users WHERE username = %s", (uname,))
            if cur.fetchone():
                skipped.append(uname)
                continue
            pwd = default_password or _password()
            cur.execute(
                """
                INSERT INTO users (username, name, role, password_hash, org_id, student_code)
                VALUES (%s, %s, 'student', %s, 1, %s)
                RETURNING id, username, name, student_code
                """,
                (uname, name or uname, hash_password(pwd), code or None),
            )
            person = cur.fetchone()
            if class_id:
                cur.execute(
                    "INSERT INTO enrollments (class_id, user_id) VALUES (%s, %s) ON CONFLICT DO NOTHING",
                    (class_id, person["id"]),
                )
            created.append(person["username"])
            creds.append({"username": person["username"], "password": pwd, "name": person["name"], "student_code": person["student_code"]})
    return {"ok": True, "created": len(created), "skipped": len(skipped), "accounts": creds}


def bulk_reset(class_id: int) -> list[dict]:
    creds = []
    with cursor() as cur:
        cur.execute(
            """
            SELECT u.id, u.username, u.name, u.student_code
            FROM enrollments e JOIN users u ON u.id = e.user_id
            WHERE e.class_id = %s AND u.role = 'student'
            ORDER BY u.name
            """,
            (class_id,),
        )
        rows = list(cur.fetchall())
        for row in rows:
            pwd = _password()
            cur.execute("UPDATE users SET password_hash = %s WHERE id = %s", (hash_password(pwd), row["id"]))
            creds.append({**row, "password": pwd})
    return creds


def classes_for(user: dict | None) -> list[dict]:
    role = (user or {}).get("role")
    uname = (user or {}).get("username")
    with cursor() as cur:
        if role == "teacher" and uname:
            cur.execute(
                """
                SELECT c.id, c.name, c.join_code, u.name AS teacher, c.teacher_id
                FROM classes c
                LEFT JOIN users u ON u.id = c.teacher_id
                JOIN users t ON t.id = c.teacher_id
                WHERE t.username = %s
                ORDER BY c.name
                """,
                (uname,),
            )
        else:
            cur.execute(
                """
                SELECT c.id, c.name, c.join_code, u.name AS teacher, c.teacher_id
                FROM classes c
                LEFT JOIN users u ON u.id = c.teacher_id
                ORDER BY c.name
                """
            )
        rows = list(cur.fetchall())
        for row in rows:
            if not row.get("join_code"):
                row["join_code"] = ensure_join_code(row["id"])
        return rows


def assign_teacher(class_id: int, teacher_id: int) -> None:
    with cursor() as cur:
        cur.execute("SELECT id, role FROM users WHERE id = %s", (teacher_id,))
        row = cur.fetchone()
        if not row or row["role"] != "teacher":
            raise ValueError("teacher")
        cur.execute("UPDATE classes SET teacher_id = %s WHERE id = %s", (teacher_id, class_id))


def list_teachers() -> list[dict]:
    with cursor() as cur:
        cur.execute("SELECT id, username, name FROM users WHERE role = 'teacher' ORDER BY name")
        return list(cur.fetchall())


CSV_TEMPLATE = "Họ tên,Mã HS,Tài khoản\nNguyễn Văn A,HS101,\nTrần Thị B,HS102,\n"


def reset_one(user_id: int, *, roles: tuple[str, ...] = ("student",)) -> dict | None:
    pwd = _password()
    with cursor() as cur:
        cur.execute(
            "SELECT id, username, name, student_code, role FROM users WHERE id = %s",
            (user_id,),
        )
        row = cur.fetchone()
        if not row or row["role"] not in roles:
            return None
        cur.execute("UPDATE users SET password_hash = %s WHERE id = %s", (hash_password(pwd), user_id))
    return {**dict(row), "password": pwd}


def staff_manages_student(staff: dict | None, user_id: int) -> bool:
    if not staff:
        return False
    if staff.get("role") in ("admin", "leadership"):
        return True
    allowed = {c["id"] for c in classes_for(staff)}
    if not allowed:
        return False
    with cursor() as cur:
        cur.execute("SELECT class_id FROM enrollments WHERE user_id = %s", (user_id,))
        return any(row["class_id"] in allowed for row in cur.fetchall())
