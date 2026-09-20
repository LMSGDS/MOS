"""Web quản trị MOS-KulKul — lớp, bài tập, tiến độ, đánh giá học sinh."""
from __future__ import annotations

import json
from pathlib import Path

from fastapi import APIRouter, File, Form, Request, UploadFile
from fastapi.responses import HTMLResponse, PlainTextResponse, RedirectResponse, Response
from fastapi.templating import Jinja2Templates

from app.accounts import create_account, remove_student, update_account
from app.assign import LAN_DEFAULT, configure_assignment, list_configured
from app.db import cursor
from app.insights import annotate_sessions, bank_reliability, class_radar, skill_gaps
from app.roster import (
    CSV_TEMPLATE,
    assign_teacher,
    bulk_reset,
    classes_for,
    import_csv,
    list_teachers,
    reset_one,
    rotate_join_code,
    staff_manages_student,
)
from app.pedagogy import (
    class_first_attempt_fail,
    class_hint_dependency,
    class_unresolved_stuck,
    pedagogy_alerts,
    teacher_footprint,
)
from app.live import list_class_sessions
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


def _leaders(user: dict | None) -> bool:
    return bool(user and user.get("role") == "admin")


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
        "asset_v": "kulkul13",
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
    pedagogy = pedagogy_alerts(24) if _leaders(user) else []
    return TEMPLATES.TemplateResponse(
        request,
        "admin.html",
        _ctx(
            request,
            user,
            {
                "nav": "home",
                "pedagogy_alerts": pedagogy,
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


ROSTER_PAGE = 25
STUDENT_TABS = ("danh-sach", "nhap", "phong-may")


def _students_tab(value: str | None) -> str:
    tab = (value or "danh-sach").strip()
    return tab if tab in STUDENT_TABS else "danh-sach"


def _students_url(**params) -> str:
    parts = []
    for key, raw in params.items():
        if raw is None or raw == "":
            continue
        parts.append(f"{key}={raw}")
    return "/quan-tri/hoc-sinh" + (("?" + "&".join(parts)) if parts else "")


def _filter_roster(user: dict, class_id: int | None, query: str, level: str) -> list[dict]:
    roster = list_roster(class_id)
    if user.get("role") == "teacher":
        allowed = {c["id"] for c in classes_for(user)}
        roster = [row for row in roster if row.get("class_id") in allowed]
    needle = (query or "").strip().lower()
    if needle:
        roster = [
            row
            for row in roster
            if needle in (row.get("name") or "").lower()
            or needle in (row.get("username") or "").lower()
            or needle in (row.get("student_code") or "").lower()
        ]
    if level:
        roster = [row for row in roster if (row.get("level") or "chua_bat_dau") == level]
    return roster


def _store_password_csv(request: Request, accounts: list[dict]) -> None:
    lines = ["username,password,name,student_code"]
    for acc in accounts:
        lines.append(
            f"{acc['username']},{acc['password']},{acc.get('name') or ''},{acc.get('student_code') or ''}"
        )
    request.session["roster_csv"] = "\n".join(lines)


@router.get("/quan-tri/hoc-sinh", response_class=HTMLResponse)
def admin_students(request: Request):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _staff(user):
        return RedirectResponse("/tien-do", status_code=303)
    tab = _students_tab(request.query_params.get("tab"))
    query = (request.query_params.get("q") or "").strip()
    lop = request.query_params.get("lop") or ""
    muc = request.query_params.get("muc") or ""
    cid = int(lop) if str(lop).isdigit() else None
    roster = _filter_roster(user, cid, query, muc)
    total = len(roster)
    pages = max(1, (total + ROSTER_PAGE - 1) // ROSTER_PAGE)
    try:
        page = max(1, int(request.query_params.get("trang") or 1))
    except ValueError:
        page = 1
    page = min(page, pages)
    start = (page - 1) * ROSTER_PAGE
    return TEMPLATES.TemplateResponse(
        request,
        "admin_students.html",
        _ctx(
            request,
            user,
            {
                "nav": "students",
                "tab": tab,
                "roster": roster[start : start + ROSTER_PAGE],
                "roster_total": total,
                "page": page,
                "pages": pages,
                "q": query,
                "lop": lop,
                "muc": muc,
                "classes": classes_for(user),
                "teachers": list_teachers() if user.get("role") == "admin" else [],
                "imported": request.query_params.get("nhap"),
                "reset_n": request.query_params.get("reset"),
                "error": request.query_params.get("loi"),
                "created": request.query_params.get("tao"),
                "updated": request.query_params.get("sua"),
                "removed": request.query_params.get("xoa"),
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
                if role == "teacher" and user.get("role") == "admin"
                else "student"
            ),
            student_code=student_code,
            class_id=cid,
        )
    except ValueError:
        return RedirectResponse(_students_url(tab="danh-sach", loi=1), status_code=303)
    return RedirectResponse(_students_url(tab="danh-sach", tao=1), status_code=303)


@router.post("/quan-tri/hoc-sinh/nhap")
async def admin_import_roster(
    request: Request,
    class_id: str = Form(""),
    file: UploadFile = File(...),
):
    user = _session_user(request)
    if not user or not _staff(user):
        return RedirectResponse("/dang-nhap", status_code=303)
    raw = await file.read()
    text = raw.decode("utf-8-sig", errors="replace")
    cid = int(class_id) if str(class_id).isdigit() else None
    result = import_csv(text, class_id=cid)
    _store_password_csv(request, result.get("accounts") or [])
    return RedirectResponse(_students_url(tab="nhap", nhap=result["created"]), status_code=303)


@router.get("/quan-tri/hoc-sinh/mau.csv")
def admin_roster_template(request: Request):
    user = _session_user(request)
    if not user or not _staff(user):
        return RedirectResponse("/dang-nhap", status_code=303)
    body = "\ufeff" + CSV_TEMPLATE
    return Response(
        body,
        media_type="text/csv; charset=utf-8",
        headers={"Content-Disposition": 'attachment; filename="mau-danh-sach-hoc-sinh.csv"'},
    )


@router.get("/quan-tri/hoc-sinh/bao-cao.csv")
def admin_roster_export(request: Request):
    user = _session_user(request)
    if not user or not _staff(user):
        return RedirectResponse("/dang-nhap", status_code=303)
    query = (request.query_params.get("q") or "").strip()
    lop = request.query_params.get("lop") or ""
    muc = request.query_params.get("muc") or ""
    cid = int(lop) if str(lop).isdigit() else None
    roster = _filter_roster(user, cid, query, muc)
    lines = ["Mã HS,Họ tên,Tài khoản,Lớp,Tiến độ,Hoàn thành,Điểm TB,Tiến bộ,Đánh giá"]
    for row in roster:
        done = f"{row.get('exercises_completed') or 0}/{row.get('exercises_assigned') or 0}"
        lines.append(
            ",".join(
                [
                    str(row.get("student_code") or ""),
                    str(row.get("name") or "").replace(",", " "),
                    str(row.get("username") or ""),
                    str(row.get("class_name") or "Chưa xếp lớp").replace(",", " "),
                    f"{float(row.get('completion_pct') or 0):.0f}",
                    done,
                    f"{float(row.get('overall_score') or 0):.0f}",
                    f"{float(row.get('avg_growth') or 0):.0f}",
                    str(row.get("level_label") or ""),
                ]
            )
        )
    body = "\ufeff" + "\n".join(lines) + "\n"
    return Response(
        body,
        media_type="text/csv; charset=utf-8",
        headers={"Content-Disposition": 'attachment; filename="bao-cao-hoc-sinh.csv"'},
    )


@router.get("/quan-tri/hoc-sinh/mat-khau.csv")
def admin_roster_csv(request: Request):
    user = _session_user(request)
    if not user or not _staff(user):
        return RedirectResponse("/dang-nhap", status_code=303)
    body = request.session.pop("roster_csv", "username,password\n")
    return PlainTextResponse(body, media_type="text/csv; charset=utf-8")


@router.post("/quan-tri/hoc-sinh/reset")
def admin_bulk_reset(request: Request, class_id: str = Form(...)):
    user = _session_user(request)
    if not user or not _staff(user):
        return RedirectResponse("/dang-nhap", status_code=303)
    cid = int(class_id) if str(class_id).isdigit() else 0
    creds = bulk_reset(cid) if cid else []
    _store_password_csv(request, creds)
    return RedirectResponse(_students_url(tab="phong-may", reset=len(creds)), status_code=303)


@router.post("/quan-tri/lop/ma")
def admin_join_code(request: Request, class_id: str = Form(...)):
    user = _session_user(request)
    if not user or not _staff(user):
        return RedirectResponse("/dang-nhap", status_code=303)
    cid = int(class_id) if str(class_id).isdigit() else 0
    if cid:
        rotate_join_code(cid)
    return RedirectResponse(_students_url(tab="phong-may"), status_code=303)


@router.post("/quan-tri/lop/giao-vien")
def admin_set_teacher(request: Request, class_id: str = Form(...), teacher_id: str = Form(...)):
    user = _session_user(request)
    if not user or user.get("role") != "admin":
        return RedirectResponse("/quan-tri", status_code=303)
    try:
        assign_teacher(int(class_id), int(teacher_id))
    except (ValueError, TypeError):
        return RedirectResponse(_students_url(tab="phong-may", loi=1), status_code=303)
    return RedirectResponse(_students_url(tab="phong-may"), status_code=303)


@router.post("/quan-tri/hoc-sinh/{user_id}/mat-khau")
def admin_reset_one(request: Request, user_id: int):
    user = _session_user(request)
    if not user or not _staff(user):
        return RedirectResponse("/dang-nhap", status_code=303)
    if not staff_manages_student(user, user_id) and user.get("role") != "admin":
        return RedirectResponse(_students_url(tab="danh-sach", loi=1), status_code=303)
    cred = reset_one(user_id)
    if not cred:
        return RedirectResponse(_students_url(tab="danh-sach", loi=1), status_code=303)
    _store_password_csv(request, [cred])
    return RedirectResponse(_students_url(tab="danh-sach", reset=1), status_code=303)


@router.post("/quan-tri/hoc-sinh/{user_id}/sua")
def admin_edit_student(
    request: Request,
    user_id: int,
    name: str = Form(...),
    student_code: str = Form(""),
    class_id: str = Form(""),
    password: str = Form(""),
):
    user = _session_user(request)
    if not user or not _staff(user):
        return RedirectResponse("/dang-nhap", status_code=303)
    if user.get("role") != "admin" and not staff_manages_student(user, user_id):
        return RedirectResponse(_students_url(tab="danh-sach", loi=1), status_code=303)
    cid = int(class_id) if str(class_id).isdigit() else None
    try:
        update_account(
            user_id,
            name=name,
            student_code=student_code,
            class_id=cid,
            password=password or None,
        )
    except ValueError:
        return RedirectResponse(_students_url(tab="danh-sach", loi=1), status_code=303)
    return RedirectResponse(_students_url(tab="danh-sach", sua=1), status_code=303)


@router.post("/quan-tri/hoc-sinh/{user_id}/xoa")
def admin_delete_student(request: Request, user_id: int):
    user = _session_user(request)
    if not user or not _staff(user):
        return RedirectResponse("/dang-nhap", status_code=303)
    if user.get("role") != "admin" and not staff_manages_student(user, user_id):
        return RedirectResponse(_students_url(tab="danh-sach", loi=1), status_code=303)
    try:
        result = remove_student(user_id)
    except ValueError:
        return RedirectResponse(_students_url(tab="danh-sach", loi=1), status_code=303)
    flag = "1" if result.get("deleted") else "giu"
    return RedirectResponse(_students_url(tab="danh-sach", xoa=flag), status_code=303)


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
    q_rows, q_axes = _q_matrix_for_user(user_id)
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
                "q_matrix": q_rows,
                "q_axes": q_axes,
            },
        ),
    )


def _q_matrix_for_user(user_id: int) -> tuple[list[dict], dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT q.criterion_id, q.locate, q.tool, q.configure, q.status,
                   q.earned, q.possible, p.title, a.project_id, a.id AS attempt_id
            FROM q_matrix_results q
            JOIN attempts a ON a.id = q.attempt_id
            JOIN projects p ON p.id = a.project_id
            WHERE a.user_id = %s
            ORDER BY q.created_at DESC
            LIMIT 80
            """,
            (user_id,),
        )
        rows = list(cur.fetchall())
    axes = {"locate": 0.0, "tool": 0.0, "configure": 0.0}
    totals = {"locate": 0, "tool": 0, "configure": 0}
    hits = {"locate": 0, "tool": 0, "configure": 0}
    for row in rows:
        for key in axes:
            val = str(row.get(key) or "")
            if not val:
                continue
            totals[key] += 1
            if val == "pass":
                hits[key] += 1
    for key in axes:
        axes[key] = round(100 * hits[key] / totals[key], 1) if totals[key] else 0.0
    return rows, axes


@router.get("/quan-tri/giam-sat", response_class=HTMLResponse)
def admin_live(request: Request):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _staff(user):
        return RedirectResponse("/tien-do", status_code=303)
    classes = classes_for(user)
    raw = request.query_params.get("lop") or "0"
    class_id = int(raw) if str(raw).isdigit() else 0
    return TEMPLATES.TemplateResponse(
        request,
        "admin_live.html",
        _ctx(
            request,
            user,
            {
                "nav": "live",
                "classes": classes,
                "class_id": class_id,
                "sessions": annotate_sessions(list_class_sessions(class_id)),
            },
        ),
    )


@router.get("/quan-tri/lo-hong", response_class=HTMLResponse)
def admin_gaps(request: Request):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _staff(user):
        return RedirectResponse("/tien-do", status_code=303)
    raw = request.query_params.get("lop") or "0"
    class_id = int(raw) if str(raw).isdigit() else 0
    return TEMPLATES.TemplateResponse(
        request,
        "admin_gaps.html",
        _ctx(
            request,
            user,
            {
                "nav": "gaps",
                "classes": classes_for(user),
                "class_id": class_id,
                "gaps": skill_gaps(class_id),
                "radar": class_radar(class_id),
            },
        ),
    )


@router.get("/quan-tri/ngan-hang", response_class=HTMLResponse)
def admin_bank(request: Request):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _staff(user):
        return RedirectResponse("/tien-do", status_code=303)
    return TEMPLATES.TemplateResponse(
        request,
        "admin_bank.html",
        _ctx(request, user, {"nav": "bank", "bank": bank_reliability()}),
    )


@router.get("/quan-tri/su-pham", response_class=HTMLResponse)
def admin_pedagogy(request: Request):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _leaders(user):
        return RedirectResponse("/quan-tri", status_code=303)
    hours = request.query_params.get("gio") or "24"
    hours_n = int(hours) if str(hours).isdigit() else 24
    hours_n = max(1, min(hours_n, 168))
    return TEMPLATES.TemplateResponse(
        request,
        "admin_pedagogy.html",
        _ctx(
            request,
            user,
            {
                "nav": "pedagogy",
                "hours": hours_n,
                "hints": class_hint_dependency(hours_n),
                "first_fail": class_first_attempt_fail(hours_n),
                "stuck": class_unresolved_stuck(hours_n),
                "teachers": teacher_footprint(hours_n),
                "alerts": pedagogy_alerts(hours_n),
            },
        ),
    )


@router.get("/quan-tri/lti", response_class=HTMLResponse)
def admin_lti(request: Request):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _leaders(user):
        return RedirectResponse("/quan-tri", status_code=303)
    try:
        from app.lti import list_platforms
    except Exception:
        return HTMLResponse("LTI chưa sẵn sàng (cần PyJWT). Chờ git-sync pip install.", status_code=503)

    host = request.headers.get("host", "mos.gds.edu.vn")
    scheme = "https" if "edu.vn" in host else request.url.scheme
    base = f"{scheme}://{host}"
    return TEMPLATES.TemplateResponse(
        request,
        "admin_lti.html",
        _ctx(
            request,
            user,
            {
                "nav": "lti",
                "platforms": list_platforms(),
                "login_url": f"{base}/lti/login",
                "launch_url": f"{base}/lti/launch",
                "jwks_url": f"{base}/lti/jwks",
            },
        ),
    )


@router.post("/quan-tri/lti")
def admin_lti_save(
    request: Request,
    name: str = Form(""),
    issuer: str = Form(...),
    client_id: str = Form(...),
    auth_login_url: str = Form(...),
    auth_token_url: str = Form(""),
    jwks_url: str = Form(""),
):
    user = _session_user(request)
    if not user:
        return RedirectResponse("/dang-nhap", status_code=303)
    if not _leaders(user):
        return RedirectResponse("/quan-tri", status_code=303)
    try:
        from app.lti import register_platform
    except Exception:
        return RedirectResponse("/quan-tri/lti", status_code=303)

    register_platform(
        name=name,
        issuer=issuer,
        client_id=client_id,
        auth_login_url=auth_login_url,
        auth_token_url=auth_token_url,
        jwks_url=jwks_url,
    )
    return RedirectResponse("/quan-tri/lti", status_code=303)


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
        _ctx(
            request,
            user,
            {
                "nav": "exercises",
                "exercises": list_exercises(),
                "classes": classes_for(user),
                "configured": list_configured(),
                "lan_default": LAN_DEFAULT,
            },
        ),
    )


@router.post("/quan-tri/bai-tap")
def admin_configure_assignment(
    request: Request,
    class_id: str = Form(...),
    project_id: str = Form(...),
    mode: str = Form("training"),
    time_limit_sec: str = Form(""),
    lan_only: str = Form(""),
    unlock_below: str = Form(""),
    unlock_project_id: str = Form(""),
):
    user = _session_user(request)
    if not user or not _staff(user):
        return RedirectResponse("/dang-nhap", status_code=303)
    cid = int(class_id) if str(class_id).isdigit() else 0
    limit = int(time_limit_sec) if str(time_limit_sec).isdigit() else None
    below = float(unlock_below) if unlock_below else None
    with cursor() as cur:
        cur.execute("SELECT id FROM users WHERE username = %s", (user.get("username"),))
        row = cur.fetchone()
    configure_assignment(
        cid,
        project_id,
        assigned_by=row["id"] if row else None,
        mode=mode,
        time_limit_sec=limit,
        ip_allow=LAN_DEFAULT if lan_only else "",
        unlock_below=below,
        unlock_project_id=unlock_project_id or None,
    )
    return RedirectResponse("/quan-tri/bai-tap", status_code=303)


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
    from app.adaptive import adaptive_cards

    return TEMPLATES.TemplateResponse(
        request,
        "progress.html",
        _ctx(
            request,
            user,
            {
                "nav": "mine",
                "evaluation": evaluation,
                "adaptive": adaptive_cards(user_id),
                "join_error": request.query_params.get("lop"),
                "exercises": list_student_exercises(user_id, "word"),
                "timeline": student_timeline(user_id),
                "skills": student_skills(user_id, "word"),
            },
        ),
    )
