"""Seed PostgreSQL from existing school accounts and MOS starter projects."""
from __future__ import annotations

import json
from pathlib import Path

from app.auth import load_users
from app.db import cursor

ROOT = Path(__file__).resolve().parent.parent
STATIC = ROOT / "app" / "static"


def seed() -> None:
    users = load_users()
    with cursor() as cur:
        cur.execute(
            "INSERT INTO orgs (id, name) VALUES (1, %s) ON CONFLICT (id) DO NOTHING",
            ("Trường GDS",),
        )
        cur.execute(
            "SELECT setval(pg_get_serial_sequence('orgs', 'id'), GREATEST(1, COALESCE((SELECT MAX(id) FROM orgs), 1)))"
        )
        for u in users:
            role = u.get("role") or "student"
            if role == "user":
                role = "student"
            cur.execute(
                """
                INSERT INTO users (username, name, role, password_hash, org_id)
                VALUES (%s, %s, %s, %s, 1)
                ON CONFLICT (username) DO UPDATE SET
                  name = EXCLUDED.name,
                  role = EXCLUDED.role,
                  password_hash = EXCLUDED.password_hash
                """,
                (u["username"], u.get("name") or u["username"], role, u["password_hash"]),
            )
        cur.execute(
            "SELECT setval(pg_get_serial_sequence('users', 'id'), GREATEST(1, (SELECT COALESCE(MAX(id), 1) FROM users)))"
        )
        cur.execute("SELECT id FROM users WHERE username = %s", ("giaovien",))
        teacher = cur.fetchone()
        cur.execute("SELECT id FROM users WHERE username = %s", ("hocsinh",))
        student = cur.fetchone()
        if teacher:
            cur.execute(
                """
                INSERT INTO classes (id, name, org_id, teacher_id, curriculum)
                VALUES (1, %s, 1, %s, %s::jsonb)
                ON CONFLICT (id) DO UPDATE SET teacher_id = EXCLUDED.teacher_id
                """,
                (
                    "10A1",
                    teacher["id"],
                    json.dumps(
                        {"word": "Mail Merge, Định dạng", "excel": "Công thức", "powerpoint": "Slide master"},
                        ensure_ascii=False,
                    ),
                ),
            )
            cur.execute(
                "SELECT setval(pg_get_serial_sequence('classes', 'id'), GREATEST(1, COALESCE((SELECT MAX(id) FROM classes), 1)))"
            )
        if teacher and student:
            cur.execute(
                """
                INSERT INTO enrollments (class_id, user_id) VALUES (1, %s)
                ON CONFLICT DO NOTHING
                """,
                (student["id"],),
            )
        projects = [
            {
                "id": "word-mail-merge",
                "title": "Word — Mail Merge thư mời",
                "program": "word",
                "skill_domain": "Mail Merge",
                "filename": "mau-van-ban-mos.docx",
                "file_path": str(STATIC / "mau-van-ban.docx"),
                "steps": [
                    "Mở tệp bài trên Microsoft Word (máy).",
                    "Thực hiện Mail Merge theo hướng dẫn đề.",
                    "Lưu bài rồi nộp từ MOS-KulKul.",
                ],
                "rubric": {"contains_text": ["MOS"], "min_chars": 20},
            },
            {
                "id": "excel-cong-thuc",
                "title": "Excel — Công thức tính điểm",
                "program": "excel",
                "skill_domain": "Công thức",
                "filename": "mau-bang-tinh-mos.xlsx",
                "file_path": str(STATIC / "mau-bang-tinh.xlsx"),
                "steps": [
                    "Mở tệp trên Microsoft Excel.",
                    "Dùng công thức, không gõ tay kết quả.",
                    "Lưu và nộp bài.",
                ],
                "rubric": {"min_chars": 8},
            },
            {
                "id": "ppt-slide-master",
                "title": "PowerPoint — Bố cục slide",
                "program": "powerpoint",
                "skill_domain": "Slide master",
                "filename": "mau-bai-trinh-bay-mos.pptx",
                "file_path": str(STATIC / "mau-bai-trinh-bay.pptx"),
                "steps": [
                    "Mở tệp trên Microsoft PowerPoint.",
                    "Chỉnh bố cục slide theo đề.",
                    "Lưu và nộp bài.",
                ],
                "rubric": {"min_chars": 8},
            },
        ]
        for p in projects:
            cur.execute(
                """
                INSERT INTO projects (id, title, program, skill_domain, filename, file_path, steps, rubric)
                VALUES (%s, %s, %s, %s, %s, %s, %s::jsonb, %s::jsonb)
                ON CONFLICT (id) DO UPDATE SET
                  title = EXCLUDED.title,
                  file_path = EXCLUDED.file_path,
                  steps = EXCLUDED.steps,
                  rubric = EXCLUDED.rubric
                """,
                (
                    p["id"],
                    p["title"],
                    p["program"],
                    p["skill_domain"],
                    p["filename"],
                    p["file_path"],
                    json.dumps(p["steps"], ensure_ascii=False),
                    json.dumps(p["rubric"], ensure_ascii=False),
                ),
            )
