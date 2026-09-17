"""Seed PostgreSQL from existing school accounts and MOS starter projects."""
from __future__ import annotations

import json
from pathlib import Path

from app.auth import load_users
from app.db import cursor
from app.grade import load_rubric, sha256_file

ROOT = Path(__file__).resolve().parent.parent
STATIC = ROOT / "app" / "static"
WORD11 = ROOT / "data" / "projects" / "word-objective-1-1" / "Word_1-1.docx"
WORD11_RUBRIC = ROOT / "app" / "rubrics" / "word-objective-1-1.json"
WORD12 = ROOT / "data" / "projects" / "word-objective-1-2" / "Word_1-2.docx"
WORD12_RUBRIC = ROOT / "app" / "rubrics" / "word-objective-1-2.json"
WORD13 = ROOT / "data" / "projects" / "word-objective-1-3" / "Word_1-3.docx"
WORD13_RUBRIC = ROOT / "app" / "rubrics" / "word-objective-1-3.json"
WORD14 = ROOT / "data" / "projects" / "word-objective-1-4" / "Word_1-4.docx"
WORD14_RUBRIC = ROOT / "app" / "rubrics" / "word-objective-1-4.json"


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
        word11_rubric = load_rubric(WORD11_RUBRIC) if WORD11_RUBRIC.is_file() else {}
        word12_rubric = load_rubric(WORD12_RUBRIC) if WORD12_RUBRIC.is_file() else {}
        word13_rubric = load_rubric(WORD13_RUBRIC) if WORD13_RUBRIC.is_file() else {}
        word14_rubric = load_rubric(WORD14_RUBRIC) if WORD14_RUBRIC.is_file() else {}
        projects = [
            {
                "id": "word-objective-1-1",
                "title": "Word 1.1 — Navigate within documents",
                "program": "word",
                "skill_domain": "Navigate within documents",
                "filename": "Word_1-1.docx",
                "file_path": str(WORD11),
                "rubric_version": word11_rubric.get("rubric_version") or "1.0.0",
                "steps": [
                    "Mở Word_1-1.docx trên Microsoft Word đã cài trên máy (không dùng Office Online).",
                    "Dùng Navigation pane tìm to, xem Results, đổi sang toy và chuyển giữa kết quả.",
                    "Tìm đúng từ Toymakers (hoa/thường); Advanced Find Toy/toy giới hạn Heading 2.",
                    "Bookmark SalesManager trên Lola Jacobsen và DesignManager trên Sarah Jones.",
                    "Tạo liên kết mục lục tới New Electronic Favorites, Why Buy Wingtip Toys?, Recognition, Make It Your Own, Hand-Carved Toys, Resources.",
                    "Go To Graphic đến cuối, Go To đầu trang 3, rồi Go To bookmark SalesManager.",
                    "Lưu bài. Trong luyện tập chọn Kiểm tra nhiệm vụ — 38 điểm Find/Go To chưa tự chấm nếu chưa có bộ ghi nhận thao tác.",
                ],
                "rubric": word11_rubric or {"rubric_version": "1.0.0"},
                "source_sha256": sha256_file(WORD11) if WORD11.is_file() else None,
            },
            {
                "id": "word-objective-1-2",
                "title": "Word 1.2 — Format documents",
                "program": "word",
                "skill_domain": "Format documents",
                "filename": "Word_1-2.docx",
                "file_path": str(WORD12),
                "rubric_version": word12_rubric.get("rubric_version") or "1.0.0",
                "steps": [
                    "Mở Word_1-2.docx trên Microsoft Word đã cài trên máy.",
                    "Design: Page Color, Watermark Example Only, Page Borders.",
                    "Insert Header: tiêu đề tài liệu, số trang; bật Different First Page.",
                    "Design > Style Sets: áp bộ làm Heading 1 cỡ 15 pt.",
                    "Lưu. Luyện tập: Kiểm tra nhiệm vụ.",
                ],
                "rubric": word12_rubric or {"rubric_version": "1.0.0"},
                "source_sha256": sha256_file(WORD12) if WORD12.is_file() else None,
            },
            {
                "id": "word-objective-1-3",
                "title": "Word 1.3 — Save and share documents",
                "program": "word",
                "skill_domain": "Save and share documents",
                "filename": "Word_1-3.docx",
                "file_path": str(WORD13),
                "rubric_version": word13_rubric.get("rubric_version") or "1.0.0",
                "steps": [
                    "Mở Word_1-3.docx trên Microsoft Word đã cài trên máy.",
                    "File > Info: Title Simple Room Design; Tags color; style; Status Draft.",
                    "Save As một bản PDF hoặc Word 97-2003 (nộp lại .docx gốc).",
                    "File > Print xem khổ giấy; File > Share (có thể hủy gửi).",
                    "Lưu .docx. Print/Share chưa tự chấm nếu chưa có bộ ghi nhận thao tác.",
                ],
                "rubric": word13_rubric or {"rubric_version": "1.0.0"},
                "source_sha256": sha256_file(WORD13) if WORD13.is_file() else None,
            },
            {
                "id": "word-objective-1-4",
                "title": "Word 1.4 — Inspect documents for issues",
                "program": "word",
                "skill_domain": "Inspect documents for issues",
                "filename": "Word_1-4.docx",
                "file_path": str(WORD14),
                "rubric_version": word14_rubric.get("rubric_version") or "1.0.0",
                "steps": [
                    "Mở Word_1-4.docx trên Microsoft Word đã cài trên máy.",
                    "Review: xóa hết Comments; Accept All Changes; tắt Track Changes.",
                    "Show/Hide ¶, bỏ Hidden text.",
                    "File > Info > Check for Issues: Inspect Document, Check Compatibility, Check Accessibility.",
                    "Lưu. Inspector/Compatibility chưa tự chấm nếu chưa có bộ ghi nhận thao tác.",
                ],
                "rubric": word14_rubric or {"rubric_version": "1.0.0"},
                "source_sha256": sha256_file(WORD14) if WORD14.is_file() else None,
            },
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
                INSERT INTO projects (id, title, program, skill_domain, filename, file_path, steps, rubric, rubric_version)
                VALUES (%s, %s, %s, %s, %s, %s, %s::jsonb, %s::jsonb, %s)
                ON CONFLICT (id) DO UPDATE SET
                  title = EXCLUDED.title,
                  file_path = EXCLUDED.file_path,
                  steps = EXCLUDED.steps,
                  rubric = EXCLUDED.rubric,
                  rubric_version = EXCLUDED.rubric_version
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
    print("seeded")


if __name__ == "__main__":
    seed()
