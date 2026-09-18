"""Web quản trị MOS-KulKul — chỉ trình duyệt, không cài app."""
from __future__ import annotations

from pathlib import Path

from fastapi import APIRouter, Request
from fastapi.responses import HTMLResponse, RedirectResponse
from fastapi.templating import Jinja2Templates

from app.db import cursor

TEMPLATES = Jinja2Templates(directory=str(Path(__file__).resolve().parent / "templates"))
router = APIRouter()


def _session_user(request: Request) -> dict | None:
    user = request.session.get("user")
    return user if isinstance(user, dict) else None


def _staff(user: dict | None) -> bool:
    return bool(user and user.get("role") in ("admin", "teacher", "leadership"))


@router.get("/quan-tri", response_class=HTMLResponse)
def admin_home(request: Request):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _staff(user):
        return RedirectResponse("/", status_code=303)
    try:
        return _dashboard(request, user)
    except Exception:
        return HTMLResponse(
            "PostgreSQL chưa sẵn sàng. Chạy deploy/docker-compose.yml rồi đặt DATABASE_URL.",
            status_code=503,
        )


def _dashboard(request: Request, user: dict):
    with cursor() as cur:
        cur.execute("SELECT COUNT(*) AS n FROM users WHERE role = 'student'")
        students = cur.fetchone()["n"]
        cur.execute("SELECT COUNT(*) AS n FROM classes")
        classes = cur.fetchone()["n"]
        cur.execute("SELECT COUNT(*) AS n FROM attempts WHERE status = 'submitted'")
        submitted = cur.fetchone()["n"]
        cur.execute(
            "SELECT COALESCE(AVG(score), 0) AS avg_score FROM attempts WHERE status = 'submitted' AND score IS NOT NULL"
        )
        avg = round(float(cur.fetchone()["avg_score"] or 0), 1)
        cur.execute(
            """
            SELECT p.skill_domain AS skill, COUNT(*) AS n,
                   COALESCE(AVG(a.score), 0) AS avg_score
            FROM attempts a
            JOIN projects p ON p.id = a.project_id
            WHERE a.status = 'submitted'
            GROUP BY p.skill_domain
            ORDER BY avg_score ASC
            """
        )
        skills = cur.fetchall()
        cur.execute(
            """
            SELECT c.name AS class_name, u.name AS teacher,
                   COUNT(a.id) FILTER (WHERE a.status = 'submitted') AS done,
                   COALESCE(AVG(a.score) FILTER (WHERE a.status = 'submitted'), 0) AS avg_score,
                   COALESCE(SUM(a.duration_sec) FILTER (WHERE a.status = 'submitted'), 0) AS practice_sec
            FROM classes c
            LEFT JOIN users u ON u.id = c.teacher_id
            LEFT JOIN attempts a ON a.class_id = c.id
            GROUP BY c.id, c.name, u.name
            ORDER BY c.name
            """
        )
        classes_rows = cur.fetchall()
        cur.execute(
            """
            SELECT u.name AS student, p.title, a.mode, a.status, a.score, a.verified_score, a.pending_score,
                   a.duration_sec, a.submitted_at, a.id AS attempt_id,
                   COALESCE((a.payload->>'evidence_count')::int, 0) AS evidence_count
            FROM attempts a
            JOIN users u ON u.id = a.user_id
            JOIN projects p ON p.id = a.project_id
            WHERE a.status = 'submitted' OR COALESCE((a.payload->>'evidence_count')::int, 0) > 0
               OR a.verified_score IS NOT NULL
            ORDER BY COALESCE(a.submitted_at, a.started_at) DESC
            LIMIT 40
            """
        )
        history = cur.fetchall()
        cur.execute(
            """
            SELECT u.name AS student, p.title, t.action, t.detail, t.ts, a.id AS attempt_id
            FROM telemetry t
            JOIN attempts a ON a.id = t.attempt_id
            JOIN users u ON u.id = a.user_id
            JOIN projects p ON p.id = a.project_id
            WHERE t.action NOT IN ('submit', 'submit-offline', 'open', 'event', 'checkpoint')
            ORDER BY t.ts DESC
            LIMIT 40
            """
        )
        evidence_rows = cur.fetchall()
    return TEMPLATES.TemplateResponse(
        request,
        "admin.html",
        {
            "user": user,
            "host": request.headers.get("host", "mos.gds.edu.vn"),
            "asset_v": "kulkul5",
            "stats": {"students": students, "classes": classes, "submitted": submitted, "avg": avg},
            "skills": skills,
            "classes_rows": classes_rows,
            "history": history,
            "evidence_rows": evidence_rows,
            "program": {"id": "word", "short": "Word"},
            "programs": [],
        },
    )
