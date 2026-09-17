"""Client API v1 — desktop MOS-KulKul (JWT, store-and-forward)."""
from __future__ import annotations

import json
import secrets
from datetime import datetime, timezone
from pathlib import Path

from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import FileResponse

from app.auth import authenticate
from app.db import cursor
from app.programs import normalize
from app.scoring import score_file
from app.security import bearer_user
from app.tokens import issue

ROOT = Path(__file__).resolve().parent.parent
RESULTS = ROOT / "data" / "results"
router = APIRouter(prefix="/api/v1")


def _user_row(username: str) -> dict | None:
    with cursor() as cur:
        cur.execute("SELECT * FROM users WHERE username = %s", (username,))
        return cur.fetchone()


@router.post("/auth/login")
async def v1_login(request: Request):
    data = await request.json()
    username = str(data.get("username") or "")
    password = str(data.get("password") or "")
    program = normalize(str(data.get("chuong_trinh") or "word"))
    user = authenticate(username, password)
    if not user:
        raise HTTPException(status_code=401, detail="sai")
    row = _user_row(user["username"])
    if row:
        user = {
            "id": row["id"],
            "username": row["username"],
            "name": row["name"],
            "role": row["role"],
        }
    token = issue(user)
    request.session["user"] = {
        "username": user["username"],
        "name": user["name"],
        "role": user["role"],
    }
    request.session["chuong_trinh"] = program
    request.session["che_do"] = "dock"
    return {"ok": True, "token": token, "user": user, "program": program}


@router.get("/me")
def v1_me(request: Request):
    user = bearer_user(request)
    row = _user_row(user["username"])
    if row:
        user["id"] = row["id"]
        user["role"] = row["role"]
        user["name"] = row["name"]
    return {"ok": True, "user": user}


@router.get("/projects")
def v1_projects(request: Request, program: str | None = None):
    bearer_user(request)
    prog = normalize(program) if program else None
    with cursor() as cur:
        if prog:
            cur.execute(
                "SELECT id, title, program, skill_domain, filename, steps, time_limit_sec FROM projects WHERE program = %s ORDER BY title",
                (prog,),
            )
        else:
            cur.execute(
                "SELECT id, title, program, skill_domain, filename, steps, time_limit_sec FROM projects ORDER BY program, title"
            )
        rows = cur.fetchall()
    return {"ok": True, "projects": rows}


@router.get("/projects/{project_id}/file")
def v1_project_file(request: Request, project_id: str):
    bearer_user(request)
    with cursor() as cur:
        cur.execute("SELECT filename, file_path FROM projects WHERE id = %s", (project_id,))
        row = cur.fetchone()
    if not row:
        raise HTTPException(status_code=404, detail="project")
    path = Path(row["file_path"])
    if not path.is_file():
        raise HTTPException(status_code=404, detail="file")
    return FileResponse(path, filename=row["filename"])


@router.post("/attempts")
async def v1_start_attempt(request: Request):
    user = bearer_user(request)
    body = await request.json()
    project_id = str(body.get("project_id") or "")
    mode = str(body.get("mode") or "training")
    if mode not in ("training", "testing"):
        mode = "training"
    row = _user_row(user["username"])
    if not row:
        raise HTTPException(status_code=401, detail="user")
    with cursor() as cur:
        cur.execute("SELECT id FROM projects WHERE id = %s", (project_id,))
        if not cur.fetchone():
            raise HTTPException(status_code=404, detail="project")
        cur.execute("SELECT class_id FROM enrollments WHERE user_id = %s LIMIT 1", (row["id"],))
        enr = cur.fetchone()
        attempt_id = secrets.token_hex(12)
        cur.execute(
            """
            INSERT INTO attempts (id, user_id, project_id, class_id, mode, status)
            VALUES (%s, %s, %s, %s, %s, 'running')
            """,
            (attempt_id, row["id"], project_id, enr["class_id"] if enr else None, mode),
        )
    return {"ok": True, "attempt_id": attempt_id, "mode": mode}


@router.post("/attempts/{attempt_id}/telemetry")
async def v1_telemetry(request: Request, attempt_id: str):
    bearer_user(request)
    body = await request.json()
    events = body.get("events") or []
    if not isinstance(events, list):
        raise HTTPException(status_code=400, detail="events")
    with cursor() as cur:
        cur.execute("SELECT id FROM attempts WHERE id = %s", (attempt_id,))
        if not cur.fetchone():
            raise HTTPException(status_code=404, detail="attempt")
        for ev in events:
            if not isinstance(ev, dict):
                continue
            cur.execute(
                """
                INSERT INTO telemetry (attempt_id, skill, action, detail)
                VALUES (%s, %s, %s, %s::jsonb)
                """,
                (
                    attempt_id,
                    str(ev.get("skill") or ""),
                    str(ev.get("action") or "event"),
                    json.dumps(ev.get("detail") or {}, ensure_ascii=False),
                ),
            )
    return {"ok": True, "accepted": len(events)}


@router.get("/attempts")
def v1_attempts(request: Request):
    user = bearer_user(request)
    row = _user_row(user["username"])
    if not row:
        raise HTTPException(status_code=401, detail="user")
    with cursor() as cur:
        if row["role"] in ("admin", "teacher", "leadership"):
            cur.execute(
                """
                SELECT a.id, a.project_id, a.mode, a.status, a.score, a.max_score,
                       a.started_at, a.submitted_at, a.duration_sec, p.title
                FROM attempts a JOIN projects p ON p.id = a.project_id
                ORDER BY a.started_at DESC
                LIMIT 100
                """
            )
        else:
            cur.execute(
                """
                SELECT a.id, a.project_id, a.mode, a.status, a.score, a.max_score,
                       a.started_at, a.submitted_at, a.duration_sec, p.title
                FROM attempts a JOIN projects p ON p.id = a.project_id
                WHERE a.user_id = %s
                ORDER BY a.started_at DESC
                LIMIT 50
                """,
                (row["id"],),
            )
        rows = cur.fetchall()
    return {"ok": True, "attempts": rows}


@router.post("/attempts/{attempt_id}/submit")
async def v1_submit(request: Request, attempt_id: str):
    user = bearer_user(request)
    row_user = _user_row(user["username"])
    RESULTS.mkdir(parents=True, exist_ok=True)
    saved = None
    ctype = (request.headers.get("content-type") or "").lower()
    if "multipart/form-data" in ctype:
        form = await request.form()
        upload = form.get("file")
        if upload is not None and hasattr(upload, "read"):
            filename = Path(getattr(upload, "filename", None) or "bai").name
            dest = RESULTS / f"{attempt_id}-{filename}"
            data = await upload.read()
            dest.write_bytes(data)
            saved = dest
    with cursor() as cur:
        cur.execute(
            """
            SELECT a.*, p.rubric, p.skill_domain
            FROM attempts a JOIN projects p ON p.id = a.project_id
            WHERE a.id = %s
            """,
            (attempt_id,),
        )
        attempt = cur.fetchone()
        if not attempt:
            raise HTTPException(status_code=404, detail="attempt")
        if row_user and attempt["user_id"] != row_user["id"] and row_user["role"] not in ("admin", "teacher", "leadership"):
            raise HTTPException(status_code=403, detail="forbidden")
        rubric = attempt["rubric"] if isinstance(attempt["rubric"], dict) else json.loads(attempt["rubric"] or "{}")
        scored = score_file(saved, rubric) if saved else {"score": 0, "max_score": 100, "checks": []}
        started = attempt["started_at"]
        now = datetime.now(timezone.utc)
        duration = None
        if started:
            if started.tzinfo is None:
                started = started.replace(tzinfo=timezone.utc)
            duration = int((now - started).total_seconds())
        cur.execute(
            """
            UPDATE attempts SET
              status = 'submitted',
              score = %s,
              max_score = %s,
              submitted_at = now(),
              duration_sec = %s,
              result_path = %s,
              payload = %s::jsonb,
              retries = retries + 1
            WHERE id = %s
            """,
            (
                scored["score"],
                scored.get("max_score") or 100,
                duration,
                str(saved) if saved else None,
                json.dumps({"checks": scored.get("checks")}, ensure_ascii=False),
                attempt_id,
            ),
        )
        cur.execute(
            """
            INSERT INTO telemetry (attempt_id, skill, action, detail)
            VALUES (%s, %s, 'submit', %s::jsonb)
            """,
            (
                attempt_id,
                attempt.get("skill_domain") or "",
                json.dumps({"score": scored["score"]}, ensure_ascii=False),
            ),
        )
    return {"ok": True, "score": scored, "duration_sec": duration}
