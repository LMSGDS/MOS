"""Cỗ máy giao bài MOS — mode, LAN, luật thích ứng. Không đăng file tài liệu."""
from __future__ import annotations

import ipaddress

from app.db import cursor
from app.progress import assign_class_projects, recompute_evaluation

LAN_DEFAULT = "10.0.0.0/8,172.16.0.0/12,192.168.0.0/16"


def client_ip(request) -> str:
    xff = (request.headers.get("x-forwarded-for") or "").split(",")[0].strip()
    if xff:
        return xff
    if request.client and request.client.host:
        return request.client.host
    return ""


def ip_allowed(ip: str, allow: str) -> bool:
    rules = [p.strip() for p in (allow or "").split(",") if p.strip()]
    if not rules:
        return True
    try:
        addr = ipaddress.ip_address(ip)
    except ValueError:
        return False
    for rule in rules:
        try:
            if "/" in rule:
                if addr in ipaddress.ip_network(rule, strict=False):
                    return True
            elif addr == ipaddress.ip_address(rule):
                return True
        except ValueError:
            continue
    return False


def configure_assignment(
    class_id: int,
    project_id: str,
    *,
    assigned_by: int | None = None,
    mode: str = "training",
    time_limit_sec: int | None = None,
    ip_allow: str = "",
    unlock_below: float | None = None,
    unlock_project_id: str | None = None,
) -> dict:
    mode = mode if mode in ("training", "testing") else "training"
    assign_class_projects(class_id, [project_id], assigned_by=assigned_by)
    with cursor() as cur:
        cur.execute(
            """
            UPDATE assignments SET
              mode = %s,
              time_limit_sec = %s,
              ip_allow = %s,
              unlock_below = %s,
              unlock_project_id = %s
            WHERE class_id = %s AND project_id = %s
            RETURNING *
            """,
            (mode, time_limit_sec, ip_allow or "", unlock_below, unlock_project_id or None, class_id, project_id),
        )
        row = cur.fetchone()
        if unlock_below is not None and unlock_project_id:
            cur.execute(
                """
                INSERT INTO adaptive_rules (class_id, source_project_id, below_score, unlock_project_id, created_by)
                VALUES (%s, %s, %s, %s, %s)
                ON CONFLICT (class_id, source_project_id, unlock_project_id)
                DO UPDATE SET below_score = EXCLUDED.below_score
                """,
                (class_id, project_id, unlock_below, unlock_project_id, assigned_by),
            )
    return dict(row) if row else {}


def assignment_for(user_id: int, project_id: str) -> dict | None:
    with cursor() as cur:
        cur.execute(
            """
            SELECT a.*
            FROM assignments a
            JOIN enrollments e ON e.class_id = a.class_id
            WHERE e.user_id = %s AND a.project_id = %s
            ORDER BY a.id DESC
            LIMIT 1
            """,
            (user_id, project_id),
        )
        return cur.fetchone()


def student_project_ids(user_id: int) -> set[str]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT a.project_id
            FROM assignments a
            JOIN enrollments e ON e.class_id = a.class_id
            WHERE e.user_id = %s
            UNION
            SELECT r.unlock_project_id AS project_id
            FROM adaptive_rules r
            JOIN enrollments e ON e.class_id = r.class_id
            JOIN attempts att ON att.user_id = e.user_id AND att.project_id = r.source_project_id
            WHERE e.user_id = %s
              AND att.status IN ('submitted', 'graded')
              AND COALESCE(att.verified_score, att.score, 0) < r.below_score
            """,
            (user_id, user_id),
        )
        return {row["project_id"] for row in cur.fetchall()}


def apply_adaptive(user_id: int, project_id: str, score) -> list[str]:
    if score is None:
        return []
    unlocked = []
    with cursor() as cur:
        cur.execute("SELECT class_id FROM enrollments WHERE user_id = %s", (user_id,))
        classes = [r["class_id"] for r in cur.fetchall() if r.get("class_id")]
        if not classes:
            return []
        cur.execute(
            """
            SELECT * FROM adaptive_rules
            WHERE source_project_id = %s AND class_id = ANY(%s) AND %s < below_score
            """,
            (project_id, classes, float(score)),
        )
        rules = list(cur.fetchall())
        for rule in rules:
            cur.execute(
                """
                INSERT INTO assignments (project_id, class_id, assigned_by, mode)
                VALUES (%s, %s, %s, 'training')
                ON CONFLICT (project_id, class_id) DO NOTHING
                """,
                (rule["unlock_project_id"], rule["class_id"], rule.get("created_by")),
            )
            unlocked.append(rule["unlock_project_id"])
    if unlocked:
        recompute_evaluation(user_id, "word")
    return unlocked


def list_configured(class_id: int = 0) -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT a.*, p.title, c.name AS class_name
            FROM assignments a
            JOIN projects p ON p.id = a.project_id
            JOIN classes c ON c.id = a.class_id
            WHERE (%s = 0 OR a.class_id = %s)
            ORDER BY c.name, p.sort_order, p.title
            """,
            (class_id, class_id),
        )
        return list(cur.fetchall())
