"""Đồng bộ tài khoản học sinh/giáo viên lên PostgreSQL (web + MOS-KulKul)."""
from __future__ import annotations

from app.auth import hash_password, load_users
from app.db import cursor

ALLOWED_ROLES = ("admin", "leadership", "teacher", "student")


def record_login(username: str, client: str) -> dict | None:
    """Mỗi lần đăng nhập web hoặc KulKul: ghi user vào PG và last_seen."""
    uname = (username or "").strip().lower()
    if not uname:
        return None
    source = "kulkul" if client == "kulkul" else "web"
    try:
        with cursor() as cur:
            cur.execute(
                "SELECT id, username, name, role, student_code FROM users WHERE username = %s",
                (uname,),
            )
            row = cur.fetchone()
            if not row:
                src = next(
                    (u for u in load_users() if (u.get("username") or "").lower() == uname),
                    None,
                )
                if not src:
                    return None
                cur.execute(
                    """
                    INSERT INTO users (username, name, role, password_hash, org_id, student_code)
                    VALUES (%s, %s, %s, %s, 1, %s)
                    ON CONFLICT (username) DO UPDATE SET
                      name = EXCLUDED.name,
                      role = EXCLUDED.role
                    RETURNING id, username, name, role, student_code
                    """,
                    (
                        src["username"],
                        src.get("name") or src["username"],
                        src.get("role") or "student",
                        src["password_hash"],
                        src.get("student_code"),
                    ),
                )
                row = cur.fetchone()
            cur.execute(
                """
                UPDATE users SET last_seen_at = now(), last_client = %s
                WHERE username = %s
                RETURNING id, username, name, role, student_code, last_seen_at, last_client
                """,
                (source, uname),
            )
            return cur.fetchone()
    except Exception:
        return None


def create_account(
    *,
    username: str,
    name: str,
    password: str,
    role: str = "student",
    student_code: str | None = None,
    class_id: int | None = None,
) -> dict:
    uname = (username or "").strip().lower()
    display = (name or "").strip() or uname
    role = (role or "student").strip().lower()
    if role not in ALLOWED_ROLES:
        raise ValueError("role")
    if not uname or not password:
        raise ValueError("username")
    code = (student_code or "").strip() or None
    with cursor() as cur:
        cur.execute("SELECT id FROM users WHERE username = %s", (uname,))
        if cur.fetchone():
            raise ValueError("exists")
        cur.execute(
            """
            INSERT INTO users (username, name, role, password_hash, org_id, student_code)
            VALUES (%s, %s, %s, %s, 1, %s)
            RETURNING id, username, name, role, student_code
            """,
            (uname, display, role, hash_password(password), code),
        )
        row = cur.fetchone()
        if class_id and role == "student":
            cur.execute("SELECT id FROM classes WHERE id = %s", (class_id,))
            if cur.fetchone():
                cur.execute(
                    """
                    INSERT INTO enrollments (class_id, user_id) VALUES (%s, %s)
                    ON CONFLICT DO NOTHING
                    """,
                    (class_id, row["id"]),
                )
    return dict(row)


def update_account(
    user_id: int,
    *,
    name: str,
    student_code: str | None = None,
    class_id: int | None = None,
    password: str | None = None,
) -> dict:
    display = (name or "").strip()
    if not display:
        raise ValueError("name")
    code = (student_code or "").strip() or None
    with cursor() as cur:
        cur.execute("SELECT id, role FROM users WHERE id = %s", (user_id,))
        row = cur.fetchone()
        if not row:
            raise ValueError("missing")
        cur.execute(
            """
            UPDATE users SET name = %s, student_code = %s
            WHERE id = %s
            RETURNING id, username, name, role, student_code
            """,
            (display, code, user_id),
        )
        updated = cur.fetchone()
        if password:
            cur.execute(
                "UPDATE users SET password_hash = %s WHERE id = %s",
                (hash_password(password), user_id),
            )
        if updated["role"] == "student":
            cur.execute("DELETE FROM enrollments WHERE user_id = %s", (user_id,))
            if class_id:
                cur.execute("SELECT id FROM classes WHERE id = %s", (class_id,))
                if cur.fetchone():
                    cur.execute(
                        """
                        INSERT INTO enrollments (class_id, user_id) VALUES (%s, %s)
                        ON CONFLICT DO NOTHING
                        """,
                        (class_id, user_id),
                    )
    return dict(updated)


def remove_student(user_id: int) -> dict:
    with cursor() as cur:
        cur.execute("SELECT id, username, name, role FROM users WHERE id = %s", (user_id,))
        row = cur.fetchone()
        if not row or row["role"] != "student":
            raise ValueError("student")
        cur.execute("DELETE FROM enrollments WHERE user_id = %s", (user_id,))
    try:
        with cursor() as cur:
            cur.execute("DELETE FROM users WHERE id = %s", (user_id,))
        return {"ok": True, "deleted": True, "username": row["username"]}
    except Exception:
        return {"ok": True, "deleted": False, "unenrolled": True, "username": row["username"]}


def list_classes() -> list[dict]:
    try:
        with cursor() as cur:
            cur.execute(
                """
                SELECT c.id, c.name, u.name AS teacher
                FROM classes c
                LEFT JOIN users u ON u.id = c.teacher_id
                ORDER BY c.name
                """
            )
            return list(cur.fetchall())
    except Exception:
        return []
