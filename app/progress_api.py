"""API tiến độ / đánh giá học sinh."""
from __future__ import annotations

from fastapi import APIRouter, HTTPException, Request

from app.accounts import create_account
from app.client_v1 import _require_user, _staff
from app.db import cursor
from app.adaptive import adaptive_cards
from app.insights import bank_reliability, class_radar, program_radar, skill_gaps
from app.pedagogy import (
    class_first_attempt_fail,
    class_hint_dependency,
    class_unresolved_stuck,
    pedagogy_alerts,
    teacher_footprint,
)
from app.stafflog import record_staff_event
from app.progress import (
    LEVELS,
    STATUS_LABELS,
    assign_class_projects,
    get_evaluation,
    list_exercises,
    list_roster,
    list_student_exercises,
    recompute_evaluation,
    student_skills,
    student_timeline,
)
from app.security import bearer_user

router = APIRouter(prefix="/api/v1")


def _as_staff_or_self(row: dict, target_id: int) -> None:
    if row["id"] == target_id:
        return
    if not _staff(row):
        raise HTTPException(status_code=403, detail="forbidden")


@router.get("/exercises")
def v1_exercises(request: Request):
    bearer_user(request)
    return {"ok": True, "exercises": list_exercises()}


@router.get("/progress")
def v1_progress(request: Request, user_id: int | None = None, program: str = "word"):
    user = bearer_user(request)
    row = _require_user(user)
    target = user_id or row["id"]
    _as_staff_or_self(row, target)
    evaluation = get_evaluation(target, program) or recompute_evaluation(target, program)
    return {
        "ok": True,
        "evaluation": evaluation,
        "exercises": list_student_exercises(target, program),
        "timeline": student_timeline(target),
        "skills": student_skills(target, program),
        "levels": LEVELS,
        "status_labels": STATUS_LABELS,
    }


@router.post("/users")
async def v1_create_user(request: Request):
    user = bearer_user(request)
    row = _require_user(user)
    if not _staff(row):
        raise HTTPException(status_code=403, detail="forbidden")
    body = await request.json()
    role = str(body.get("role") or "student").strip().lower()
    if row["role"] == "teacher" and role not in ("student",):
        raise HTTPException(status_code=403, detail="role")
    try:
        created = create_account(
            username=str(body.get("username") or ""),
            name=str(body.get("name") or ""),
            password=str(body.get("password") or ""),
            role=role,
            student_code=str(body.get("student_code") or "") or None,
            class_id=int(body["class_id"]) if body.get("class_id") else None,
        )
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    return {"ok": True, "user": created, "store": "postgresql"}


@router.get("/students")
def v1_students(request: Request, class_id: int | None = None):
    user = bearer_user(request)
    row = _require_user(user)
    if not _staff(row):
        raise HTTPException(status_code=403, detail="forbidden")
    return {"ok": True, "students": list_roster(class_id)}


@router.get("/students/{user_id}/evaluation")
def v1_student_evaluation(request: Request, user_id: int, program: str = "word"):
    user = bearer_user(request)
    row = _require_user(user)
    _as_staff_or_self(row, user_id)
    evaluation = get_evaluation(user_id, program) or recompute_evaluation(user_id, program)
    return {
        "ok": True,
        "evaluation": evaluation,
        "exercises": list_student_exercises(user_id, program),
        "timeline": student_timeline(user_id),
        "skills": student_skills(user_id, program),
    }


@router.post("/classes/{class_id}/assign")
async def v1_assign(request: Request, class_id: int):
    user = bearer_user(request)
    row = _require_user(user)
    if not _staff(row):
        raise HTTPException(status_code=403, detail="forbidden")
    body = await request.json()
    ids = body.get("project_ids") or []
    if not isinstance(ids, list) or not ids:
        raise HTTPException(status_code=400, detail="project_ids")
    with cursor() as cur:
        cur.execute("SELECT id FROM classes WHERE id = %s", (class_id,))
        if not cur.fetchone():
            raise HTTPException(status_code=404, detail="class")
    count = assign_class_projects(class_id, [str(i) for i in ids], assigned_by=row["id"])
    from app.assign import configure_assignment

    mode = str(body.get("mode") or "training")
    ip_allow = str(body.get("ip_allow") or "")
    if body.get("lan_only"):
        from app.assign import LAN_DEFAULT

        ip_allow = LAN_DEFAULT
    below = body.get("unlock_below")
    unlock = body.get("unlock_project_id")
    for pid in ids:
        configure_assignment(
            class_id,
            str(pid),
            assigned_by=row["id"],
            mode=mode,
            time_limit_sec=body.get("time_limit_sec"),
            ip_allow=ip_allow,
            unlock_below=float(below) if below not in (None, "") else None,
            unlock_project_id=str(unlock) if unlock else None,
        )
    return {"ok": True, "assigned": count, "class_id": class_id}


@router.post("/staff/heartbeat")
async def v1_staff_heartbeat(request: Request):
    user = bearer_user(request) if request.headers.get("authorization") else None
    if user is None:
        sess = request.session.get("user")
        user = sess if isinstance(sess, dict) else None
    if not user or not user.get("username"):
        raise HTTPException(status_code=401, detail="token")
    row = _require_user(user)
    if not _staff(row):
        raise HTTPException(status_code=403, detail="forbidden")
    try:
        body = await request.json()
    except Exception:
        body = {}
    path = str((body or {}).get("path") or "/quan-tri/giam-sat")
    event = "heartbeat"
    if (body or {}).get("live"):
        event = "heartbeat"
        if "giam-sat" not in path:
            path = "/quan-tri/giam-sat"
    record_staff_event(row["id"], event, path, request.session.get("staff_session_id"))
    return {"ok": True}


@router.get("/insights/pedagogy")
def v1_pedagogy(request: Request, hours: int = 24):
    user = bearer_user(request)
    row = _require_user(user)
    if row.get("role") != "admin":
        raise HTTPException(status_code=403, detail="forbidden")
    hours = max(1, min(int(hours or 24), 168))
    return {
        "ok": True,
        "hours": hours,
        "hints": class_hint_dependency(hours),
        "first_fail": class_first_attempt_fail(hours),
        "stuck": class_unresolved_stuck(hours),
        "teachers": teacher_footprint(hours),
        "alerts": pedagogy_alerts(hours),
    }


@router.get("/insights/gaps")
def v1_skill_gaps(request: Request, class_id: int = 0):
    user = bearer_user(request)
    row = _require_user(user)
    if not _staff(row):
        raise HTTPException(status_code=403, detail="forbidden")
    return {"ok": True, "gaps": skill_gaps(class_id), "radar": class_radar(class_id)}


@router.get("/insights/bank")
def v1_bank(request: Request):
    user = bearer_user(request)
    row = _require_user(user)
    if not _staff(row):
        raise HTTPException(status_code=403, detail="forbidden")
    return {"ok": True, "bank": bank_reliability()}


@router.get("/progress/radar")
def v1_radar(request: Request, user_id: int | None = None):
    user = bearer_user(request)
    row = _require_user(user)
    target = user_id or row["id"]
    _as_staff_or_self(row, target)
    return {"ok": True, "axes": program_radar(target)}


@router.get("/progress/adaptive")
def v1_adaptive(request: Request, user_id: int | None = None):
    user = bearer_user(request)
    row = _require_user(user)
    target = user_id or row["id"]
    _as_staff_or_self(row, target)
    return {"ok": True, "cards": adaptive_cards(target)}


@router.get("/classes/{class_id}/roster")
def v1_class_roster(request: Request, class_id: int):
    user = bearer_user(request)
    row = _require_user(user)
    if not _staff(row):
        raise HTTPException(status_code=403, detail="forbidden")
    return {"ok": True, "class_id": class_id, "students": list_roster(class_id)}
