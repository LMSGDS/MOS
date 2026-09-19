"""Sự kiện nộp bài cho dashboard giáo viên (SSE, Cloudflare-friendly)."""
from __future__ import annotations

import asyncio
import json
from collections import defaultdict

from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import StreamingResponse

from app.db import cursor
from app.security import bearer_user

router = APIRouter(prefix="/api/v1")
_hub: dict[int, list[asyncio.Queue]] = defaultdict(list)


def publish_class(class_id: int | None, event: dict) -> None:
    rooms = [0]
    if class_id:
        rooms.append(int(class_id))
    payload = dict(event)
    for room in rooms:
        for queue in list(_hub.get(room) or []):
            try:
                queue.put_nowait(payload)
            except Exception:
                continue


def staff_from_request(request: Request) -> dict:
    user = None
    header = request.headers.get("authorization") or ""
    if header.lower().startswith("bearer "):
        try:
            user = bearer_user(request)
        except HTTPException:
            user = None
    if user is None:
        sess = request.session.get("user")
        if isinstance(sess, dict) and sess.get("username"):
            user = sess
    if not user:
        raise HTTPException(status_code=401, detail="token")
    with cursor() as cur:
        cur.execute("SELECT * FROM users WHERE username = %s", (user["username"],))
        row = cur.fetchone()
    if not row or row.get("role") not in ("admin", "teacher", "leadership"):
        raise HTTPException(status_code=403, detail="forbidden")
    return row


def list_class_sessions(class_id: int) -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT s.session_id, s.student_id, s.exam_id, s.class_id, s.status,
                   s.progress_pct, s.mode, s.score, s.verified_score,
                   s.updated_at, s.started_at, s.submitted_at,
                   u.name AS student, u.student_code, p.title
            FROM exam_sessions s
            JOIN users u ON u.id = s.student_id
            JOIN projects p ON p.id = s.exam_id
            WHERE (%s = 0 OR s.class_id = %s)
              AND (
                s.status IN ('IN_PROGRESS', 'SUBMITTED')
                OR s.updated_at > now() - interval '12 hours'
              )
            ORDER BY s.updated_at DESC NULLS LAST
            LIMIT 80
            """,
            (class_id, class_id),
        )
        return list(cur.fetchall())


@router.get("/classes/{class_id}/sessions")
def class_sessions(request: Request, class_id: int):
    staff_from_request(request)
    return {"ok": True, "class_id": class_id, "sessions": list_class_sessions(class_id)}


@router.get("/classes/{class_id}/live")
async def class_live(request: Request, class_id: int):
    staff_from_request(request)
    queue: asyncio.Queue = asyncio.Queue(maxsize=64)
    _hub[class_id].append(queue)

    async def stream():
        try:
            yield 'event: hello\ndata: {"ok":true}\n\n'
            while True:
                if await request.is_disconnected():
                    break
                try:
                    item = await asyncio.wait_for(queue.get(), timeout=20)
                    yield f"data: {json.dumps(item, ensure_ascii=False, default=str)}\n\n"
                except asyncio.TimeoutError:
                    yield ": keepalive\n\n"
        finally:
            try:
                _hub[class_id].remove(queue)
            except ValueError:
                pass

    return StreamingResponse(
        stream(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )
