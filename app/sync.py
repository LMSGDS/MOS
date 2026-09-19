"""Offline-first sync: Last-Write-Wins, một phiên đang mở, điểm chỉ từ máy chủ."""
from __future__ import annotations

import hashlib
from datetime import datetime, timezone

from app.db import cursor


def parse_ts(value) -> datetime | None:
    if value is None or value == "":
        return None
    if isinstance(value, datetime):
        return value if value.tzinfo else value.replace(tzinfo=timezone.utc)
    text = str(value).replace("Z", "+00:00")
    try:
        parsed = datetime.fromisoformat(text)
    except ValueError:
        return None
    return parsed if parsed.tzinfo else parsed.replace(tzinfo=timezone.utc)


def progress_from_score(payload: dict | None) -> int:
    if not payload:
        return 0
    max_score = float(payload.get("max_score") or 100) or 100
    earned = payload.get("verified")
    if earned is None:
        earned = payload.get("score") or 0
    pct = int(round(100 * float(earned or 0) / max_score))
    return max(0, min(100, pct))


def verify_artifact_sha256(data: bytes | None, claimed: str | None) -> str | None:
    """Trả digest thật. claimed lệch → ValueError (không tin điểm client)."""
    if not data:
        return None
    digest = hashlib.sha256(data).hexdigest()
    want = (claimed or "").strip().lower()
    if want and want != digest:
        raise ValueError("artifact_sha256")
    return digest


def abandon_other_running(user_id: int, project_id: str, keep_id: str) -> int:
    with cursor() as cur:
        cur.execute(
            """
            UPDATE attempts
            SET status = 'abandoned', updated_at = now()
            WHERE user_id = %s AND project_id = %s AND status = 'running' AND id <> %s
            """,
            (user_id, project_id, keep_id),
        )
        return cur.rowcount or 0


def reactivate_if_abandoned(attempt: dict) -> dict:
    """Máy trường mở bài mới bỏ dở phiên cũ — máy có updated_at mới hơn được mở lại."""
    if not attempt or attempt.get("status") != "abandoned":
        return attempt
    with cursor() as cur:
        cur.execute(
            """
            UPDATE attempts
            SET status = 'running', updated_at = now()
            WHERE id = %s AND status = 'abandoned'
            """,
            (attempt["id"],),
        )
    abandon_other_running(attempt["user_id"], attempt["project_id"], attempt["id"])
    attempt["status"] = "running"
    return attempt


def apply_client_clock(attempt_id: str, client_updated_at: str | None) -> dict:
    incoming = parse_ts(client_updated_at)
    with cursor() as cur:
        cur.execute(
            "SELECT client_updated_at, updated_at FROM attempts WHERE id = %s",
            (attempt_id,),
        )
        row = cur.fetchone()
        if not row:
            return {"accepted": False, "stale": False}
        current = parse_ts(row.get("client_updated_at"))
        if incoming and current and incoming < current:
            return {
                "accepted": False,
                "stale": True,
                "kept_at": current.isoformat(),
            }
        cur.execute(
            """
            UPDATE attempts
            SET updated_at = now(),
                client_updated_at = COALESCE(%s, client_updated_at, now())
            WHERE id = %s
            """,
            (incoming, attempt_id),
        )
    return {"accepted": True, "stale": False}


def q_axis(item: dict) -> tuple[str, str, str]:
    """Q-Matrix: định vị → chọn công cụ → cấu hình. Suy từ rubric nếu client không gửi."""
    locate = str(item.get("locate") or item.get("q_locate") or "")
    tool = str(item.get("tool") or item.get("q_tool") or "")
    configure = str(item.get("configure") or item.get("q_configure") or "")
    if locate or tool or configure:
        return locate, tool, configure
    status = str(item.get("status") or "")
    reason = str(item.get("reason_code") or "").lower()
    if status == "pass":
        return "pass", "pass", "pass"
    if status != "fail":
        return "", "", status
    if any(token in reason for token in ("missing", "not_found", "absent", "locate")):
        return "fail", "", ""
    if any(token in reason for token in ("tool", "action", "command", "ribbon")):
        return "pass", "fail", ""
    return "pass", "pass", "fail"


def store_q_matrix(attempt_id: str, scored: dict | None) -> int:
    items = (scored or {}).get("criteria") or (scored or {}).get("results") or []
    if not isinstance(items, list) or not items:
        return 0
    written = 0
    with cursor() as cur:
        cur.execute("DELETE FROM q_matrix_results WHERE attempt_id = %s", (attempt_id,))
        for item in items:
            if not isinstance(item, dict):
                continue
            cid = str(item.get("criterion_id") or item.get("id") or "")
            if not cid:
                continue
            locate, tool, configure = q_axis(item)
            cur.execute(
                """
                INSERT INTO q_matrix_results (
                  attempt_id, criterion_id, locate, tool, configure, status, earned, possible
                ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (
                    attempt_id,
                    cid,
                    locate,
                    tool,
                    configure,
                    str(item.get("status") or ""),
                    item.get("earned") or 0,
                    item.get("possible") or 0,
                ),
            )
            written += 1
        if written:
            cur.execute(
                """
                INSERT INTO first_attempt_q (
                  attempt_id, locate_fail, tool_fail, configure_fail, fail_count, item_count
                )
                SELECT
                  %s,
                  COUNT(*) FILTER (WHERE locate = 'fail'),
                  COUNT(*) FILTER (WHERE tool = 'fail'),
                  COUNT(*) FILTER (WHERE configure = 'fail'),
                  COUNT(*) FILTER (WHERE status = 'fail'),
                  COUNT(*)
                FROM q_matrix_results
                WHERE attempt_id = %s
                ON CONFLICT (attempt_id) DO NOTHING
                """,
                (attempt_id, attempt_id),
            )
    return written
