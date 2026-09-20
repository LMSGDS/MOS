"""Super Admin macro metrics, hierarchy, and org settings — no Redis."""
from __future__ import annotations

import os
from datetime import date, datetime, timedelta
from pathlib import Path

from app.db import cursor
from app.insights import skill_gaps


def client_version() -> str:
    path = Path(__file__).resolve().parent.parent / "desktop" / "MosDock" / "MosDock.csproj"
    try:
        text = path.read_text(encoding="utf-8")
    except OSError:
        return ""
    start = text.find("<Version>")
    end = text.find("</Version>", start)
    if start < 0 or end < 0:
        return ""
    return text[start + 9 : end].strip()


def postgres_ok() -> bool:
    try:
        with cursor() as cur:
            cur.execute("SELECT 1")
        return True
    except Exception:
        return False


def host_load() -> dict:
    cpu_pct = 0.0
    ram_used = 0.0
    ram_total = 0.0
    try:
        load = float(os.getloadavg()[0])
        cpus = os.cpu_count() or 1
        cpu_pct = min(100.0, round(100.0 * load / cpus, 1))
    except (OSError, AttributeError):
        pass
    try:
        info = {}
        with open("/proc/meminfo", encoding="utf-8") as fh:
            for line in fh:
                key, raw, *_ = line.replace(":", " ").split()
                if key in ("MemTotal", "MemAvailable"):
                    info[key] = int(raw)
        total = info.get("MemTotal") or 0
        avail = info.get("MemAvailable") or 0
        ram_total = round(total / 1024 / 1024, 1)
        ram_used = round((total - avail) / 1024 / 1024, 1)
    except OSError:
        pass
    return {"cpu_pct": cpu_pct, "ram_used_gb": ram_used, "ram_total_gb": ram_total}


def adoption_series(days: int = 30) -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT DATE(started_at) AS day, COUNT(*) AS n
            FROM attempts
            WHERE started_at > now() - (%s || ' days')::interval
            GROUP BY 1
            ORDER BY 1
            """,
            (days,),
        )
        found = {}
        for row in cur.fetchall():
            raw = row["day"]
            if isinstance(raw, datetime):
                key = raw.date()
            elif isinstance(raw, date):
                key = raw
            else:
                key = date.fromisoformat(str(raw)[:10])
            found[key] = int(row["n"])
        cur.execute(
            """
            SELECT COUNT(*) AS n FROM attempts
            WHERE started_at > date_trunc('month', now())
            """
        )
        month_n = int(cur.fetchone()["n"])
        cur.execute(
            """
            SELECT COUNT(*) AS n FROM attempts
            WHERE started_at >= date_trunc('month', now()) - interval '1 month'
              AND started_at < date_trunc('month', now())
            """
        )
        prev_n = int(cur.fetchone()["n"])
    series = []
    today = date.today()
    for i in range(days - 1, -1, -1):
        day = today - timedelta(days=i)
        series.append({"day": day.isoformat()[5:], "n": found.get(day, 0)})
    delta = 0.0
    if prev_n:
        delta = round(100.0 * (month_n - prev_n) / prev_n, 1)
    return {"days": series, "month_n": month_n, "prev_n": prev_n, "delta_pct": delta}


def grade_radar() -> dict:
    with cursor() as cur:
        cur.execute(
            """
            SELECT
              CASE
                WHEN c.name ~ '^10' THEN '10'
                WHEN c.name ~ '^11' THEN '11'
                WHEN c.name ~ '^12' THEN '12'
                ELSE 'khac'
              END AS grade,
              p.program,
              ROUND(AVG(COALESCE(a.verified_score, a.score))::numeric, 1) AS score
            FROM attempts a
            JOIN projects p ON p.id = a.project_id
            LEFT JOIN classes c ON c.id = a.class_id
            WHERE a.status IN ('submitted', 'graded')
            GROUP BY 1, 2
            """
        )
        rows = cur.fetchall()
    out = {g: {"word": 0.0, "excel": 0.0, "powerpoint": 0.0} for g in ("10", "11", "12")}
    for row in rows:
        grade = row["grade"]
        if grade not in out:
            continue
        prog = row["program"]
        if prog in out[grade]:
            out[grade][prog] = float(row["score"] or 0)
    return out


def dashboard_insight(radar: dict, skills: list[dict]) -> str:
    weakest = None
    for grade, axes in radar.items():
        for prog, score in axes.items():
            if score <= 0:
                continue
            if weakest is None or score < weakest[2]:
                weakest = (grade, prog, score)
    if weakest and weakest[2] < 60:
        label = {"word": "Word", "excel": "Excel", "powerpoint": "PowerPoint"}[weakest[1]]
        return (
            f"Khối {weakest[0]} đang có điểm trung bình {label} "
            f"({weakest[2]:.0f}) thấp hơn mục tiêu. "
            f"Đề xuất: tăng cường giao bài luyện tập module này trong 2 tuần tới."
        )
    if skills:
        skill = skills[0]
        return (
            f"Chuyên đề yếu nhất toàn trường: {skill.get('skill') or '—'} "
            f"(điểm TB {float(skill.get('avg_score') or 0):.0f}). "
            f"Tổ chuyên môn nên rà soát giáo án và ngân hàng đề."
        )
    return "Chưa đủ dữ liệu bài nộp để khuyến nghị quản trị."


def list_teacher_hub() -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT u.id, u.username, u.name, COALESCE(u.is_active, TRUE) AS is_active,
                   COUNT(c.id) AS class_count
            FROM users u
            LEFT JOIN classes c ON c.teacher_id = u.id
            WHERE u.role = 'teacher'
            GROUP BY u.id
            ORDER BY u.name
            """
        )
        return [dict(row) for row in cur.fetchall()]


def roster_matrix() -> dict:
    with cursor() as cur:
        cur.execute("SELECT id, name, teacher_id FROM classes ORDER BY name")
        classes = [dict(row) for row in cur.fetchall()]
        cur.execute("SELECT id, name FROM users WHERE role = 'teacher' ORDER BY name")
        teachers = [dict(row) for row in cur.fetchall()]
    assigned = {c["id"]: c.get("teacher_id") for c in classes}
    return {"classes": classes, "teachers": teachers, "assigned": assigned}


def save_matrix(pairs: list[tuple[int, int]]) -> int:
    by_class: dict[int, int] = {}
    for teacher_id, class_id in pairs:
        by_class[class_id] = teacher_id
    with cursor() as cur:
        cur.execute("UPDATE classes SET teacher_id = NULL")
        for class_id, teacher_id in by_class.items():
            cur.execute(
                "UPDATE classes SET teacher_id = %s WHERE id = %s",
                (teacher_id, class_id),
            )
    return len(by_class)


def set_active(user_id: int, active: bool) -> dict | None:
    with cursor() as cur:
        cur.execute(
            """
            UPDATE users SET is_active = %s
            WHERE id = %s AND role IN ('teacher', 'student')
            RETURNING id, username, name, role, is_active
            """,
            (active, user_id),
        )
        row = cur.fetchone()
    return dict(row) if row else None


def transfer_student(user_id: int, class_id: int) -> None:
    with cursor() as cur:
        cur.execute("SELECT id, role FROM users WHERE id = %s", (user_id,))
        row = cur.fetchone()
        if not row or row["role"] != "student":
            raise ValueError("student")
        cur.execute("SELECT id FROM classes WHERE id = %s", (class_id,))
        if not cur.fetchone():
            raise ValueError("class")
        cur.execute("DELETE FROM enrollments WHERE user_id = %s", (user_id,))
        cur.execute(
            """
            INSERT INTO enrollments (class_id, user_id) VALUES (%s, %s)
            ON CONFLICT DO NOTHING
            """,
            (class_id, user_id),
        )


def compare_classes(left_id: int, right_id: int) -> dict:
    return {"left": skill_gaps(left_id), "right": skill_gaps(right_id)}


def get_setting(key: str, default: str = "") -> str:
    try:
        with cursor() as cur:
            cur.execute("SELECT value FROM org_settings WHERE key = %s", (key,))
            row = cur.fetchone()
        return (row["value"] if row else default) or default
    except Exception:
        return default


def set_setting(key: str, value: str) -> None:
    with cursor() as cur:
        cur.execute(
            """
            INSERT INTO org_settings (key, value) VALUES (%s, %s)
            ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value
            """,
            (key, value),
        )
