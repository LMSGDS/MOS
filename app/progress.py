"""Tiến trình, tiến độ, tiến bộ và đánh giá học sinh theo bài tập MOS."""
from __future__ import annotations

import json
from datetime import datetime, timedelta, timezone

from app.db import cursor

LEVELS = {
    "chua_bat_dau": "Chưa bắt đầu",
    "bat_dau": "Bắt đầu",
    "dang_tien_bo": "Đang tiến bộ",
    "dat": "Đạt yêu cầu",
    "xuat_sac": "Xuất sắc",
}

STATUS_LABELS = {
    "not_started": "Chưa làm",
    "in_progress": "Đang làm",
    "submitted": "Đã nộp",
    "mastered": "Hoàn thành",
}


def _num(value):
    if value is None or value == "":
        return None
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def _int(value, default: int = 0) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return default


def _now() -> datetime:
    return datetime.now(timezone.utc)


def _criteria(payload: dict | None) -> list[dict]:
    if not isinstance(payload, dict):
        return []
    items = payload.get("criteria") or []
    return [c for c in items if isinstance(c, dict)]


def _next_status(current: str | None, event: str, verified, pending, attempt_status: str | None = None) -> str:
    current = current or "not_started"
    if current == "mastered":
        return "mastered"
    if verified is not None and float(verified) >= 100 and (pending is None or float(pending) <= 0):
        return "mastered"
    if event == "submit" or (attempt_status or "") in {"submitted", "graded"}:
        return "submitted"
    if event in {"start", "checkpoint", "evidence", "backfill"}:
        return "in_progress" if current == "not_started" else current
    return current or "in_progress"


def record_attempt_event(attempt: dict, *, event: str, payload: dict | None = None) -> None:
    if not attempt or not attempt.get("user_id") or not attempt.get("project_id"):
        return
    payload = payload or {}
    if not isinstance(payload, dict):
        payload = {}
    user_id = attempt["user_id"]
    project_id = str(attempt["project_id"])
    class_id = attempt.get("class_id")
    attempt_id = attempt.get("id")
    verified = _num(payload.get("verified", attempt.get("verified_score")))
    pending = _num(payload.get("pending", attempt.get("pending_score")))
    score = _num(payload.get("score", attempt.get("score")))
    criteria = _criteria(payload)
    passed = sum(1 for c in criteria if c.get("status") == "pass")
    total = len(criteria)
    evidence_count = _int(payload.get("evidence_count") or 0)
    now = _now()
    with cursor() as cur:
        cur.execute(
            """
            SELECT id FROM assignments
            WHERE project_id = %s AND class_id = %s
            """,
            (project_id, class_id),
        )
        asg = cur.fetchone()
        assignment_id = asg["id"] if asg else None
        cur.execute(
            "SELECT * FROM exercise_progress WHERE user_id = %s AND project_id = %s",
            (user_id, project_id),
        )
        row = cur.fetchone()
        current = row["status"] if row else "not_started"
        status = _next_status(current, event, verified, pending, attempt.get("status"))
        first_verified = _num(row["first_verified"]) if row else None
        if first_verified is None and verified is not None:
            first_verified = verified
        last_verified = verified if verified is not None else (_num(row["last_verified"]) if row else None)
        growth = 0.0
        if first_verified is not None and last_verified is not None:
            growth = round(last_verified - first_verified, 1)
        best = max(_num(row["best_verified"]) or 0 if row else 0, last_verified or 0)
        attempt_count = _int(row["attempt_count"] if row else 0)
        checkpoint_count = _int(row["checkpoint_count"] if row else 0)
        if event == "start":
            attempt_count += 1
        if event == "checkpoint":
            checkpoint_count += 1
        attempt_count = max(attempt_count, 1)
        if evidence_count == 0 and row:
            evidence_count = _int(row.get("evidence_count"))
        summary = {
            "grader_version": payload.get("grader_version"),
            "complete": payload.get("complete"),
            "event": event,
        }
        cur.execute(
            """
            INSERT INTO exercise_progress (
              user_id, project_id, class_id, assignment_id, attempt_id, status,
              best_verified, first_verified, last_verified, last_pending, last_score,
              attempt_count, checkpoint_count, evidence_count, criteria_passed, criteria_total,
              growth, first_started_at, last_activity_at, completed_at, mastery_at, payload
            ) VALUES (
              %s, %s, %s, %s, %s, %s,
              %s, %s, %s, %s, %s,
              %s, %s, %s, %s, %s,
              %s, %s, %s, %s, %s, %s::jsonb
            )
            ON CONFLICT (user_id, project_id) DO UPDATE SET
              class_id = COALESCE(EXCLUDED.class_id, exercise_progress.class_id),
              assignment_id = COALESCE(EXCLUDED.assignment_id, exercise_progress.assignment_id),
              attempt_id = COALESCE(EXCLUDED.attempt_id, exercise_progress.attempt_id),
              status = EXCLUDED.status,
              best_verified = GREATEST(exercise_progress.best_verified, EXCLUDED.best_verified),
              first_verified = COALESCE(exercise_progress.first_verified, EXCLUDED.first_verified),
              last_verified = COALESCE(EXCLUDED.last_verified, exercise_progress.last_verified),
              last_pending = COALESCE(EXCLUDED.last_pending, exercise_progress.last_pending),
              last_score = COALESCE(EXCLUDED.last_score, exercise_progress.last_score),
              attempt_count = EXCLUDED.attempt_count,
              checkpoint_count = EXCLUDED.checkpoint_count,
              evidence_count = GREATEST(exercise_progress.evidence_count, EXCLUDED.evidence_count),
              criteria_passed = EXCLUDED.criteria_passed,
              criteria_total = EXCLUDED.criteria_total,
              growth = EXCLUDED.growth,
              first_started_at = COALESCE(exercise_progress.first_started_at, EXCLUDED.first_started_at),
              last_activity_at = EXCLUDED.last_activity_at,
              completed_at = COALESCE(exercise_progress.completed_at, EXCLUDED.completed_at),
              mastery_at = COALESCE(exercise_progress.mastery_at, EXCLUDED.mastery_at),
              payload = exercise_progress.payload || EXCLUDED.payload
            """,
            (
                user_id,
                project_id,
                class_id,
                assignment_id,
                attempt_id,
                status,
                best,
                first_verified,
                last_verified,
                pending,
                score,
                attempt_count,
                checkpoint_count,
                evidence_count,
                passed,
                total,
                growth,
                now if event == "start" or not row else row.get("first_started_at") or now,
                now,
                now if status in {"submitted", "mastered"} else None,
                now if status == "mastered" else None,
                json.dumps(summary, ensure_ascii=False),
            ),
        )
        for item in criteria:
            cid = str(item.get("criterion_id") or "")
            if not cid:
                continue
            st = str(item.get("status") or "unverified")
            cur.execute(
                """
                INSERT INTO skill_progress (
                  user_id, project_id, criterion_id, skill_label, status, earned, possible,
                  attempts, first_pass_at, last_attempt_id, updated_at
                ) VALUES (%s, %s, %s, %s, %s, %s, %s, 1, %s, %s, %s)
                ON CONFLICT (user_id, project_id, criterion_id) DO UPDATE SET
                  skill_label = EXCLUDED.skill_label,
                  status = EXCLUDED.status,
                  earned = EXCLUDED.earned,
                  possible = EXCLUDED.possible,
                    attempts = skill_progress.attempts + CASE WHEN %s THEN 1 ELSE 0 END,
                  first_pass_at = COALESCE(skill_progress.first_pass_at, EXCLUDED.first_pass_at),
                  last_attempt_id = EXCLUDED.last_attempt_id,
                  updated_at = EXCLUDED.updated_at
                """,
                (
                    user_id,
                    project_id,
                    cid,
                    str(item.get("prompt") or cid),
                    st,
                    _num(item.get("earned")) or 0,
                    _num(item.get("possible")) or 0,
                    now if st == "pass" else None,
                    attempt_id,
                    now,
                    event != "backfill",
                ),
            )
        if event != "backfill":
            cur.execute(
                """
                INSERT INTO progress_events (
                  user_id, project_id, attempt_id, event, verified, pending, score,
                  criteria_passed, criteria_total, detail
                ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s::jsonb)
                """,
                (
                    user_id,
                    project_id,
                    attempt_id,
                    event,
                    verified,
                    pending,
                    score,
                    passed,
                    total,
                    json.dumps({"status": status, "growth": growth}, ensure_ascii=False),
                ),
            )
        cur.execute("SELECT program FROM projects WHERE id = %s", (project_id,))
        prog = cur.fetchone()
    program = (prog or {}).get("program") or "word"
    recompute_evaluation(user_id, program)


def recompute_evaluation(user_id: int, program: str = "word") -> dict:
    with cursor() as cur:
        cur.execute(
            """
            SELECT class_id FROM enrollments WHERE user_id = %s ORDER BY enrolled_at DESC LIMIT 1
            """,
            (user_id,),
        )
        enr = cur.fetchone()
        class_id = enr["class_id"] if enr else None
        cur.execute(
            """
            SELECT COUNT(*) AS n
            FROM assignments a
            JOIN projects p ON p.id = a.project_id
            WHERE a.class_id = %s AND p.program = %s
            """,
            (class_id, program),
        )
        assigned = _int((cur.fetchone() or {}).get("n"))
        if assigned == 0:
            cur.execute(
                "SELECT COUNT(*) AS n FROM projects WHERE program = %s AND published = TRUE",
                (program,),
            )
            assigned = _int((cur.fetchone() or {}).get("n"))
        cur.execute(
            """
            SELECT ep.status, ep.best_verified, ep.last_verified, ep.growth
            FROM exercise_progress ep
            JOIN projects p ON p.id = ep.project_id
            WHERE ep.user_id = %s AND p.program = %s
            """,
            (user_id, program),
        )
        rows = cur.fetchall()
        started = len(rows)
        completed = sum(1 for r in rows if r["status"] in {"submitted", "mastered"})
        mastered = sum(1 for r in rows if r["status"] == "mastered")
        scores = [_num(r["best_verified"]) or 0 for r in rows]
        growths = [_num(r["growth"]) or 0 for r in rows]
        avg_verified = round(sum(scores) / len(scores), 1) if scores else None
        avg_growth = round(sum(growths) / len(growths), 1) if growths else None
        denom = assigned or max(started, 1)
        completion_pct = round(100.0 * completed / denom, 1) if denom else 0.0
        cur.execute(
            """
            SELECT sp.criterion_id, sp.skill_label, sp.status, sp.earned, sp.possible, p.title
            FROM skill_progress sp
            JOIN projects p ON p.id = sp.project_id
            WHERE sp.user_id = %s AND p.program = %s
            """,
            (user_id, program),
        )
        skills = cur.fetchall()
        weak = [
            {"id": s["criterion_id"], "label": s["skill_label"], "status": s["status"], "title": s["title"]}
            for s in skills
            if s["status"] in {"fail", "unverified"}
        ][:8]
        strong = [
            {"id": s["criterion_id"], "label": s["skill_label"], "status": s["status"], "title": s["title"]}
            for s in skills
            if s["status"] == "pass"
        ][:8]
        overall = avg_verified if avg_verified is not None else 0.0
        if started == 0:
            level = "chua_bat_dau"
        elif completion_pct >= 90 and overall >= 90:
            level = "xuat_sac"
        elif completion_pct >= 70 and overall >= 70:
            level = "dat"
        elif started > 0 and (completion_pct >= 25 or overall >= 40):
            level = "dang_tien_bo"
        else:
            level = "bat_dau"
        cur.execute("SELECT name FROM users WHERE id = %s", (user_id,))
        name = ((cur.fetchone() or {}).get("name") or "Học sinh")
        summary = (
            f"{name}: {completed}/{assigned or started} bài {program} "
            f"({completion_pct}%). Điểm TB {overall:.0f}. "
            f"Tiến bộ trung bình {avg_growth if avg_growth is not None else 0:+.0f} điểm. "
            f"Mức đánh giá: {LEVELS[level]}."
        )
        cur.execute(
            """
            INSERT INTO student_evaluations (
              user_id, program, class_id, overall_score, exercises_assigned, exercises_started,
              exercises_completed, exercises_mastered, completion_pct, avg_verified, avg_growth,
              weak_skills, strong_skills, level, summary, computed_at
            ) VALUES (
              %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s::jsonb, %s::jsonb, %s, %s, now()
            )
            ON CONFLICT (user_id, program) DO UPDATE SET
              class_id = EXCLUDED.class_id,
              overall_score = EXCLUDED.overall_score,
              exercises_assigned = EXCLUDED.exercises_assigned,
              exercises_started = EXCLUDED.exercises_started,
              exercises_completed = EXCLUDED.exercises_completed,
              exercises_mastered = EXCLUDED.exercises_mastered,
              completion_pct = EXCLUDED.completion_pct,
              avg_verified = EXCLUDED.avg_verified,
              avg_growth = EXCLUDED.avg_growth,
              weak_skills = EXCLUDED.weak_skills,
              strong_skills = EXCLUDED.strong_skills,
              level = EXCLUDED.level,
              summary = EXCLUDED.summary,
              computed_at = now()
            """,
            (
                user_id,
                program,
                class_id,
                overall,
                assigned,
                started,
                completed,
                mastered,
                completion_pct,
                avg_verified,
                avg_growth,
                json.dumps(weak, ensure_ascii=False),
                json.dumps(strong, ensure_ascii=False),
                level,
                summary,
            ),
        )
    return get_evaluation(user_id, program)


def get_evaluation(user_id: int, program: str = "word") -> dict | None:
    with cursor() as cur:
        cur.execute(
            """
            SELECT e.*, u.name, u.username, u.student_code, c.name AS class_name
            FROM student_evaluations e
            JOIN users u ON u.id = e.user_id
            LEFT JOIN classes c ON c.id = e.class_id
            WHERE e.user_id = %s AND e.program = %s
            """,
            (user_id, program),
        )
        row = cur.fetchone()
    if not row:
        return None
    row = dict(row)
    row["level_label"] = LEVELS.get(row.get("level"), row.get("level"))
    return row


def list_student_exercises(user_id: int, program: str | None = None) -> list[dict]:
    sql = """
        SELECT p.id AS project_id, p.title, p.program, p.objective, p.skill_domain,
               ep.status, ep.best_verified, ep.last_verified, ep.last_pending, ep.growth,
               ep.attempt_count, ep.checkpoint_count, ep.evidence_count,
               ep.criteria_passed, ep.criteria_total, ep.last_activity_at,
               a.id AS assignment_id, a.due_at, a.mode AS assign_mode
        FROM projects p
        LEFT JOIN exercise_progress ep ON ep.project_id = p.id AND ep.user_id = %s
        LEFT JOIN enrollments en ON en.user_id = %s
        LEFT JOIN assignments a ON a.project_id = p.id AND a.class_id = en.class_id
        WHERE p.published = TRUE
    """
    params: list = [user_id, user_id]
    if program:
        sql += " AND p.program = %s"
        params.append(program)
    sql += " ORDER BY p.program, p.sort_order, p.title"
    with cursor() as cur:
        cur.execute(sql, tuple(params))
        rows = cur.fetchall()
    out = []
    for row in rows:
        item = dict(row)
        item["status"] = item.get("status") or "not_started"
        item["status_label"] = STATUS_LABELS.get(item["status"], item["status"])
        out.append(item)
    return out


def student_timeline(user_id: int, limit: int = 40) -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT pe.*, p.title
            FROM progress_events pe
            JOIN projects p ON p.id = pe.project_id
            WHERE pe.user_id = %s
            ORDER BY pe.created_at DESC
            LIMIT %s
            """,
            (user_id, limit),
        )
        return cur.fetchall()


def student_skills(user_id: int, program: str = "word") -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT sp.*, p.title
            FROM skill_progress sp
            JOIN projects p ON p.id = sp.project_id
            WHERE sp.user_id = %s AND p.program = %s
            ORDER BY CASE sp.status WHEN 'fail' THEN 0 WHEN 'unverified' THEN 1 ELSE 2 END,
                     p.title, sp.criterion_id
            """,
            (user_id, program),
        )
        return cur.fetchall()


def list_roster(class_id: int | None = None) -> list[dict]:
    sql = """
        SELECT u.id, u.username, u.name, u.student_code, u.role,
               u.last_seen_at, u.last_client, u.created_at,
               c.id AS class_id, c.name AS class_name,
               e.overall_score, e.completion_pct, e.level, e.exercises_completed,
               e.exercises_assigned, e.exercises_mastered, e.avg_growth, e.summary
        FROM users u
        LEFT JOIN enrollments en ON en.user_id = u.id
        LEFT JOIN classes c ON c.id = en.class_id
        LEFT JOIN student_evaluations e ON e.user_id = u.id AND e.program = 'word'
        WHERE u.role = 'student'
    """
    params: list = []
    if class_id:
        sql += " AND en.class_id = %s"
        params.append(class_id)
    sql += " ORDER BY c.name NULLS LAST, u.name"
    with cursor() as cur:
        cur.execute(sql, tuple(params))
        rows = cur.fetchall()
    now = datetime.now(timezone.utc)
    out = []
    for row in rows:
        item = dict(row)
        item["level_label"] = LEVELS.get(item.get("level") or "chua_bat_dau")
        seen = item.get("last_seen_at")
        created = item.get("created_at")
        if seen and getattr(seen, "tzinfo", None) is None:
            seen = seen.replace(tzinfo=timezone.utc)
        if created and getattr(created, "tzinfo", None) is None:
            created = created.replace(tzinfo=timezone.utc)
        online = bool(seen and now - seen <= timedelta(minutes=10))
        item["online"] = online
        stale = timedelta(days=7)
        item["flag_inactive"] = bool(
            (seen and now - seen > stale)
            or (seen is None and created and now - created > stale)
        )
        if online and (item.get("last_client") or "") == "kulkul":
            item["presence"] = "Đang luyện tập"
        elif online:
            item["presence"] = "Đang online"
        else:
            item["presence"] = "Đang offline"
        out.append(item)
    return out


def list_exercises() -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT p.id, p.title, p.program, p.objective, p.skill_domain, p.rubric_version,
                   COUNT(DISTINCT a.class_id) AS class_count,
                   COUNT(DISTINCT ep.user_id) AS students_started,
                   COUNT(DISTINCT ep.user_id) FILTER (WHERE ep.status IN ('submitted', 'mastered')) AS students_done,
                   COALESCE(AVG(ep.best_verified), 0) AS avg_best
            FROM projects p
            LEFT JOIN assignments a ON a.project_id = p.id
            LEFT JOIN exercise_progress ep ON ep.project_id = p.id
            WHERE p.published = TRUE
            GROUP BY p.id
            ORDER BY p.program, p.sort_order, p.title
            """
        )
        return cur.fetchall()


def assign_class_projects(class_id: int, project_ids: list[str], assigned_by: int | None = None) -> int:
    count = 0
    with cursor() as cur:
        for pid in project_ids:
            cur.execute(
                """
                INSERT INTO assignments (project_id, class_id, assigned_by)
                VALUES (%s, %s, %s)
                ON CONFLICT (project_id, class_id) DO NOTHING
                """,
                (pid, class_id, assigned_by),
            )
            if cur.rowcount:
                count += 1
        cur.execute("SELECT user_id FROM enrollments WHERE class_id = %s", (class_id,))
        students = [r["user_id"] for r in cur.fetchall()]
    for uid in students:
        recompute_evaluation(uid, "word")
    return count


def backfill_all() -> int:
    with cursor() as cur:
        cur.execute(
            """
            SELECT a.*, p.program
            FROM attempts a
            JOIN projects p ON p.id = a.project_id
            WHERE a.verified_score IS NOT NULL OR a.status IN ('submitted', 'graded')
            ORDER BY a.started_at
            """
        )
        rows = cur.fetchall()
    n = 0
    for attempt in rows:
        payload = attempt.get("payload") if isinstance(attempt.get("payload"), dict) else {}
        if not payload:
            payload = {
                "verified": attempt.get("verified_score"),
                "pending": attempt.get("pending_score"),
                "score": attempt.get("score"),
            }
        event = "backfill"
        record_attempt_event(attempt, event=event, payload=payload)
        n += 1
    with cursor() as cur:
        cur.execute("SELECT id FROM users WHERE role = 'student'")
        students = [r["id"] for r in cur.fetchall()]
    for uid in students:
        recompute_evaluation(uid, "word")
    return n
