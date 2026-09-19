"""Web quản trị MOS-KulKul — lớp, bài tập, tiến độ, đánh giá học sinh."""
from __future__ import annotations

import json
from pathlib import Path

from fastapi import APIRouter, Form, Request
from fastapi.responses import HTMLResponse, RedirectResponse
from fastapi.templating import Jinja2Templates

from app.accounts import create_account, list_classes
from app.db import cursor
from app.progress import (
    LEVELS,
    STATUS_LABELS,
    get_evaluation,
    list_exercises,
    list_roster,
    list_student_exercises,
    recompute_evaluation,
    student_skills,
    student_timeline,
)

TEMPLATES = Jinja2Templates(directory=str(Path(__file__).resolve().parent / "templates"))
router = APIRouter()


def _session_user(request: Request) -> dict | None:
    user = request.session.get("user")
    return user if isinstance(user, dict) else None


def _staff(user: dict | None) -> bool:
    return bool(user and user.get("role") in ("admin", "teacher", "leadership"))


def _jsonish(value):
    if isinstance(value, (list, dict)):
        return value
    if not value:
        return []
    if isinstance(value, str):
        return json.loads(value)
    return value


def _ctx(request: Request, user: dict, extra: dict | None = None) -> dict:
    data = {
        "user": user,
        "host": request.headers.get("host", "mos.gds.edu.vn"),
        "asset_v": "kulkul9",
        "program": {"id": "word", "short": "Word"},
        "programs": [],
        "levels": LEVELS,
        "status_labels": STATUS_LABELS,
    }
    if extra:
        data.update(extra)
    return data


@router.get("/quan-tri", response_class=HTMLResponse)
def admin_home(request: Request):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _staff(user):
        return RedirectResponse("/tien-do", status_code=303)
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
        cur.execute("SELECT COUNT(*) AS n FROM projects WHERE published = TRUE")
        exercises = cur.fetchone()["n"]
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
    roster = list_roster()[:12]
    return TEMPLATES.TemplateResponse(
        request,
        "admin.html",
        _ctx(
            request,
            user,
            {
                "nav": "home",
                "stats": {
                    "students": students,
                    "classes": classes,
                    "exercises": exercises,
                    "submitted": submitted,
                    "avg": avg,
                },
                "skills": skills,
                "classes_rows": classes_rows,
                "history": history,
                "evidence_rows": evidence_rows,
                "roster": roster,
            },
        ),
    )


@router.get("/quan-tri/hoc-sinh", response_class=HTMLResponse)
def admin_students(request: Request):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _staff(user):
        return RedirectResponse("/tien-do", status_code=303)
    return TEMPLATES.TemplateResponse(
        request,
        "admin_students.html",
        _ctx(
            request,
            user,
            {
                "nav": "students",
                "roster": list_roster(),
                "classes": list_classes(),
                "error": request.query_params.get("loi"),
            },
        ),
    )


@router.post("/quan-tri/hoc-sinh")
def admin_create_student(
    request: Request,
    username: str = Form(...),
    name: str = Form(...),
    password: str = Form(...),
    student_code: str = Form(""),
    class_id: str = Form(""),
    role: str = Form("student"),
):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _staff(user):
        return RedirectResponse("/tien-do", status_code=303)
    cid = int(class_id) if str(class_id).isdigit() else None
    try:
        create_account(
            username=username,
            name=name,
            password=password,
            role=(
                "teacher"
                if role == "teacher" and user.get("role") in ("admin", "leadership")
                else "student"
            ),
            student_code=student_code,
            class_id=cid,
        )
    except ValueError:
        return RedirectResponse("/quan-tri/hoc-sinh?loi=1", status_code=303)
    return RedirectResponse("/quan-tri/hoc-sinh", status_code=303)


@router.get("/quan-tri/hoc-sinh/{user_id}", response_class=HTMLResponse)
def admin_student(request: Request, user_id: int):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _staff(user):
        return RedirectResponse("/tien-do", status_code=303)
    evaluation = get_evaluation(user_id, "word") or recompute_evaluation(user_id, "word")
    if evaluation:
        evaluation["weak_skills"] = _jsonish(evaluation.get("weak_skills"))
        evaluation["strong_skills"] = _jsonish(evaluation.get("strong_skills"))
    return TEMPLATES.TemplateResponse(
        request,
        "admin_student.html",
        _ctx(
            request,
            user,
            {
                "nav": "students",
                "evaluation": evaluation,
                "exercises": list_student_exercises(user_id, "word"),
                "timeline": student_timeline(user_id),
                "skills": student_skills(user_id, "word"),
            },
        ),
    )


@router.get("/quan-tri/bai-tap", response_class=HTMLResponse)
def admin_exercises(request: Request):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _staff(user):
        return RedirectResponse("/tien-do", status_code=303)
    return TEMPLATES.TemplateResponse(
        request,
        "admin_exercises.html",
        _ctx(request, user, {"nav": "exercises", "exercises": list_exercises()}),
    )


@router.get("/tien-do", response_class=HTMLResponse)
def my_progress(request: Request):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    with cursor() as cur:
        cur.execute("SELECT id FROM users WHERE username = %s", (user.get("username"),))
        row = cur.fetchone()
    if not row:
        return RedirectResponse("/", status_code=303)
    user_id = row["id"]
    evaluation = get_evaluation(user_id, "word") or recompute_evaluation(user_id, "word")
    if evaluation:
        evaluation["weak_skills"] = _jsonish(evaluation.get("weak_skills"))
        evaluation["strong_skills"] = _jsonish(evaluation.get("strong_skills"))
    return TEMPLATES.TemplateResponse(
        request,
        "progress.html",
        _ctx(
            request,
            user,
            {
                "nav": "mine",
                "evaluation": evaluation,
                "exercises": list_student_exercises(user_id, "word"),
                "timeline": student_timeline(user_id),
                "skills": student_skills(user_id, "word"),
            },
        ),
    )
