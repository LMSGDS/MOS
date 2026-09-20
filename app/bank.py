"""Ngân hàng đề 3 tầng: Objective Domains → Project Blocks → Atomic Tasks → Exams.

Không MinIO / Redis: file nằm data/projects, payload JSON xuống MOS-KulKul.
"""
from __future__ import annotations

import hashlib
import json
import secrets
from itertools import combinations
from pathlib import Path

from app.db import cursor

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
        code = str(row.get("objective") or "").strip() or "1.1"
        if code.count(".") > 1:
            code = ".".join(code.split(".")[:2])
        obj_id = _oid(subject, code)
        with cursor() as cur:
            cur.execute("SELECT id FROM objective_domains WHERE id = %s", (obj_id,))
            if not cur.fetchone():
                obj_id = _oid(subject, "1.1")
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
    with cursor() as cur:
        cur.execute(
            """
            SELECT d.id, d.subject, d.code, d.title, d.parent_id, d.sort_order,
                   COUNT(t.id) FILTER (WHERE t.status <> 'archived') AS task_n
            FROM objective_domains d
            LEFT JOIN bank_tasks t ON t.objective_id = d.id
            WHERE (%s IS NULL OR d.subject = %s)
            GROUP BY d.id
            ORDER BY d.subject, d.sort_order, d.code
            """,
            (subject, subject),
        )
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
    user_id: int | None = None,
) -> str:
    tid = task_id or f"bt-{secrets.token_hex(6)}"
    weight = 1 if weight not in (1, 2, 3) else weight
    rules = {
        "locate": locate,
        "tool": tool,
        "configure": configure,
        "valid_paths": [p.strip() for p in (valid_paths or []) if p and p.strip()],
        "hint_tiers": [h for h in (hint1, hint2, hint3) if h],
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
    dest.write_bytes(data)
    digest = hashlib.sha256(data).hexdigest()
    with cursor() as cur:
        cur.execute(
            "UPDATE bank_tasks SET resource_file = %s, file_sha256 = %s WHERE id = %s",
            (str(dest), digest, task_id),
        )
    return digest


def list_projects(*, program: str = "", published_only: bool = False) -> list[dict]:
    sql = """
        SELECT p.*, COUNT(pt.task_id) AS task_n
        FROM bank_projects p
        LEFT JOIN bank_project_tasks pt ON pt.project_id = p.id
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
        sql += " AND status = 'published'"
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


def compile_payload(exam_id: str) -> dict:
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
    return {
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
        "navigation": {
            "next_task": True,
            "prev_task": True,
            "mark_for_review": mock,
            "restart_project": mock,
        },
        "projects": projects,
    }


def session_for_project(project_id: str, mode: str) -> dict:
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
            payload = compile_payload(exam["id"])
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
