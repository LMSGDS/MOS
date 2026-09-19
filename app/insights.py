"""Phân tích sư phạm: lỗ hổng kiến thức, độ tin cậy đề, cảnh báo phòng thực hành."""
from __future__ import annotations

from datetime import datetime, timezone

from app.db import cursor

STUCK_SEC = 8 * 60
WRONG_TOOL_MIN = 2
GAP_RED = 75.0
BANK_REVIEW = 90.0


def _idle_sec(updated_at) -> int:
    if updated_at is None:
        return 0
    if isinstance(updated_at, str):
        try:
            updated_at = datetime.fromisoformat(updated_at.replace("Z", "+00:00"))
        except ValueError:
            return 0
    if updated_at.tzinfo is None:
        updated_at = updated_at.replace(tzinfo=timezone.utc)
    return max(0, int((datetime.now(timezone.utc) - updated_at).total_seconds()))


def skill_gaps(class_id: int = 0) -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT
              COALESCE(c.name, 'Chưa xếp lớp') AS class_name,
              a.class_id,
              p.id AS exam_id,
              p.title,
              COALESCE(NULLIF(p.skill_domain, ''), p.objective, p.id) AS skill,
              COUNT(*) AS n,
              COUNT(*) FILTER (WHERE q.status = 'fail') AS fails,
              COUNT(*) FILTER (WHERE q.locate = 'fail') AS locate_fail,
              COUNT(*) FILTER (WHERE q.tool = 'fail') AS tool_fail,
              COUNT(*) FILTER (WHERE q.configure = 'fail') AS configure_fail
            FROM q_matrix_results q
            JOIN attempts a ON a.id = q.attempt_id
            JOIN projects p ON p.id = a.project_id
            LEFT JOIN classes c ON c.id = a.class_id
            WHERE a.status IN ('submitted', 'graded')
              AND (%s = 0 OR a.class_id = %s)
            GROUP BY c.name, a.class_id, p.id, p.title, skill
            HAVING COUNT(*) > 0
            ORDER BY
              (100.0 * COUNT(*) FILTER (WHERE q.status = 'fail') / COUNT(*)) DESC,
              p.title
            """,
            (class_id, class_id),
        )
        rows = list(cur.fetchall())
    out = []
    for row in rows:
        n = int(row["n"] or 0)
        fails = int(row["fails"] or 0)
        fail_pct = round(100.0 * fails / n, 1) if n else 0.0
        heat = "red" if fail_pct >= GAP_RED else "amber" if fail_pct >= 50 else "green"
        out.append(
            {
                **row,
                "fail_pct": fail_pct,
                "heat": heat,
                "action": (
                    f"{fail_pct:.0f}% học sinh sai «{row['skill']}» — nên ôn lại chuyên đề này trên lớp."
                    if heat == "red"
                    else ""
                ),
            }
        )
    return out


def bank_reliability() -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT
              p.id AS exam_id,
              p.title,
              COALESCE(NULLIF(p.skill_domain, ''), p.objective, p.id) AS skill,
              p.program,
              COUNT(*) AS submits,
              COUNT(DISTINCT a.class_id) AS classes,
              COUNT(*) FILTER (WHERE COALESCE(a.verified_score, a.score, 0) < 50) AS low_score,
              COUNT(*) FILTER (WHERE COALESCE(a.verified_score, a.score, 0) < 70) AS below70,
              ROUND(AVG(COALESCE(a.verified_score, a.score))::numeric, 1) AS avg_score
            FROM attempts a
            JOIN projects p ON p.id = a.project_id
            WHERE a.status IN ('submitted', 'graded')
            GROUP BY p.id, p.title, skill, p.program
            ORDER BY
              (100.0 * COUNT(*) FILTER (WHERE COALESCE(a.verified_score, a.score, 0) < 50)
                 / NULLIF(COUNT(*), 0)) DESC NULLS LAST,
              p.title
            """
        )
        rows = list(cur.fetchall())
    out = []
    for row in rows:
        n = int(row["submits"] or 0)
        low = int(row["low_score"] or 0)
        below70 = int(row["below70"] or 0)
        low_pct = round(100.0 * low / n, 1) if n else 0.0
        below70_pct = round(100.0 * below70 / n, 1) if n else 0.0
        review = n >= 3 and (low_pct >= BANK_REVIEW or below70_pct >= BANK_REVIEW)
        out.append(
            {
                **row,
                "low_pct": low_pct,
                "below70_pct": below70_pct,
                "review": review,
                "verdict": (
                    "Cần rà soát đề — tỷ lệ sai quá cao ở nhiều lớp, có thể đề quá sức hoặc diễn đạt lệch."
                    if review
                    else "Ổn định"
                ),
            }
        )
    return out


def program_radar(user_id: int) -> list[dict]:
    from app.progress import get_evaluation, recompute_evaluation

    axes = []
    for program, label in (("word", "Word"), ("excel", "Excel"), ("powerpoint", "PowerPoint")):
        ev = get_evaluation(user_id, program) or recompute_evaluation(user_id, program) or {}
        weak = ev.get("weak_skills") or []
        if isinstance(weak, str):
            import json

            try:
                weak = json.loads(weak)
            except json.JSONDecodeError:
                weak = []
        axes.append(
            {
                "program": program,
                "label": label,
                "score": float(ev.get("overall_score") or 0),
                "completion_pct": float(ev.get("completion_pct") or 0),
                "level": ev.get("level") or "chua_bat_dau",
                "weak": weak,
            }
        )
    return axes


def annotate_sessions(rows: list[dict]) -> list[dict]:
    items = [dict(row) for row in rows]
    ids = [str(r.get("session_id") or "") for r in items if r.get("session_id")]
    tools: dict[str, int] = {}
    if ids:
        with cursor() as cur:
            cur.execute(
                """
                SELECT attempt_id,
                       COUNT(*) FILTER (WHERE tool = 'fail') AS tool_fail
                FROM q_matrix_results
                WHERE attempt_id = ANY(%s)
                GROUP BY attempt_id
                """,
                (ids,),
            )
            for row in cur.fetchall():
                tools[str(row["attempt_id"])] = int(row["tool_fail"] or 0)
    for row in items:
        sid = str(row.get("session_id") or "")
        idle = _idle_sec(row.get("updated_at"))
        alerts = []
        if str(row.get("status") or "") == "IN_PROGRESS" and idle >= STUCK_SEC:
            minutes = max(1, idle // 60)
            alerts.append(
                {
                    "code": "stuck",
                    "label": f"Kẹt {minutes} phút — giáo viên nên tới hỗ trợ",
                }
            )
        if tools.get(sid, 0) >= WRONG_TOOL_MIN:
            alerts.append(
                {
                    "code": "wrong_tool",
                    "label": "Liên tục chọn sai công cụ (Q-Matrix)",
                }
            )
        row["idle_sec"] = idle
        row["alerts"] = alerts
        row["alert"] = bool(alerts)
    return items
