"""Phân hệ Identity & Roster: JWT standalone hoặc LTI OIDC / LIS.

Không xây forum, luận, quiz, hay hòm thư — chỉ định danh và danh sách lớp.
"""
from __future__ import annotations

import hashlib
import re

from app.db import cursor

STAFF_ROLES = ("admin", "teacher")
LTI_INSTRUCTOR = (
    "http://purl.imsglobal.org/vocab/lis/v2/membership#Instructor",
    "http://purl.imsglobal.org/vocab/lis/v2/institution/person#Instructor",
    "Instructor",
    "Faculty",
    "Administrator",
    "http://purl.imsglobal.org/vocab/lis/v2/institution/person#Administrator",
)


def role_from_lti(roles) -> str:
    items = roles if isinstance(roles, (list, tuple)) else [roles]
    text = " ".join(str(r or "") for r in items)
    if any(tag in text for tag in LTI_INSTRUCTOR):
        return "teacher"
    return "student"


def lis_username(issuer: str, sub: str) -> str:
    raw = f"{issuer}|{sub}"
    digest = hashlib.sha256(raw.encode("utf-8")).hexdigest()[:16]
    host = re.sub(r"[^a-z0-9]+", "", (issuer or "").split("://")[-1].split("/")[0].lower())[:18]
    return f"lti-{host or 'plat'}-{digest}"


def upsert_lis_person(
    *,
    issuer: str,
    sub: str,
    name: str,
    email: str = "",
    roles=None,
    sourced_id: str = "",
    context_id: str = "",
    context_title: str = "",
    deployment_id: str = "",
) -> dict:
    """Ghi học sinh/giáo viên theo LIS sourcedId + LTI sub (Canvas/Moodle)."""
    uname = lis_username(issuer, sub)
    display = (name or email or uname).strip()
    role = role_from_lti(roles)
    sourced = (sourced_id or sub or "").strip() or None
    with cursor() as cur:
        cur.execute(
            """
            SELECT id, username, name, role FROM users
            WHERE lti_issuer = %s AND lti_sub = %s
            """,
            (issuer, sub),
        )
        row = cur.fetchone()
        if row:
            cur.execute(
                """
                UPDATE users SET
                  name = %s,
                  lis_sourced_id = COALESCE(%s, lis_sourced_id),
                  last_seen_at = now(),
                  last_client = 'lti'
                WHERE id = %s
                RETURNING id, username, name, role, lis_sourced_id, lti_sub, lti_issuer
                """,
                (display, sourced, row["id"]),
            )
            person = cur.fetchone()
        else:
            cur.execute(
                """
                INSERT INTO users (
                  username, name, role, password_hash, org_id,
                  lis_sourced_id, lti_sub, lti_issuer, last_client, last_seen_at
                ) VALUES (%s, %s, %s, 'lti$', 1, %s, %s, %s, 'lti', now())
                RETURNING id, username, name, role, lis_sourced_id, lti_sub, lti_issuer
                """,
                (uname, display, role, sourced, sub, issuer),
            )
            person = cur.fetchone()
        class_id = None
        if context_id:
            title = (context_title or context_id).strip()
            cur.execute(
                """
                SELECT id FROM classes
                WHERE lti_context_id = %s AND COALESCE(lti_deployment_id, '') = %s
                """,
                (context_id, deployment_id or ""),
            )
            klass = cur.fetchone()
            if klass:
                class_id = klass["id"]
            else:
                teacher_id = person["id"] if person["role"] == "teacher" else None
                cur.execute(
                    """
                    INSERT INTO classes (name, org_id, teacher_id, lti_context_id, lti_deployment_id)
                    VALUES (%s, 1, %s, %s, %s)
                    RETURNING id
                    """,
                    (title, teacher_id, context_id, deployment_id or None),
                )
                class_id = cur.fetchone()["id"]
            if person["role"] == "student":
                cur.execute(
                    "INSERT INTO enrollments (class_id, user_id) VALUES (%s, %s) ON CONFLICT DO NOTHING",
                    (class_id, person["id"]),
                )
            elif person["role"] == "teacher":
                cur.execute(
                    "UPDATE classes SET teacher_id = %s WHERE id = %s AND teacher_id IS NULL",
                    (person["id"], class_id),
                )
    return {**person, "class_id": class_id}


def public_user(row: dict) -> dict:
    return {
        "id": row.get("id"),
        "username": row.get("username"),
        "name": row.get("name"),
        "role": row.get("role") or "student",
    }
