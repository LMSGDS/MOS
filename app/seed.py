"""Seed PostgreSQL from existing school accounts and MOS starter projects."""
from __future__ import annotations

import json
from pathlib import Path

from app.auth import load_users
from app.db import cursor
from app.progress import assign_class_projects, backfill_all
from app import i18n
from app.grade import load_rubric, sha256_file

ROOT = Path(__file__).resolve().parent.parent
STATIC = ROOT / "app" / "static"
RUBRIC_DIR = ROOT / "app" / "rubrics"
PROJECTS_DIR = ROOT / "data" / "projects"


def _sort_order(tail: str) -> int:
    raw = tail.lower().replace("a", "1").replace("b", "2").replace("c", "3")
    parts = raw.split("-")
    nums = []
    for part in parts:
        nums.append(int(part) if part.isdigit() else 0)
    while len(nums) < 3:
        nums.append(0)
    return nums[0] * 10000 + nums[1] * 100 + nums[2]


def seed() -> None:
    users = load_users()
    with cursor() as cur:
        cur.execute(
            "INSERT INTO orgs (id, name) VALUES (1, %s) ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name",
            ("mos.gds.edu.vn",),
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
                INSERT INTO users (username, name, role, password_hash, org_id, student_code)
                VALUES (%s, %s, %s, %s, 1, %s)
                ON CONFLICT (username) DO UPDATE SET
                  name = EXCLUDED.name,
                  role = EXCLUDED.role,
                  password_hash = EXCLUDED.password_hash,
                  student_code = COALESCE(EXCLUDED.student_code, users.student_code)
                """,
                (
                    u["username"],
                    u.get("name") or u["username"],
                    role,
                    u["password_hash"],
                    "HS001" if u["username"] == "hocsinh" else None,
                ),
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
            extras = [
                ("hocsinh2", "Học sinh 2", "HS002"),
                ("hocsinh3", "Học sinh 3", "HS003"),
            ]
            cur.execute("SELECT password_hash FROM users WHERE username = %s", ("hocsinh",))
            pw = (cur.fetchone() or {}).get("password_hash")
            extra_ids = [student["id"]]
            if pw:
                for uname, name, code in extras:
                    cur.execute(
                        """
                        INSERT INTO users (username, name, role, password_hash, org_id, student_code)
                        VALUES (%s, %s, 'student', %s, 1, %s)
                        ON CONFLICT (username) DO UPDATE SET
                          name = EXCLUDED.name,
                          password_hash = EXCLUDED.password_hash,
                          student_code = COALESCE(EXCLUDED.student_code, users.student_code)
                        RETURNING id
                        """,
                        (uname, name, pw, code),
                    )
                    got = cur.fetchone()
                    if got:
                        extra_ids.append(got["id"])
                    else:
                        cur.execute("SELECT id FROM users WHERE username = %s", (uname,))
                        hit = cur.fetchone()
                        if hit:
                            extra_ids.append(hit["id"])
            for uid in extra_ids:
                cur.execute(
                    """
                    INSERT INTO enrollments (class_id, user_id) VALUES (1, %s)
                    ON CONFLICT DO NOTHING
                    """,
                    (uid,),
                )
        word_projects = []
        ppt_projects = []
        excel_projects = []
        for rubric_path in sorted(RUBRIC_DIR.glob("word-objective-*.json")):
            rubric = load_rubric(rubric_path)
            # `rubric` giữ NGUYÊN khối song ngữ để lưu vào DB — mất bản EN ở đây
            # là mất vĩnh viễn. `display` chỉ dùng để dựng chuỗi hiển thị.
            display = i18n.localize_rubric(rubric)
            pid = rubric.get("project_id") or rubric_path.stem
            tail = pid.removeprefix("word-objective-")
            filename = f"Word_{tail}.docx"
            file_path = PROJECTS_DIR / pid / filename
            prompts = [c.get("prompt") for c in display.get("criteria") or [] if c.get("prompt")]
            steps = [
                f"Mở {filename} trên Microsoft Word đã cài trên máy (không dùng Office Online).",
                *prompts,
                "Lưu bài. Trong luyện tập chọn Kiểm tra nhiệm vụ.",
            ]
            word_projects.append(
                {
                    "id": pid,
                    "title": display.get("title") or pid,
                    "program": "word",
                    "skill_domain": display.get("title") or pid,
                    "objective": str(rubric.get("objective") or tail),
                    "sort_order": _sort_order(tail),
                    "filename": filename,
                    "file_path": str(file_path),
                    "rubric_version": rubric.get("rubric_version") or "1.0.0",
                    "steps": steps,
                    "rubric": rubric,
                    "source_sha256": sha256_file(file_path) if file_path.is_file() else None,
                }
            )
        for rubric_path in sorted(RUBRIC_DIR.glob("powerpoint-objective-*.json")):
            rubric = load_rubric(rubric_path)
            # `rubric` giữ NGUYÊN khối song ngữ để lưu vào DB — mất bản EN ở đây
            # là mất vĩnh viễn. `display` chỉ dùng để dựng chuỗi hiển thị.
            display = i18n.localize_rubric(rubric)
            pid = rubric.get("project_id") or rubric_path.stem
            tail = pid.removeprefix("powerpoint-objective-")
            filename = f"PowerPoint_{tail}.pptx"
            file_path = PROJECTS_DIR / pid / filename
            prompts = [c.get("prompt") for c in display.get("criteria") or [] if c.get("prompt")]
            steps = [
                f"Mở {filename} trên Microsoft PowerPoint đã cài trên máy (không dùng Office Online).",
                *prompts,
                "Lưu bài. Trong luyện tập chọn Kiểm tra nhiệm vụ.",
            ]
            ppt_projects.append(
                {
                    "id": pid,
                    "title": display.get("title") or pid,
                    "program": "powerpoint",
                    "skill_domain": display.get("title") or pid,
                    "objective": str(rubric.get("objective") or tail),
                    "sort_order": _sort_order(tail),
                    "filename": filename,
                    "file_path": str(file_path),
                    "rubric_version": rubric.get("rubric_version") or "1.0.0",
                    "steps": steps,
                    "rubric": rubric,
                    "source_sha256": sha256_file(file_path) if file_path.is_file() else None,
                }
            )
        for rubric_path in sorted(RUBRIC_DIR.glob("excel-objective-*.json")):
            rubric = load_rubric(rubric_path)
            # `rubric` giữ NGUYÊN khối song ngữ để lưu vào DB — mất bản EN ở đây
            # là mất vĩnh viễn. `display` chỉ dùng để dựng chuỗi hiển thị.
            display = i18n.localize_rubric(rubric)
            pid = rubric.get("project_id") or rubric_path.stem
            tail = pid.removeprefix("excel-objective-")
            filename = f"Excel_{tail}.xlsx"
            file_path = PROJECTS_DIR / pid / filename
            prompts = [c.get("prompt") for c in display.get("criteria") or [] if c.get("prompt")]
            steps = [
                f"Mở {filename} trên Microsoft Excel đã cài trên máy (không dùng Office Online).",
                *prompts,
                "Lưu bài. Trong luyện tập chọn Kiểm tra nhiệm vụ.",
            ]
            excel_projects.append(
                {
                    "id": pid,
                    "title": display.get("title") or pid,
                    "program": "excel",
                    "skill_domain": display.get("title") or pid,
                    "objective": str(rubric.get("objective") or tail),
                    "sort_order": _sort_order(tail),
                    "filename": filename,
                    "file_path": str(file_path),
                    "rubric_version": rubric.get("rubric_version") or "1.0.0",
                    "steps": steps,
                    "rubric": rubric,
                    "source_sha256": sha256_file(file_path) if file_path.is_file() else None,
                }
            )
        projects = word_projects + excel_projects + ppt_projects + [
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
            version = p.get("rubric_version") or (p["rubric"].get("rubric_version") if isinstance(p["rubric"], dict) else None) or "legacy"
            cur.execute(
                """
                INSERT INTO projects (id, title, program, skill_domain, filename, file_path, steps, rubric, rubric_version, objective, sort_order)
                VALUES (%s, %s, %s, %s, %s, %s, %s::jsonb, %s::jsonb, %s, %s, %s)
                ON CONFLICT (id) DO UPDATE SET
                  title = EXCLUDED.title,
                  file_path = EXCLUDED.file_path,
                  steps = EXCLUDED.steps,
                  rubric = EXCLUDED.rubric,
                  rubric_version = EXCLUDED.rubric_version,
                  objective = EXCLUDED.objective,
                  sort_order = EXCLUDED.sort_order
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
                    version,
                    p.get("objective") or "",
                    int(p.get("sort_order") or 0),
                ),
            )
            version_id = f"{p['id']}:{version}"
            manifest = {
                "project_id": p["id"],
                "rubric_version": version,
                "filename": p["filename"],
                "sha256": p.get("source_sha256"),
                "capabilities": (p["rubric"].get("capabilities") if isinstance(p["rubric"], dict) else None) or [],
            }
            cur.execute(
                """
                INSERT INTO project_versions (id, project_id, rubric_version, manifest, rubric, source_sha256, published)
                VALUES (%s, %s, %s, %s::jsonb, %s::jsonb, %s, TRUE)
                ON CONFLICT (project_id, rubric_version) DO UPDATE SET
                  manifest = EXCLUDED.manifest,
                  rubric = EXCLUDED.rubric,
                  source_sha256 = COALESCE(EXCLUDED.source_sha256, project_versions.source_sha256)
                """,
                (
                    version_id,
                    p["id"],
                    version,
                    json.dumps(manifest, ensure_ascii=False),
                    json.dumps(p["rubric"], ensure_ascii=False),
                    p.get("source_sha256"),
                ),
            )
        word_ids = [p["id"] for p in word_projects]
        excel_ids = [p["id"] for p in excel_projects]
        ppt_ids = [p["id"] for p in ppt_projects]
        teacher_id = teacher["id"] if teacher else None
    if teacher_id and (word_ids or excel_ids or ppt_ids):
        assign_class_projects(1, word_ids + excel_ids + ppt_ids, assigned_by=teacher_id)
    from app.bank import seed_bank

    seed_bank()
    backfill_all()
    print("seeded")


if __name__ == "__main__":
    seed()
