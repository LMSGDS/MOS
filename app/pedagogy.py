"""Bốn chỉ số sư phạm: gợi ý sớm, sai lần 1, kẹt không cứu, dấu vết giáo viên."""
from __future__ import annotations

from app.db import cursor

HINT_EARLY_MS = 30_000
HINT_ALERT = 80.0
FIRST_FAIL_ALERT = 70.0
STUCK_SEC = 10 * 60
STUCK_ALERT = 40.0


def _pct(part, whole) -> float:
    whole = float(whole or 0)
    if whole <= 0:
        return 0.0
    return round(100.0 * float(part or 0) / whole, 1)


def class_hint_dependency(hours: int = 24) -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT
              a.class_id,
              COALESCE(c.name, 'Chưa xếp lớp') AS class_name,
              u.name AS teacher,
              c.teacher_id,
              COUNT(DISTINCT a.user_id) AS students,
              COUNT(DISTINCT a.user_id) FILTER (
                WHERE EXISTS (
                  SELECT 1 FROM telemetry t
                  WHERE t.attempt_id = a.id
                    AND t.action = 'hint'
                    AND COALESCE((t.detail->>'elapsed_ms')::int, 999999) <= %s
                )
              ) AS early_hint
            FROM attempts a
            LEFT JOIN classes c ON c.id = a.class_id
            LEFT JOIN users u ON u.id = c.teacher_id
            WHERE a.started_at > now() - (%s || ' hours')::interval
            GROUP BY a.class_id, c.name, u.name, c.teacher_id
            HAVING COUNT(DISTINCT a.user_id) > 0
            ORDER BY c.name NULLS LAST
            """,
            (HINT_EARLY_MS, hours),
        )
        rows = list(cur.fetchall())
    out = []
    for row in rows:
        rate = _pct(row["early_hint"], row["students"])
        out.append(
            {
                **row,
                "rate": rate,
                "alert": rate >= HINT_ALERT,
                "reading": (
                    f"{rate:.0f}% học sinh bấm Gợi ý trong 30 giây — học vẹt, thiếu bài giảng nền."
                    if rate >= HINT_ALERT
                    else "Ổn"
                ),
            }
        )
    return out


def class_first_attempt_fail(hours: int = 24) -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT
              a.class_id,
              COALESCE(c.name, 'Chưa xếp lớp') AS class_name,
              u.name AS teacher,
              COUNT(DISTINCT a.user_id) AS students,
              COUNT(DISTINCT a.user_id) FILTER (
                WHERE f.tool_fail > 0 OR f.locate_fail > 0
              ) AS first_fail
            FROM first_attempt_q f
            JOIN attempts a ON a.id = f.attempt_id
            LEFT JOIN classes c ON c.id = a.class_id
            LEFT JOIN users u ON u.id = c.teacher_id
            WHERE f.created_at > now() - (%s || ' hours')::interval
            GROUP BY a.class_id, c.name, u.name
            HAVING COUNT(DISTINCT a.user_id) > 0
            ORDER BY c.name NULLS LAST
            """,
            (hours,),
        )
        rows = list(cur.fetchall())
    out = []
    for row in rows:
        rate = _pct(row["first_fail"], row["students"])
        out.append(
            {
                **row,
                "rate": rate,
                "alert": rate >= FIRST_FAIL_ALERT,
                "reading": (
                    f"{rate:.0f}% sai bước Định vị/Công cụ ngay lần 1 — thiếu demo trên lớp."
                    if rate >= FIRST_FAIL_ALERT
                    else "Ổn"
                ),
            }
        )
    return out


def class_unresolved_stuck(hours: int = 24) -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT
              a.class_id,
              COALESCE(c.name, 'Chưa xếp lớp') AS class_name,
              u.name AS teacher,
              COUNT(*) AS submits,
              COUNT(*) FILTER (
                WHERE EXTRACT(EPOCH FROM (
                  a.submitted_at - COALESCE((
                    SELECT MAX(t.ts) FROM telemetry t
                    WHERE t.attempt_id = a.id
                      AND t.action NOT IN ('submit', 'submit-offline')
                  ), a.started_at)
                )) >= %s
                AND COALESCE(a.verified_score, a.score, 0) < 40
              ) AS unresolved
            FROM attempts a
            LEFT JOIN classes c ON c.id = a.class_id
            LEFT JOIN users u ON u.id = c.teacher_id
            WHERE a.status IN ('submitted', 'graded')
              AND a.submitted_at > now() - (%s || ' hours')::interval
            GROUP BY a.class_id, c.name, u.name
            HAVING COUNT(*) > 0
            ORDER BY c.name NULLS LAST
            """,
            (STUCK_SEC, hours),
        )
        rows = list(cur.fetchall())
    out = []
    for row in rows:
        rate = _pct(row["unresolved"], row["submits"])
        out.append(
            {
                **row,
                "rate": rate,
                "alert": rate >= STUCK_ALERT,
                "reading": (
                    f"{rate:.0f}% nộp sau ≥10 phút im lặng, điểm thấp — kẹt mà không được scaffolding."
                    if rate >= STUCK_ALERT
                    else "Ổn"
                ),
            }
        )
    return out


def teacher_footprint(hours: int = 24) -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT
              u.id AS teacher_id,
              u.name AS teacher,
              u.role,
              MAX(s.last_seen_at) AS last_seen_at,
              COALESCE(SUM(EXTRACT(EPOCH FROM (s.last_seen_at - s.started_at))), 0) AS session_sec,
              (
                SELECT COUNT(*) FROM staff_events e
                WHERE e.user_id = u.id
                  AND e.event IN ('live_open', 'heartbeat')
                  AND e.path LIKE '%%giam-sat%%'
                  AND e.created_at > now() - (%s || ' hours')::interval
              ) AS live_events,
              (
                SELECT COUNT(*) FROM staff_events e
                WHERE e.user_id = u.id
                  AND e.event = 'heartbeat'
                  AND e.created_at > now() - (%s || ' hours')::interval
              ) AS heartbeats
            FROM users u
            LEFT JOIN staff_sessions s
              ON s.user_id = u.id
             AND s.started_at > now() - (%s || ' hours')::interval
            WHERE u.role IN ('teacher', 'admin', 'leadership')
            GROUP BY u.id, u.name, u.role
            ORDER BY u.role, u.name
            """,
            (hours, hours, hours),
        )
        rows = list(cur.fetchall())
    out = []
    for row in rows:
        live = int(row["live_events"] or 0)
        beats = int(row["heartbeats"] or 0)
        minutes = round(float(row["session_sec"] or 0) / 60.0, 1)
        absent = row.get("role") == "teacher" and live == 0 and beats == 0
        out.append(
            {
                **row,
                "session_min": minutes,
                "live_open": live > 0,
                "alert": absent,
                "reading": (
                    "0 tương tác trên Giám sát phòng thi — không có dấu vết số trong ca."
                    if absent
                    else f"Online ~{minutes:.0f} phút · heartbeat giám sát: {beats}"
                ),
            }
        )
    return out


def pedagogy_alerts(hours: int = 24) -> list[dict]:
    hints = {row.get("class_id"): row for row in class_hint_dependency(hours)}
    firsts = {row.get("class_id"): row for row in class_first_attempt_fail(hours)}
    stuck = {row.get("class_id"): row for row in class_unresolved_stuck(hours)}
    feet = {row.get("teacher"): row for row in teacher_footprint(hours)}
    alerts = []
    keys = set(hints) | set(firsts) | set(stuck)
    for cid in keys:
        h = hints.get(cid) or {}
        f = firsts.get(cid) or {}
        s = stuck.get(cid) or {}
        teacher = h.get("teacher") or f.get("teacher") or s.get("teacher")
        foot = feet.get(teacher) or {}
        flags = []
        if h.get("alert"):
            flags.append("hint")
        if f.get("alert"):
            flags.append("first_fail")
        if s.get("alert"):
            flags.append("stuck")
        if foot.get("alert"):
            flags.append("no_live")
        if len(flags) < 2 and "no_live" not in flags:
            continue
        risk = "cao" if len(flags) >= 3 or ("no_live" in flags and len(flags) >= 2) else "theo dõi"
        alerts.append(
            {
                "class_id": cid,
                "class_name": h.get("class_name") or f.get("class_name") or s.get("class_name"),
                "teacher": teacher,
                "flags": flags,
                "risk": risk,
                "hint_rate": h.get("rate", 0),
                "first_fail_rate": f.get("rate", 0),
                "stuck_rate": s.get("rate", 0),
                "live_open": foot.get("live_open", False),
                "summary": (
                    f"{h.get('class_name') or 'Lớp'}: "
                    + "; ".join(
                        x
                        for x in (
                            h.get("reading") if h.get("alert") else "",
                            f.get("reading") if f.get("alert") else "",
                            s.get("reading") if s.get("alert") else "",
                            foot.get("reading") if foot.get("alert") else "",
                        )
                        if x and x != "Ổn"
                    )
                ),
            }
        )
    alerts.sort(key=lambda a: (0 if a["risk"] == "cao" else 1, a.get("class_name") or ""))
    return alerts
