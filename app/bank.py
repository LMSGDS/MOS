"""Ngân hàng đề 3 tầng: Objective Domains → Project Blocks → Atomic Tasks → Exams.

Không MinIO / Redis: file nằm data/projects, payload JSON xuống MOS-KulKul.
"""
from __future__ import annotations

import hashlib
import json
import random
import secrets
from datetime import datetime, timezone
from functools import wraps
from itertools import combinations
from pathlib import Path

from app.db import as_service, cursor


def _engine(fn):
    """Chấm / compile payload: đọc ngân hàng dù request đang là học sinh."""

    @wraps(fn)
    def wrapped(*args, **kwargs):
        with as_service():
            return fn(*args, **kwargs)

    return wrapped

ROOT = Path(__file__).resolve().parent.parent
BANK_FILES = ROOT / "data" / "projects" / "bank"

SUBJECTS = (
    ("MO-100", "word", "Word MOS 2019"),
    ("MO-200", "excel", "Excel MOS 2019"),
    ("MO-300", "powerpoint", "PowerPoint MOS 2019"),
)

# MOS Study Guide 2019 — Tầng 1 khóa taxonomy (chỉ Super Admin sửa).
TAXONOMY: dict[str, list[tuple[str, str, list[tuple[str, str]]]]] = {
    "MO-100": [
        ("1", "Manage Documents", [
            ("1.1", "Navigate within documents"),
            ("1.2", "Format documents"),
            ("1.3", "Save and share documents"),
            ("1.4", "Inspect documents for issues"),
        ]),
        ("2", "Insert and Format Text, Paragraphs, and Sections", [
            ("2.1", "Insert text and paragraphs"),
            ("2.2", "Format text and paragraphs"),
            ("2.3", "Create and configure document sections"),
        ]),
        ("3", "Manage Tables and Lists", [
            ("3.1", "Create tables"),
            ("3.2", "Modify tables"),
            ("3.3", "Create and modify lists"),
        ]),
        ("4", "Create and Manage References", [
            ("4.1", "Create and manage reference elements"),
            ("4.2", "Create and manage reference tables"),
        ]),
        ("5", "Insert and Format Graphic Elements", [
            ("5.1", "Insert illustrations and text boxes"),
            ("5.2", "Format illustrations and text boxes"),
            ("5.3", "Add text to graphic elements"),
            ("5.4", "Modify graphic elements"),
        ]),
        ("6", "Manage Document Collaboration", [
            ("6.1", "Add and manage comments"),
            ("6.2", "Manage change tracking"),
        ]),
    ],
    "MO-200": [
        ("1", "Manage Worksheets and Workbooks", [
            ("1.1", "Import data into workbooks"),
            ("1.2", "Navigate within workbooks"),
            ("1.3", "Format worksheets and workbooks"),
            ("1.4", "Customize options and views"),
            ("1.5", "Configure content for collaboration"),
        ]),
        ("2", "Manage Data Cells and Ranges", [
            ("2.1", "Manipulate data in worksheets"),
            ("2.2", "Format cells and ranges"),
            ("2.3", "Define and reference named ranges"),
            ("2.4", "Summarize data visually"),
        ]),
        ("3", "Manage Tables and Table Data", [
            ("3.1", "Create and format tables"),
            ("3.2", "Modify tables"),
            ("3.3", "Filter and sort table data"),
        ]),
        ("4", "Perform Operations by using Formulas and Functions", [
            ("4.1", "Insert references"),
            ("4.2", "Calculate and transform data"),
            ("4.3", "Format and modify text"),
        ]),
        ("5", "Manage Charts", [
            ("5.1", "Create charts"),
            ("5.2", "Modify charts"),
            ("5.3", "Format charts"),
        ]),
    ],
    "MO-300": [
        ("1", "Manage Presentations", [
            ("1.1", "Modify slide masters, layouts, and themes"),
            ("1.2", "Change presentation options and views"),
            ("1.3", "Configure print settings"),
            ("1.4", "Configure and present slideshows"),
            ("1.5", "Prepare presentations for collaboration"),
        ]),
        ("2", "Insert and Format Text, Shapes, and Images", [
            ("2.1", "Insert text and text boxes"),
            ("2.2", "Format text and shapes"),
            ("2.3", "Insert and format images"),
            ("2.4", "Order and group objects"),
        ]),
        ("3", "Insert Tables, Charts, SmartArt, 3D Models, and Media", [
            ("3.1", "Insert and format tables"),
            ("3.2", "Insert and modify charts"),
            ("3.3", "Insert and format SmartArt"),
            ("3.4", "Insert and modify 3D models and media"),
        ]),
        ("4", "Apply Transitions and Animations", [
            ("4.1", "Apply and configure slide transitions"),
            ("4.2", "Animate slide content"),
            ("4.3", "Set timing for transitions and animations"),
        ]),
        ("5", "Manage Multiple Presentations", [
            ("5.1", "Merge content from multiple presentations"),
            ("5.2", "Track changes and resolve issues"),
        ]),
    ],
}

CUT_SCORE = 700
SCALE_MAX = 1000
EXAM_MINUTES = 50
PROJECT_MIN, PROJECT_MAX = 5, 7
TASK_MIN, TASK_MAX = 25, 35


def _oid(subject: str, code: str) -> str:
    return f"{subject.lower()}-{code.replace('.', '-')}"


def _hash_rules(rules: dict | list | str) -> str:
    raw = rules if isinstance(rules, str) else json.dumps(rules, sort_keys=True, ensure_ascii=False)
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()[:16]


def scale_score(earned: float, possible: float) -> int:
    if possible <= 0:
        return 0
    return int(round(SCALE_MAX * float(earned) / float(possible)))


def passed(scaled: int) -> bool:
    return scaled >= CUT_SCORE


def seed_taxonomy() -> None:
    with cursor() as cur:
        for subject, _prog, _label in SUBJECTS:
            for i, (code, title, children) in enumerate(TAXONOMY[subject], start=1):
                parent = _oid(subject, code)
                cur.execute(
                    """
                    INSERT INTO objective_domains (id, subject, code, title, description, parent_id, sort_order)
                    VALUES (%s, %s, %s, %s, %s, NULL, %s)
                    ON CONFLICT (id) DO UPDATE SET title = EXCLUDED.title, sort_order = EXCLUDED.sort_order
                    """,
                    (parent, subject, code, title, title, i),
                )
                for j, (ccode, ctitle) in enumerate(children, start=1):
                    cur.execute(
                        """
                        INSERT INTO objective_domains (id, subject, code, title, description, parent_id, sort_order)
                        VALUES (%s, %s, %s, %s, %s, %s, %s)
                        ON CONFLICT (id) DO UPDATE SET
                          title = EXCLUDED.title, parent_id = EXCLUDED.parent_id, sort_order = EXCLUDED.sort_order
                        """,
                        (_oid(subject, ccode), subject, ccode, ctitle, ctitle, parent, j),
                    )


def sync_existing_projects() -> None:
    """Ánh xạ projects Word/Excel/PPT sẵn có thành Project Block + Atomic Tasks."""
    with cursor() as cur:
        cur.execute(
            """
            SELECT id, title, program, objective, skill_domain, filename, file_path, rubric, published
            FROM projects ORDER BY program, sort_order, id
            """
        )
        rows = list(cur.fetchall())
    for row in rows:
        subject = {"word": "MO-100", "excel": "MO-200", "powerpoint": "MO-300"}.get(row["program"], "MO-100")
        code = str(row.get("objective") or "").strip()
        if code.count(".") > 1:
            code = ".".join(code.split(".")[:2])
        obj_id = _oid(subject, code) if code else None
        if obj_id:
            with cursor() as cur:
                cur.execute("SELECT id FROM objective_domains WHERE id = %s", (obj_id,))
                if not cur.fetchone():
                    obj_id = None
        pid = f"bp-{row['id']}"
        status = "published" if row.get("published") is not False else "draft"
        rubric = row.get("rubric")
        if isinstance(rubric, str):
            try:
                rubric = json.loads(rubric)
            except json.JSONDecodeError:
                rubric = {}
        if not isinstance(rubric, dict):
            rubric = {}
        criteria = rubric.get("criteria") or []
        with cursor() as cur:
            cur.execute(
                """
                INSERT INTO bank_projects
                  (id, name, scenario_description, resource_file_url, program, source_project_id, status)
                VALUES (%s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT (id) DO UPDATE SET
                  name = EXCLUDED.name, resource_file_url = EXCLUDED.resource_file_url,
                  source_project_id = EXCLUDED.source_project_id, status = EXCLUDED.status
                """,
                (
                    pid,
                    row["title"],
                    row.get("skill_domain") or row["title"],
                    row.get("file_path") or "",
                    row["program"],
                    row["id"],
                    status,
                ),
            )
            if not criteria:
                tid = f"bt-{row['id']}-1"
                rules = {"valid_paths": [], "final_state": {}, "hint_tiers": [], "weight": 1}
                _upsert_task(cur, tid, obj_id, row["title"], rules, 1, status, row["id"], row.get("file_path") or "")
                cur.execute(
                    """
                    INSERT INTO bank_project_tasks (project_id, task_id, sequence_order)
                    VALUES (%s, %s, 1) ON CONFLICT DO NOTHING
                    """,
                    (pid, tid),
                )
                continue
            for idx, crit in enumerate(criteria, start=1):
                tid = f"bt-{row['id']}-{crit.get('id') or idx}"
                help_steps = crit.get("help_steps") or []
                rules = {
                    "criterion_id": crit.get("id"),
                    "kind": crit.get("kind"),
                    "valid_paths": _paths_from_criterion(crit),
                    "final_state": crit.get("predicate") or {},
                    "hint_tiers": help_steps[:3],
                    "weight": int(crit.get("weight") or 1),
                    "partial_credit_training": True,
                }
                weight = 1
                w = int(crit.get("weight") or 1)
                if w >= 8:
                    weight = 3
                elif w >= 4:
                    weight = 2
                _upsert_task(
                    cur,
                    tid,
                    obj_id,
                    crit.get("prompt") or row["title"],
                    rules,
                    weight,
                    status,
                    row["id"],
                    row.get("file_path") or "",
                )
                cur.execute(
                    """
                    INSERT INTO bank_project_tasks (project_id, task_id, sequence_order)
                    VALUES (%s, %s, %s) ON CONFLICT DO NOTHING
                    """,
                    (pid, tid, idx),
                )


def _paths_from_criterion(crit: dict) -> list[str]:
    selector = crit.get("selector") or {}
    action = str(selector.get("action") or crit.get("kind") or "ribbon")
    paths = [f"Ribbon_{action}", f"Shortcut_{action}", f"ContextMenu_{action}"]
    return paths


def _upsert_task(cur, tid, obj_id, instruction, rules, weight, status, source_project_id, resource):
    cur.execute(
        """
        INSERT INTO bank_tasks
          (id, objective_id, instruction_text, q_matrix_rules, default_weight, status,
           source_project_id, resource_file, version_hash)
        VALUES (%s, %s, %s, %s::jsonb, %s, %s, %s, %s, %s)
        ON CONFLICT (id) DO UPDATE SET
          objective_id = EXCLUDED.objective_id,
          instruction_text = EXCLUDED.instruction_text,
          q_matrix_rules = EXCLUDED.q_matrix_rules,
          default_weight = EXCLUDED.default_weight,
          version_hash = EXCLUDED.version_hash
        """,
        (
            tid,
            obj_id,
            instruction,
            json.dumps(rules, ensure_ascii=False),
            weight,
            status,
            source_project_id,
            resource,
            _hash_rules(rules),
        ),
    )


def seed_bank() -> None:
    seed_taxonomy()
    sync_existing_projects()
    _ensure_sample_exam()


def pack_exam_projects(program: str) -> list[str]:
    """Chọn 5–7 project sao cho tổng task nằm trong 25–35 (chuẩn Certiport)."""
    with cursor() as cur:
        cur.execute(
            """
            SELECT p.id, COUNT(pt.task_id) AS task_n
            FROM bank_projects p
            LEFT JOIN bank_project_tasks pt ON pt.project_id = p.id
            WHERE p.program = %s AND p.status = 'published'
            GROUP BY p.id
            HAVING COUNT(pt.task_id) BETWEEN 1 AND 8
            ORDER BY COUNT(pt.task_id) DESC, p.id
            """,
            (program,),
        )
        rows = [dict(r) for r in cur.fetchall()]
    if len(rows) < PROJECT_MIN:
        return []
    for k in (5, 6, 7):
        for combo in combinations(rows[:14], k):
            total = sum(int(r["task_n"]) for r in combo)
            if TASK_MIN <= total <= TASK_MAX:
                return [r["id"] for r in combo]
    chosen: list[str] = []
    tasks = 0
    for row in rows:
        n = int(row["task_n"])
        if len(chosen) >= PROJECT_MAX or tasks + n > TASK_MAX:
            continue
        chosen.append(row["id"])
        tasks += n
        if PROJECT_MIN <= len(chosen) <= PROJECT_MAX and TASK_MIN <= tasks <= TASK_MAX:
            return chosen
    return chosen if PROJECT_MIN <= len(chosen) <= PROJECT_MAX and TASK_MIN <= tasks <= TASK_MAX else []


def _ensure_sample_exam() -> None:
    eid = "exam-word-mock-1"
    with cursor() as cur:
        cur.execute("SELECT id FROM bank_exams WHERE id = %s", (eid,))
        exists = cur.fetchone()
    if exists and exam_counts(eid)["ok"]:
        return
    projects = pack_exam_projects("word")
    if len(projects) < PROJECT_MIN:
        return
    with cursor() as cur:
        cur.execute(
            """
            INSERT INTO bank_exams (id, title, exam_type, duration_minutes, program, status, version_hash)
            VALUES (%s, %s, 'CERTIFICATION_MOCK', %s, 'word', 'published', %s)
            ON CONFLICT (id) DO UPDATE SET status = 'published', duration_minutes = EXCLUDED.duration_minutes
            """,
            (eid, "Đề thi thử Word MO-100 số 1", EXAM_MINUTES, secrets.token_hex(8)),
        )
        cur.execute("DELETE FROM bank_exam_projects WHERE exam_id = %s", (eid,))
        for i, pid in enumerate(projects, start=1):
            cur.execute(
                """
                INSERT INTO bank_exam_projects (exam_id, project_id, project_order)
                VALUES (%s, %s, %s) ON CONFLICT DO NOTHING
                """,
                (eid, pid, i),
            )


def list_subjects() -> list[dict]:
    return [{"id": s, "program": p, "label": lab} for s, p, lab in SUBJECTS]


def knowledge_tree(subject: str | None = None) -> list[dict]:
    sql = """
        SELECT d.id, d.subject, d.code, d.title, d.parent_id, d.sort_order,
               COUNT(t.id) FILTER (WHERE t.status <> 'archived') AS task_n
        FROM objective_domains d
        LEFT JOIN bank_tasks t ON t.objective_id = d.id
    """
    params: list = []
    if subject:
        sql += " WHERE d.subject = %s"
        params.append(subject)
    sql += " GROUP BY d.id ORDER BY d.subject, d.sort_order, d.code"
    with cursor() as cur:
        cur.execute(sql, tuple(params))
        rows = [dict(r) for r in cur.fetchall()]
    by_parent: dict[str | None, list[dict]] = {}
    for row in rows:
        by_parent.setdefault(row["parent_id"], []).append(row)
    out = []
    for subject_id, _p, label in SUBJECTS:
        if subject and subject != subject_id:
            continue
        roots = [r for r in by_parent.get(None, []) if r["subject"] == subject_id]
        node = {"id": subject_id, "code": subject_id, "title": label, "task_n": 0, "children": []}
        for root in roots:
            kids = by_parent.get(root["id"], [])
            root["children"] = kids
            root["task_n"] = int(root["task_n"] or 0) + sum(int(k["task_n"] or 0) for k in kids)
            node["children"].append(root)
            node["task_n"] += root["task_n"]
        out.append(node)
    return out


def list_tasks(*, objective_id: str = "", orphans: bool = False, published_only: bool = False) -> list[dict]:
    sql = """
        SELECT t.*, d.code AS objective_code, d.title AS objective_title, d.subject,
               EXISTS (
                 SELECT 1 FROM bank_project_tasks pt WHERE pt.task_id = t.id
               ) AS in_project
        FROM bank_tasks t
        LEFT JOIN objective_domains d ON d.id = t.objective_id
        WHERE 1=1
    """
    params: list = []
    if objective_id:
        sql += " AND (t.objective_id = %s OR t.objective_id IN (SELECT id FROM objective_domains WHERE parent_id = %s))"
        params.extend([objective_id, objective_id])
    if orphans:
        sql += " AND (t.objective_id IS NULL OR NOT EXISTS (SELECT 1 FROM bank_project_tasks pt WHERE pt.task_id = t.id))"
    if published_only:
        sql += " AND t.status = 'published'"
    sql += " ORDER BY d.subject, d.code, t.id"
    with cursor() as cur:
        cur.execute(sql, tuple(params))
        return [dict(r) for r in cur.fetchall()]


def parse_final_state(raw: str) -> dict:
    """Final State OpenXML: JSON hoặc danh sách chữ cần có trong file."""
    text = (raw or "").strip()
    if not text:
        return {}
    if text.startswith("{"):
        try:
            data = json.loads(text)
            return data if isinstance(data, dict) else {"contains": text}
        except json.JSONDecodeError:
            return {"contains": text}
    parts = [p.strip() for p in text.replace(";", ",").split(",") if p.strip()]
    return {"contains_text": parts} if parts else {}


def save_task(
    *,
    task_id: str | None,
    objective_id: str,
    instruction: str,
    weight: int = 1,
    valid_paths: list[str] | None = None,
    locate: str = "",
    tool: str = "",
    configure: str = "",
    hint1: str = "",
    hint2: str = "",
    hint3: str = "",
    final_state: str | dict | None = None,
    user_id: int | None = None,
) -> str:
    tid = task_id or f"bt-{secrets.token_hex(6)}"
    weight = 1 if weight not in (1, 2, 3) else weight
    if isinstance(final_state, dict):
        final = final_state
    else:
        final = parse_final_state(str(final_state or ""))
    rules = {
        "locate": locate,
        "tool": tool,
        "configure": configure,
        "valid_paths": [p.strip() for p in (valid_paths or []) if p and p.strip()],
        "hint_tiers": [h for h in (hint1, hint2, hint3) if h],
        "final_state": final,
        "weight": weight,
        "partial_credit_training": True,
    }
    with cursor() as cur:
        cur.execute("SELECT status FROM bank_tasks WHERE id = %s", (tid,))
        row = cur.fetchone()
        if row and row["status"] == "published":
            raise ValueError("published")
        if row:
            cur.execute(
                """
                UPDATE bank_tasks SET
                  objective_id = %s, instruction_text = %s, q_matrix_rules = %s::jsonb,
                  default_weight = %s, version_hash = %s
                WHERE id = %s AND status = 'draft'
                """,
                (objective_id, instruction, json.dumps(rules, ensure_ascii=False), weight, _hash_rules(rules), tid),
            )
        else:
            cur.execute(
                """
                INSERT INTO bank_tasks
                  (id, objective_id, instruction_text, q_matrix_rules, default_weight,
                   status, created_by, version_hash)
                VALUES (%s, %s, %s, %s::jsonb, %s, 'draft', %s, %s)
                """,
                (tid, objective_id, instruction, json.dumps(rules, ensure_ascii=False), weight, user_id, _hash_rules(rules)),
            )
    return tid


def set_task_status(task_id: str, status: str) -> None:
    if status not in ("draft", "published", "archived"):
        raise ValueError("status")
    with cursor() as cur:
        cur.execute("UPDATE bank_tasks SET status = %s WHERE id = %s", (status, task_id))


def store_practice_file(task_id: str, filename: str, data: bytes) -> str:
    BANK_FILES.mkdir(parents=True, exist_ok=True)
    dest_dir = BANK_FILES / task_id
    dest_dir.mkdir(parents=True, exist_ok=True)
    safe = Path(filename).name or "starter.docx"
    dest = dest_dir / safe
    digest = hashlib.sha256(data).hexdigest()
    with cursor() as cur:
        cur.execute(
            "SELECT id FROM bank_tasks WHERE file_sha256 = %s AND id <> %s",
            (digest, task_id),
        )
        if cur.fetchone():
            raise ValueError("duplicate")
    dest.write_bytes(data)
    with cursor() as cur:
        cur.execute(
            "UPDATE bank_tasks SET resource_file = %s, file_sha256 = %s WHERE id = %s",
            (str(dest), digest, task_id),
        )
    return digest


def ingest_objective_file(objective_id: str, filename: str, data: bytes, user_id: int | None = None) -> str:
    """Kéo-thả file Study Guide vào nhánh Objective đang chọn."""
    digest = hashlib.sha256(data).hexdigest()
    with cursor() as cur:
        cur.execute("SELECT id FROM bank_tasks WHERE file_sha256 = %s", (digest,))
        hit = cur.fetchone()
        if hit:
            raise ValueError("duplicate")
    safe = Path(filename).name or "starter.docx"
    tid = save_task(
        task_id=None,
        objective_id=objective_id,
        instruction=f"Bài luyện tập {safe}",
        user_id=user_id,
    )
    store_practice_file(tid, safe, data)
    return tid


def list_projects(*, program: str = "", published_only: bool = False) -> list[dict]:
    sql = """
        SELECT p.*, COUNT(pt.task_id) AS task_n,
               BOOL_OR(COALESCE(t.high_difficulty, FALSE)) AS high_difficulty
        FROM bank_projects p
        LEFT JOIN bank_project_tasks pt ON pt.project_id = p.id
        LEFT JOIN bank_tasks t ON t.id = pt.task_id
        WHERE 1=1
    """
    params: list = []
    if program:
        sql += " AND p.program = %s"
        params.append(program)
    if published_only:
        sql += " AND p.status = 'published'"
    sql += " GROUP BY p.id ORDER BY p.program, p.name"
    with cursor() as cur:
        cur.execute(sql, tuple(params))
        return [dict(r) for r in cur.fetchall()]


def exam_counts(exam_id: str) -> dict:
    with cursor() as cur:
        cur.execute(
            """
            SELECT COUNT(DISTINCT ep.project_id) AS projects,
                   COUNT(pt.task_id) AS tasks
            FROM bank_exam_projects ep
            LEFT JOIN bank_project_tasks pt ON pt.project_id = ep.project_id
            WHERE ep.exam_id = %s
            """,
            (exam_id,),
        )
        row = cur.fetchone() or {"projects": 0, "tasks": 0}
    projects = int(row["projects"] or 0)
    tasks = int(row["tasks"] or 0)
    ok = PROJECT_MIN <= projects <= PROJECT_MAX and TASK_MIN <= tasks <= TASK_MAX
    return {"projects": projects, "tasks": tasks, "ok": ok}


def validate_exam(exam_id: str) -> dict:
    return exam_counts(exam_id)


def list_exams(*, published_only: bool = False, program: str = "") -> list[dict]:
    sql = "SELECT * FROM bank_exams WHERE 1=1"
    params: list = []
    if published_only:
        sql += " AND status = 'published' AND COALESCE(is_global, TRUE) = TRUE"
    if program:
        sql += " AND program = %s"
        params.append(program)
    sql += " ORDER BY created_at DESC"
    with cursor() as cur:
        cur.execute(sql, tuple(params))
        exams = [dict(r) for r in cur.fetchall()]
    for exam in exams:
        exam["counts"] = exam_counts(exam["id"])
    return exams


def get_exam(exam_id: str) -> dict | None:
    with cursor() as cur:
        cur.execute("SELECT * FROM bank_exams WHERE id = %s", (exam_id,))
        row = cur.fetchone()
        if not row:
            return None
        exam = dict(row)
        cur.execute(
            """
            SELECT p.*, ep.project_order, COUNT(pt.task_id) AS task_n
            FROM bank_exam_projects ep
            JOIN bank_projects p ON p.id = ep.project_id
            LEFT JOIN bank_project_tasks pt ON pt.project_id = p.id
            WHERE ep.exam_id = %s
            GROUP BY p.id, ep.project_order
            ORDER BY ep.project_order
            """,
            (exam_id,),
        )
        exam["projects"] = [dict(r) for r in cur.fetchall()]
    exam["counts"] = exam_counts(exam_id)
    return exam


def save_exam(
    *,
    exam_id: str | None,
    title: str,
    exam_type: str,
    program: str,
    project_ids: list[str],
    user_id: int | None = None,
) -> str:
    eid = exam_id or f"exam-{secrets.token_hex(5)}"
    exam_type = exam_type if exam_type in ("PRACTICE_EXAM", "CERTIFICATION_MOCK") else "CERTIFICATION_MOCK"
    program = program if program in ("word", "excel", "powerpoint") else "word"
    with cursor() as cur:
        cur.execute("SELECT status FROM bank_exams WHERE id = %s", (eid,))
        row = cur.fetchone()
        if row and row["status"] == "published":
            raise ValueError("published")
        if row:
            cur.execute(
                """
                UPDATE bank_exams SET title = %s, exam_type = %s, program = %s, duration_minutes = %s
                WHERE id = %s AND status = 'draft'
                """,
                (title, exam_type, program, EXAM_MINUTES, eid),
            )
            cur.execute("DELETE FROM bank_exam_projects WHERE exam_id = %s", (eid,))
        else:
            cur.execute(
                """
                INSERT INTO bank_exams
                  (id, title, exam_type, duration_minutes, program, status, created_by, version_hash)
                VALUES (%s, %s, %s, %s, %s, 'draft', %s, %s)
                """,
                (eid, title, exam_type, EXAM_MINUTES, program, user_id, secrets.token_hex(8)),
            )
        for i, pid in enumerate(project_ids, start=1):
            cur.execute(
                """
                INSERT INTO bank_exam_projects (exam_id, project_id, project_order)
                VALUES (%s, %s, %s) ON CONFLICT DO NOTHING
                """,
                (eid, pid, i),
            )
    return eid


def set_exam_status(exam_id: str, status: str) -> None:
    if status not in ("draft", "published", "archived"):
        raise ValueError("status")
    if status == "published" and not exam_counts(exam_id)["ok"]:
        raise ValueError("structure")
    with cursor() as cur:
        cur.execute("UPDATE bank_exams SET status = %s WHERE id = %s", (status, exam_id))


def auto_generate_exam(*, program: str, title: str, user_id: int | None = None) -> str:
    ids = pack_exam_projects(program)
    if len(ids) < PROJECT_MIN:
        raise ValueError("projects")
    return save_exam(
        exam_id=None,
        title=title,
        exam_type="CERTIFICATION_MOCK",
        program=program,
        project_ids=ids,
        user_id=user_id,
    )


@_engine
def compile_payload(exam_id: str, student_id: int | None = None) -> dict:
    exam = get_exam(exam_id)
    if not exam:
        raise ValueError("exam")
    mock = exam["exam_type"] == "CERTIFICATION_MOCK"
    projects = []
    with cursor() as cur:
        for block in exam["projects"]:
            cur.execute(
                """
                SELECT t.id, t.instruction_text, t.q_matrix_rules, t.default_weight, t.version_hash,
                       pt.sequence_order
                FROM bank_project_tasks pt
                JOIN bank_tasks t ON t.id = pt.task_id
                WHERE pt.project_id = %s
                ORDER BY pt.sequence_order
                """,
                (block["id"],),
            )
            tasks = []
            for t in cur.fetchall():
                rules = t["q_matrix_rules"] if isinstance(t["q_matrix_rules"], dict) else {}
                item = {
                    "task_id": t["id"],
                    "instruction": t["instruction_text"],
                    "weight": t["default_weight"],
                    "sequence": t["sequence_order"],
                    "version_hash": t["version_hash"],
                    "q_matrix": {
                        "locate": rules.get("locate"),
                        "tool": rules.get("tool"),
                        "configure": rules.get("configure"),
                        "valid_paths": rules.get("valid_paths") or [],
                        "final_state": rules.get("final_state") or {},
                    },
                }
                if not mock:
                    item["hint_tiers"] = rules.get("hint_tiers") or []
                tasks.append(item)
            projects.append(
                {
                    "project_id": block["id"],
                    "source_project_id": block.get("source_project_id"),
                    "title": block["name"],
                    "order": block["project_order"],
                    "file": block.get("resource_file_url") or "",
                    "tasks": tasks,
                }
            )
    payload = {
        "exam_id": exam["id"],
        "title": exam["title"],
        "exam_type": exam["exam_type"],
        "duration_minutes": exam["duration_minutes"] or EXAM_MINUTES,
        "score_scale": SCALE_MAX,
        "cut_score": CUT_SCORE,
        "partial_credit": not mock,
        "hints": not mock,
        "elapsed_only": not mock,
        "ui": "certiport_split" if mock else "practice",
        "focus_lock": mock,
        "force_submit": mock,
        "ip_whitelist": mock,
        "version_hash": exam.get("version_hash") or "",
        "navigation": {
            "next_task": True,
            "prev_task": True,
            "mark_for_review": mock,
            "restart_project": mock,
        },
        "projects": projects,
    }
    if student_id and mock:
        payload["projects"] = shuffle_projects(projects, f"{exam['id']}:{student_id}")
    return payload


@_engine
def session_for_project(project_id: str, mode: str, student_id: int | None = None) -> dict:
    """Cờ luyện tập / thi khi MOS-KulKul mở một project hiện có."""
    training = mode != "testing"
    with cursor() as cur:
        cur.execute(
            """
            SELECT e.id, e.exam_type, e.duration_minutes, e.status
            FROM assignments a
            JOIN bank_exams e ON e.id = a.exam_id
            WHERE a.project_id = %s
            ORDER BY a.created_at DESC LIMIT 1
            """,
            (project_id,),
        )
        exam = cur.fetchone()
    if exam:
        try:
            payload = compile_payload(exam["id"], student_id=student_id)
            payload["mode"] = "testing" if exam["exam_type"] == "CERTIFICATION_MOCK" else "training"
            return payload
        except ValueError:
            pass
    return {
        "mode": "training" if training else "testing",
        "hints": training,
        "elapsed_only": training,
        "partial_credit": training,
        "ui": "practice" if training else "certiport_split",
        "focus_lock": not training,
        "force_submit": not training,
        "duration_minutes": None if training else EXAM_MINUTES,
        "score_scale": SCALE_MAX,
        "cut_score": CUT_SCORE,
    }


def flag_issue(*, exam_id: str = "", task_id: str = "", reporter_id: int | None, detail: str) -> None:
    with cursor() as cur:
        cur.execute(
            """
            INSERT INTO exam_issue_flags (exam_id, task_id, reporter_id, detail)
            VALUES (%s, %s, %s, %s)
            """,
            (exam_id or None, task_id or None, reporter_id, detail.strip()),
        )


def list_flags(limit: int = 20) -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT f.*, u.name AS reporter, e.title AS exam_title
            FROM exam_issue_flags f
            LEFT JOIN users u ON u.id = f.reporter_id
            LEFT JOIN bank_exams e ON e.id = f.exam_id
            ORDER BY f.created_at DESC LIMIT %s
            """,
            (limit,),
        )
        return [dict(r) for r in cur.fetchall()]


def get_task(task_id: str) -> dict | None:
    with cursor() as cur:
        cur.execute(
            """
            SELECT t.*, d.code AS objective_code, d.title AS objective_title, d.subject
            FROM bank_tasks t
            LEFT JOIN objective_domains d ON d.id = t.objective_id
            WHERE t.id = %s
            """,
            (task_id,),
        )
        row = cur.fetchone()
    return dict(row) if row else None


def save_project(*, name: str, program: str, scenario: str = "", user_id: int | None = None) -> str:
    pid = f"bp-{secrets.token_hex(5)}"
    program = program if program in ("word", "excel", "powerpoint") else "word"
    with cursor() as cur:
        cur.execute(
            """
            INSERT INTO bank_projects (id, name, scenario_description, program, status, created_by)
            VALUES (%s, %s, %s, %s, 'draft', %s)
            """,
            (pid, name.strip(), scenario.strip(), program, user_id),
        )
    return pid


def attach_task(project_id: str, task_id: str) -> None:
    with cursor() as cur:
        cur.execute(
            "SELECT COALESCE(MAX(sequence_order), 0) + 1 AS n FROM bank_project_tasks WHERE project_id = %s",
            (project_id,),
        )
        seq = int((cur.fetchone() or {}).get("n") or 1)
        cur.execute(
            """
            INSERT INTO bank_project_tasks (project_id, task_id, sequence_order)
            VALUES (%s, %s, %s) ON CONFLICT DO NOTHING
            """,
            (project_id, task_id, seq),
        )


@_engine
def record_task_results(attempt_id: str, scored: dict | None) -> int:
    """Traceback Tầng 3 → Tầng 1: ghi nhật ký task, thi = zero partial."""
    items = (scored or {}).get("criteria") or (scored or {}).get("results") or []
    if not isinstance(items, list) or not items:
        return 0
    with cursor() as cur:
        cur.execute("SELECT user_id, project_id, mode FROM attempts WHERE id = %s", (attempt_id,))
        att = cur.fetchone()
        if not att:
            return 0
        cur.execute("DELETE FROM student_task_results WHERE attempt_id = %s", (attempt_id,))
        exam_mode = att["mode"] == "testing"
        written = 0
        for item in items:
            if not isinstance(item, dict):
                continue
            cid = str(item.get("criterion_id") or item.get("id") or "")
            if not cid:
                continue
            tid = f"bt-{att['project_id']}-{cid}"
            cur.execute("SELECT id, default_weight FROM bank_tasks WHERE id = %s", (tid,))
            task = cur.fetchone()
            if not task:
                continue
            earned = float(item.get("earned") or 0)
            possible = float(item.get("possible") or 0)
            if exam_mode and possible > 0 and earned < possible:
                earned = 0
            cur.execute(
                """
                INSERT INTO student_task_results
                  (student_id, task_id, attempt_id, is_correct, raw_earned, raw_possible, scaled_1000)
                VALUES (%s, %s, %s, %s, %s, %s, %s)
                """,
                (
                    att["user_id"],
                    tid,
                    attempt_id,
                    possible > 0 and earned >= possible,
                    earned,
                    possible,
                    scale_score(earned, possible),
                ),
            )
            written += 1
    return written


def parent_objectives(subject: str | None = None) -> list[dict]:
    sql = "SELECT id, subject, code, title FROM objective_domains WHERE parent_id IS NULL"
    params: list = []
    if subject:
        sql += " AND subject = %s"
        params.append(subject)
    sql += " ORDER BY subject, code"
    with cursor() as cur:
        cur.execute(sql, tuple(params))
        return [dict(r) for r in cur.fetchall()]


def leaf_objectives() -> list[dict]:
    with cursor() as cur:
        cur.execute(
            """
            SELECT id, subject, code, title
            FROM objective_domains
            WHERE parent_id IS NOT NULL
            ORDER BY subject, code
            """
        )
        return [dict(r) for r in cur.fetchall()]


def save_objective(*, subject: str, code: str, title: str, parent_id: str | None = None) -> str:
    """Super Admin cập nhật Tầng 1 khi Microsoft đổi syllabus."""
    subject = subject if subject in ("MO-100", "MO-200", "MO-300") else "MO-100"
    oid = _oid(subject, code.strip())
    with cursor() as cur:
        cur.execute(
            """
            INSERT INTO objective_domains (id, subject, code, title, description, parent_id, sort_order)
            VALUES (%s, %s, %s, %s, %s, %s, 99)
            ON CONFLICT (id) DO UPDATE SET title = EXCLUDED.title, parent_id = EXCLUDED.parent_id
            """,
            (oid, subject, code.strip(), title.strip(), title.strip(), parent_id or None),
        )
    return oid


def shuffle_projects(projects: list[dict], seed_key: str) -> list[dict]:
    items = [dict(p) for p in projects]
    rnd = random.Random(int(hashlib.sha256(seed_key.encode()).hexdigest()[:12], 16))
    rnd.shuffle(items)
    for i, block in enumerate(items, start=1):
        block["order"] = i
        tasks = list(block.get("tasks") or [])
        rnd.shuffle(tasks)
        for j, task in enumerate(tasks, start=1):
            task["sequence"] = j
        block["tasks"] = tasks
    return items


def assign_objective_drill(class_id: int, objective_id: str, assigned_by: int | None = None) -> list[str]:
    """Giao luyện tập theo Tầng 1: gom mọi Atomic Task cùng Objective."""
    from app.assign import configure_assignment

    tasks = list_tasks(objective_id=objective_id, published_only=True)
    sources: list[str] = []
    for task in tasks:
        src = task.get("source_project_id")
        if src and src not in sources:
            sources.append(src)
    if not sources:
        raise ValueError("tasks")
    for src in sources[:8]:
        configure_assignment(class_id, src, assigned_by=assigned_by, mode="training")
        with cursor() as cur:
            cur.execute(
                "UPDATE assignments SET objective_id = %s WHERE class_id = %s AND project_id = %s",
                (objective_id, class_id, src),
            )
    return sources


def apply_certiport_scale(scored: dict | None, mode: str) -> dict:
    data = dict(scored or {})
    items = data.get("criteria") or data.get("results") or []
    raw = 0.0
    possible = 0.0
    exam = mode == "testing"
    if isinstance(items, list):
        for item in items:
            if not isinstance(item, dict):
                continue
            weight = int(item.get("weight") or 1)
            if weight not in (1, 2, 3):
                weight = 1
            earned = float(item.get("earned") or 0)
            poss = float(item.get("possible") or 0) or 1.0
            frac = earned / poss
            if exam and frac < 1:
                frac = 0.0
                item["earned"] = 0
            raw += weight * frac
            possible += weight
    scaled = scale_score(raw, possible)
    data["raw_earned"] = raw
    data["raw_possible"] = possible
    data["scaled_1000"] = scaled
    data["passed"] = passed(scaled)
    data["cut_score"] = CUT_SCORE
    return data


def telemetry_hits_path(events: list, valid_paths: list[str]) -> bool:
    blob = " ".join(
        f"{ev.get('skill', '')} {ev.get('action', '')} {ev.get('detail', '')}".lower()
        for ev in events
        if isinstance(ev, dict)
    )
    for path in valid_paths:
        token = str(path).lower().replace("_", " ")
        tail = str(path).split("_")[-1].lower()
        if token in blob or (tail and tail in blob):
            return True
    return False


@_engine
def record_formative(*, student_id: int, attempt_id: str, event: str, task_id: str = "") -> None:
    if event not in ("hint1", "hint2", "hint3", "wrong_check", "check_pass"):
        return
    with cursor() as cur:
        cur.execute(
            """
            INSERT INTO formative_telemetry (student_id, task_id, attempt_id, event)
            VALUES (%s, %s, %s, %s)
            """,
            (student_id, task_id or None, attempt_id, event),
        )
    if event == "hint3":
        refresh_empirical_difficulty()


@_engine
def ingest_formative_events(student_id: int, attempt_id: str, events: list) -> int:
    n = 0
    for ev in events:
        if not isinstance(ev, dict):
            continue
        action = str(ev.get("action") or ev.get("skill") or "").lower()
        detail = ev.get("detail") if isinstance(ev.get("detail"), dict) else {}
        tier = detail.get("tier") or detail.get("hint_tier") or ev.get("tier")
        event = ""
        if tier in (1, "1", "hint1") or action in {"hint1", "hint_1"}:
            event = "hint1"
        elif tier in (2, "2", "hint2") or action in {"hint2", "hint_2"}:
            event = "hint2"
        elif tier in (3, "3", "hint3") or action in {"hint3", "hint_3", "hint-level-3"}:
            event = "hint3"
        elif action in {"hint"}:
            event = "hint1"
        elif action in {"wrong", "check_fail", "fail"}:
            event = "wrong_check"
        elif action in {"check_pass", "pass"}:
            event = "check_pass"
        if event:
            record_formative(
                student_id=student_id,
                attempt_id=attempt_id,
                event=event,
                task_id=str(ev.get("task_id") or detail.get("task_id") or ""),
            )
            n += 1
    return n


@_engine
def refresh_empirical_difficulty() -> int:
    """80% học sinh khối 10 dùng hint cấp 3 → High_Difficulty."""
    with cursor() as cur:
        cur.execute(
            """
            WITH grade10 AS (
              SELECT DISTINCT u.id
              FROM users u
              JOIN enrollments e ON e.user_id = u.id
              JOIN classes c ON c.id = e.class_id
              WHERE c.name ~ '^10'
            ),
            stats AS (
              SELECT f.task_id,
                     COUNT(DISTINCT f.student_id) FILTER (WHERE f.student_id IN (SELECT id FROM grade10)) AS n,
                     COUNT(DISTINCT f.student_id) FILTER (
                       WHERE f.event = 'hint3' AND f.student_id IN (SELECT id FROM grade10)
                     ) AS hint3
              FROM formative_telemetry f
              WHERE f.task_id IS NOT NULL
              GROUP BY f.task_id
            )
            UPDATE bank_tasks t
            SET high_difficulty = (s.n >= 5 AND (100.0 * s.hint3 / s.n) >= 80)
            FROM stats s
            WHERE t.id = s.task_id
            """
        )
        return cur.rowcount or 0


@_engine
def hard_stop_next(student_id: int, task_id: str) -> dict | None:
    """Sai 3 lần liên tiếp → không sang task khác objective; đưa bài cùng Objective."""
    if not task_id:
        return None
    with cursor() as cur:
        cur.execute(
            """
            SELECT is_correct FROM student_task_results
            WHERE student_id = %s AND task_id = %s
            ORDER BY created_at DESC LIMIT 3
            """,
            (student_id, task_id),
        )
        rows = list(cur.fetchall())
        if len(rows) < 3 or any(r["is_correct"] for r in rows):
            return None
        cur.execute("SELECT objective_id, instruction_text FROM bank_tasks WHERE id = %s", (task_id,))
        src = cur.fetchone()
        if not src or not src["objective_id"]:
            return None
        cur.execute(
            """
            SELECT id, instruction_text, source_project_id
            FROM bank_tasks
            WHERE objective_id = %s AND status = 'published' AND id <> %s
            ORDER BY random() LIMIT 1
            """,
            (src["objective_id"], task_id),
        )
        nxt = cur.fetchone()
    if not nxt:
        return None
    return {
        "hard_stop": True,
        "failed_task_id": task_id,
        "next_task_id": nxt["id"],
        "next_project_id": nxt.get("source_project_id"),
        "reason": f"Sai 3 lần liên tiếp «{src['instruction_text']}». Làm bài cùng Objective trước khi sang kỹ năng khác.",
    }


@_engine
def unlock_remedial(
    student_id: int, attempt_id: str, scaled: int | None = None, *, assign: bool = True
) -> dict:
    """Traceback Tầng 3→1: sai ≥3 task cùng Objective thì mở khóa luyện tập bổ trợ."""
    with cursor() as cur:
        cur.execute(
            """
            SELECT t.objective_id, d.code, d.title, d.subject, COUNT(*) AS fails
            FROM student_task_results r
            JOIN bank_tasks t ON t.id = r.task_id
            JOIN objective_domains d ON d.id = t.objective_id
            WHERE r.attempt_id = %s AND r.is_correct = FALSE
            GROUP BY t.objective_id, d.code, d.title, d.subject
            HAVING COUNT(*) >= 3
            ORDER BY COUNT(*) DESC
            """,
            (attempt_id,),
        )
        weak = [dict(r) for r in cur.fetchall()]
        cur.execute("SELECT class_id FROM enrollments WHERE user_id = %s LIMIT 1", (student_id,))
        enr = cur.fetchone()
        if scaled is None:
            cur.execute(
                """
                SELECT scaled_1000 FROM student_task_results
                WHERE attempt_id = %s AND scaled_1000 IS NOT NULL
                ORDER BY id DESC LIMIT 1
                """,
                (attempt_id,),
            )
            scaled_row = cur.fetchone()
            if scaled_row and scaled_row.get("scaled_1000") is not None:
                scaled = int(scaled_row["scaled_1000"])
    unlocked: list[str] = []
    if weak and enr and assign:
        try:
            unlocked = assign_objective_drill(int(enr["class_id"]), weak[0]["objective_id"])
        except ValueError:
            unlocked = []
    if weak:
        w = weak[0]
        score_bit = f"Bạn đạt {scaled}/1000. " if scaled is not None else ""
        message = (
            f"{score_bit}Tuy nhiên, dữ liệu cho thấy bạn sai liên tiếp {w['fails']} câu thuộc "
            f"Objective {w['code']}: {w['title']}. Hệ thống đã tự động mở khóa bộ bài tập luyện tập "
            f"bổ trợ cho phần này trong danh sách Việc cần làm."
        )
    elif scaled is not None:
        badge = "PASS" if passed(scaled) else "FAIL"
        message = f"Bạn đạt {scaled}/1000 — {badge}."
    else:
        message = ""
    return {"udl_message": message, "unlocked_projects": unlocked, "weak_objectives": weak, "scaled_1000": scaled}


def objective_gaps(class_id: int = 0) -> list[dict]:
    """Lỗ hổng theo Tầng 1: Student_Task_Results → Tasks → Objective_Domains."""
    with cursor() as cur:
        cur.execute(
            """
            SELECT
              COALESCE(c.name, 'Chưa xếp lớp') AS class_name,
              a.class_id,
              d.id AS exam_id,
              (d.subject || ' ' || d.code || ' — ' || d.title) AS title,
              (d.subject || ' ' || d.code) AS skill,
              COUNT(*) AS n,
              COUNT(*) FILTER (WHERE r.is_correct = FALSE) AS fails,
              0 AS locate_fail,
              0 AS tool_fail,
              0 AS configure_fail
            FROM student_task_results r
            JOIN bank_tasks t ON t.id = r.task_id
            JOIN objective_domains d ON d.id = t.objective_id
            JOIN attempts a ON a.id = r.attempt_id
            LEFT JOIN classes c ON c.id = a.class_id
            WHERE a.status IN ('submitted', 'graded')
              AND (%s = 0 OR a.class_id = %s)
            GROUP BY c.name, a.class_id, d.id, d.subject, d.code, d.title
            HAVING COUNT(*) > 0
            ORDER BY
              (100.0 * COUNT(*) FILTER (WHERE r.is_correct = FALSE) / COUNT(*)) DESC,
              d.code
            """,
            (class_id, class_id),
        )
        rows = [dict(r) for r in cur.fetchall()]
    out = []
    for row in rows:
        n = int(row["n"] or 0)
        fails = int(row["fails"] or 0)
        fail_pct = round(100.0 * fails / n, 1) if n else 0.0
        heat = "red" if fail_pct >= 75 else "amber" if fail_pct >= 50 else "green"
        row.update(
            {
                "fail_pct": fail_pct,
                "heat": heat,
                "action": (
                    f"{fail_pct:.0f}% sai Objective {row['skill']} — giao luyện tập Tầng 1 cho chuyên đề này."
                    if heat in {"red", "amber"}
                    else ""
                ),
            }
        )
        out.append(row)
    return out


@_engine
def exam_hash_ok(exam_id: str, client_hash: str) -> bool:
    if not client_hash:
        return True
    with cursor() as cur:
        cur.execute("SELECT version_hash FROM bank_exams WHERE id = %s", (exam_id,))
        row = cur.fetchone()
    if not row:
        return True
    stored = row.get("version_hash") or ""
    return not stored or stored == client_hash


def _telemetry_events(attempt_id: str) -> list[dict]:
    with cursor() as cur:
        cur.execute(
            "SELECT skill, action, detail FROM telemetry WHERE attempt_id = %s",
            (attempt_id,),
        )
        rows = [dict(r) for r in cur.fetchall()]
    out = []
    for row in rows:
        detail = row.get("detail")
        if isinstance(detail, str):
            try:
                detail = json.loads(detail)
            except Exception:
                pass
        out.append({"skill": row.get("skill") or "", "action": row.get("action") or "", "detail": detail})
    return out


def is_destructive(original: Path | None, submitted: Path | None) -> bool:
    """Xóa nội dung gốc của đề → cờ Destructive_Action."""
    if not original or not submitted or not Path(original).is_file() or not Path(submitted).is_file():
        return False
    from app.scoring import extract_text

    orig = extract_text(Path(original))
    sub = extract_text(Path(submitted))
    if len(orig) < 80:
        return False
    o_tokens = set(orig.lower().split())
    s_tokens = set(sub.lower().split())
    if not o_tokens:
        return False
    keep = len(o_tokens & s_tokens) / len(o_tokens)
    return keep < 0.55 and len(sub) < 0.7 * len(orig)


def final_state_matches(path: Path | None, final: dict) -> bool:
    if not path or not Path(path).is_file() or not isinstance(final, dict) or not final:
        return False
    from app.scoring import extract_text

    blob = extract_text(Path(path)).lower()
    needles: list[str] = []
    for key in ("contains", "contains_text", "theme", "xml", "value"):
        val = final.get(key)
        if isinstance(val, str) and val.strip():
            needles.append(val.lower())
        elif isinstance(val, list):
            needles.extend(str(x).lower() for x in val if str(x).strip())
    if not needles:
        needles = [str(v).lower() for v in final.values() if isinstance(v, str) and len(str(v)) > 2]
    return bool(needles) and all(n in blob for n in needles[:4])


@_engine
def apply_q_matrix_engine(attempt_id: str, scored: dict | None, submitted_path: Path | None = None) -> dict:
    """Valid_Paths (telemetry) + Final State (OpenXML) + destructive penalty."""
    data = dict(scored or {})
    items = data.get("criteria") or data.get("results") or []
    if not isinstance(items, list) or not items:
        return data
    with cursor() as cur:
        cur.execute("SELECT id, project_id, mode FROM attempts WHERE id = %s", (attempt_id,))
        att = cur.fetchone()
        orig = None
        if att:
            cur.execute("SELECT file_path FROM projects WHERE id = %s", (att["project_id"],))
            prow = cur.fetchone()
            orig = Path(prow["file_path"]) if prow and prow.get("file_path") else None
    if not att:
        return data
    events = _telemetry_events(attempt_id)
    destructive = is_destructive(orig, Path(submitted_path) if submitted_path else None)
    data["destructive_action"] = destructive
    submit = Path(submitted_path) if submitted_path else None
    for item in items:
        if not isinstance(item, dict):
            continue
        cid = str(item.get("criterion_id") or item.get("id") or "")
        if not cid:
            continue
        task = get_task(f"bt-{att['project_id']}-{cid}") or get_task(cid)
        rules = (task or {}).get("q_matrix_rules") if task else {}
        if not isinstance(rules, dict):
            rules = {}
        paths = rules.get("valid_paths") or []
        possible = float(item.get("possible") or 0)
        if item.get("status") != "pass" and paths and telemetry_hits_path(events, paths):
            item["status"] = "pass"
            item["earned"] = possible or float(item.get("earned") or 0)
            item["reason_code"] = "valid_path"
        final = rules.get("final_state") or {}
        if item.get("status") != "pass" and final_state_matches(submit, final if isinstance(final, dict) else {}):
            item["status"] = "pass"
            item["earned"] = possible or float(item.get("earned") or 0)
            item["reason_code"] = "final_state"
    if destructive:
        factor = 0.5 if att["mode"] == "testing" else 0.8
        data["destructive_penalty"] = round(1 - factor, 2)
        for item in items:
            if isinstance(item, dict):
                item["earned"] = round(float(item.get("earned") or 0) * factor, 2)
                item["destructive"] = True
    return data


@_engine
def attempt_clock(attempt: dict) -> dict:
    created = attempt.get("created_at") or attempt.get("started_at")
    if isinstance(created, str):
        try:
            created = datetime.fromisoformat(created.replace("Z", "+00:00"))
        except ValueError:
            created = None
    if isinstance(created, datetime) and created.tzinfo is None:
        created = created.replace(tzinfo=timezone.utc)
    now = datetime.now(timezone.utc)
    elapsed = int((now - created).total_seconds()) if isinstance(created, datetime) else 0
    bank = session_for_project(str(attempt.get("project_id") or ""), attempt.get("mode") or "training")
    duration = None
    if not bank.get("elapsed_only"):
        duration = int(bank.get("duration_minutes") or EXAM_MINUTES) * 60
        cfg_limit = attempt.get("time_limit_sec")
        if cfg_limit:
            duration = int(cfg_limit)
    remaining = None if duration is None else max(0, duration - max(0, elapsed))
    return {
        "started_at": created.isoformat() if isinstance(created, datetime) else None,
        "elapsed_sec": max(0, elapsed),
        "duration_sec": duration,
        "remaining_sec": remaining,
        "elapsed_only": bool(bank.get("elapsed_only")),
        "force_submit": bool(bank.get("force_submit")) and remaining == 0,
        "focus_lock": bool(bank.get("focus_lock")),
        "cut_score": CUT_SCORE,
    }


@_engine
def student_certiport_card(student_id: int) -> dict:
    with cursor() as cur:
        cur.execute(
            """
            SELECT attempt_id, scaled_1000
            FROM student_task_results
            WHERE student_id = %s AND scaled_1000 IS NOT NULL
            ORDER BY created_at DESC LIMIT 1
            """,
            (student_id,),
        )
        row = cur.fetchone()
    if not row:
        return {}
    scaled = int(row["scaled_1000"])
    udl = unlock_remedial(student_id, row["attempt_id"], scaled=scaled, assign=False)
    return {
        "scaled_1000": scaled,
        "passed": passed(scaled),
        "badge": "PASS" if passed(scaled) else "FAIL",
        "udl_message": udl.get("udl_message") or "",
        "cut_score": CUT_SCORE,
    }
