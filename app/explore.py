"""Không gian Khám phá — đưa help_steps của rubric lên web, ngoài chế độ thi.

Đây là chỗ để HỌC, không phải để đo: trang không hiện điểm số. Nguồn nội dung
là các rubric trong app/rubrics (song ngữ dần), nên không cần bảng dữ liệu mới.

    catalog("word")                 → 6 nhóm objective MO-100, mỗi nhóm các node
    lesson("word-objective-1-2")    → đề bài + help_steps từng tiêu chí
    mastery(user_id, "word")        → project_id → new | learning | mastered
"""
from __future__ import annotations

import json
import re
from functools import lru_cache
from pathlib import Path

from app import i18n
from app.ceiling import ceiling

ROOT = Path(__file__).resolve().parent.parent
RUBRIC_DIR = ROOT / "app" / "rubrics"
MEDIA_DIR = ROOT / "app" / "static" / "hoc"

PROGRAMS = ("word", "excel", "powerpoint")
PROGRAM_NAMES = {
    "word": {"vi": "Word (MO-100)", "en": "Word (MO-100)"},
    "excel": {"vi": "Excel (MO-200)", "en": "Excel (MO-200)"},
    "powerpoint": {"vi": "PowerPoint (MO-300)", "en": "PowerPoint (MO-300)"},
}

# Tên nhóm objective theo Skills Measured của Certiport.
GROUPS: dict[str, dict[str, dict[str, str]]] = {
    "word": {
        "1": {"vi": "Quản lý tài liệu", "en": "Manage documents"},
        "2": {"vi": "Chèn và định dạng văn bản, đoạn, phân đoạn", "en": "Insert and format text, paragraphs, and sections"},
        "3": {"vi": "Quản lý bảng và danh sách", "en": "Manage tables and lists"},
        "4": {"vi": "Tạo và quản lý tham chiếu", "en": "Create and manage references"},
        "5": {"vi": "Chèn và định dạng đối tượng đồ họa", "en": "Insert and format graphic elements"},
        "6": {"vi": "Quản lý cộng tác trên tài liệu", "en": "Manage document collaboration"},
    },
    "excel": {
        "1": {"vi": "Quản lý worksheet và workbook", "en": "Manage worksheets and workbooks"},
        "2": {"vi": "Quản lý ô và vùng dữ liệu", "en": "Manage data cells and ranges"},
        "3": {"vi": "Quản lý bảng và dữ liệu bảng", "en": "Manage tables and table data"},
        "4": {"vi": "Tính toán bằng công thức và hàm", "en": "Perform operations by using formulas and functions"},
        "5": {"vi": "Quản lý biểu đồ", "en": "Manage charts"},
    },
    "powerpoint": {
        "1": {"vi": "Quản lý bài trình bày", "en": "Manage presentations"},
        "2": {"vi": "Quản lý slide", "en": "Manage slides"},
        "3": {"vi": "Chèn và định dạng chữ, hình, ảnh", "en": "Insert and format text, shapes, and images"},
        "4": {"vi": "Chèn bảng, biểu đồ, SmartArt, 3D và media", "en": "Insert tables, charts, SmartArt, 3D models, and media"},
        "5": {"vi": "Áp dụng hiệu ứng chuyển và hoạt hình", "en": "Apply transitions and animations"},
    },
}

LEVELS = ("new", "learning", "mastered")
LEVEL_LABELS = {
    "new": {"vi": "Chưa khám phá", "en": "Not explored"},
    "learning": {"vi": "Đang học", "en": "Learning"},
    "mastered": {"vi": "Đã làm chủ", "en": "Mastered"},
}
MASTERY_SCORE = 70      # giữ lại: ngưỡng khi không tra được trần điểm
MASTERY_RATIO = 0.7     # đạt 70% trần điểm THẬT thì coi là làm chủ

_SLUG = re.compile(r"^(word|excel|powerpoint)-objective-[0-9a-z-]+$")


def valid_slug(project_id: str) -> bool:
    return bool(_SLUG.match(project_id or ""))


def _load(path: Path) -> dict | None:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    return data if isinstance(data, dict) else None


def _objective_code(rubric: dict, path: Path) -> str:
    code = str(rubric.get("objective") or "").strip()
    if code:
        return code
    tail = path.stem.split("-objective-", 1)[-1]
    return tail.replace("-", ".")


def _sort_key(code: str) -> tuple:
    parts = re.split(r"[.\-]", code)
    key: list = []
    for part in parts:
        m = re.match(r"(\d+)([a-z]*)", part)
        if m:
            key.append((int(m.group(1)), m.group(2)))
        else:
            key.append((0, part))
    return tuple(key)


def _short_title(title: str) -> str:
    # "Word Objective 1.2 — Định dạng tài liệu" → "Định dạng tài liệu"
    for sep in (" — ", " – ", " - "):
        if sep in title:
            return title.split(sep, 1)[1].strip()
    return title


def catalog(program: str) -> list[dict]:
    """Nhóm objective → node. Node = một rubric (project_id)."""
    program = program if program in PROGRAMS else "word"
    nodes: list[dict] = []
    for path in sorted(RUBRIC_DIR.glob(f"{program}-objective-*.json")):
        rubric = _load(path)
        if not rubric:
            continue
        code = _objective_code(rubric, path)
        title = rubric.get("title") or path.stem
        nodes.append(
            {
                "project_id": str(rubric.get("project_id") or path.stem),
                "objective": code,
                "group": code.split(".", 1)[0],
                "title": title,
                "short": {lang: _short_title(i18n.pick(title, lang)) for lang in i18n.LANGS},
                "criteria": len(rubric.get("criteria") or []),
                "ceiling": ceiling(rubric),
                "en": i18n.coverage(rubric, "en"),
            }
        )
    nodes.sort(key=lambda n: _sort_key(n["objective"]))
    groups: list[dict] = []
    names = GROUPS.get(program, {})
    for node in nodes:
        gid = node["group"]
        if not groups or groups[-1]["id"] != gid:
            groups.append(
                {
                    "id": gid,
                    "title": names.get(gid) or {"vi": f"Nhóm {gid}", "en": f"Group {gid}"},
                    "nodes": [],
                }
            )
        groups[-1]["nodes"].append(node)
    return groups


def _media_for(project_id: str, criterion_id: str) -> str | None:
    folder = MEDIA_DIR / project_id
    for ext in (".gif", ".webp", ".png", ".mp4"):
        candidate = folder / f"{criterion_id}{ext}"
        if candidate.is_file():
            return f"/static/hoc/{project_id}/{candidate.name}"
    return None


def lesson(project_id: str) -> dict | None:
    """Rubric ở dạng THÔ (song ngữ) để template chọn ngôn ngữ bằng | t(lang)."""
    if not valid_slug(project_id):
        return None
    path = RUBRIC_DIR / f"{project_id}.json"
    rubric = _load(path)
    if not rubric:
        return None
    steps = []
    for crit in rubric.get("criteria") or []:
        if not isinstance(crit, dict):
            continue
        cid = str(crit.get("id") or "")
        steps.append(
            {
                "id": cid,
                "kind": crit.get("kind") or "artifact",
                "weight": int(crit.get("weight") or 0),
                "kc": [str(k) for k in (crit.get("kc") or [])],
                "prompt": crit.get("prompt") or "",
                "help_steps": crit.get("help_steps") or [],
                "media": _media_for(project_id, cid),
            }
        )
    program = str(rubric.get("program") or project_id.split("-", 1)[0])
    return {
        "project_id": project_id,
        "program": program,
        "program_name": PROGRAM_NAMES.get(program, {"vi": program, "en": program}),
        "objective": _objective_code(rubric, path),
        "title": rubric.get("title") or project_id,
        "source_file": rubric.get("source_file") or "",
        "steps": steps,
        "ceiling": ceiling(rubric),
        "en": i18n.coverage(rubric, "en"),
    }


@lru_cache(maxsize=None)
def auto_ceiling(project_id: str) -> int:
    """Trần điểm chấm tự động. 100 nếu không tra được rubric; 0 là giá trị THẬT.

    Cache không hết hạn: rubric tĩnh theo mỗi lần triển khai. Test nào sửa
    rubric phải gọi auto_ceiling.cache_clear().
    """
    if not valid_slug(project_id):
        return 100
    rubric = _load(RUBRIC_DIR / f"{project_id}.json")
    if not rubric:
        return 100
    return int(ceiling(rubric).get("auto") or 0)


def level_for(status: str | None, best: float | None, project_id: str | None = None) -> str:
    """Ngưỡng "đã làm chủ" tính theo trần điểm THẬT của rubric: bài 1-3 chỉ chấm
    tự động được 55/100, nên làm đúng hết phần máy chấm phải là mastered chứ
    không kẹt ở 'đang học' vì ngưỡng cứng 70."""
    tran = auto_ceiling(project_id) if project_id else 100
    if tran <= 0:
        # Không một điểm nào máy chấm được: "không đo được" khác "chưa đạt",
        # chỉ giáo viên mới xác nhận làm chủ.
        if status == "mastered":
            return "mastered"
        return "learning" if status in ("in_progress", "submitted") else "new"
    if status == "mastered" or (best or 0) >= MASTERY_RATIO * tran:
        return "mastered"
    if status in ("in_progress", "submitted") or (best or 0) > 0:
        return "learning"
    return "new"


def mastery(user_id: int | None, program: str | None = None) -> dict[str, dict]:
    """project_id → {'level', 'best'}; trống nếu chưa đăng nhập hoặc chưa có DB."""
    if not user_id:
        return {}
    try:
        from app.progress import list_student_exercises

        rows = list_student_exercises(int(user_id), program)
    except Exception:
        return {}
    out: dict[str, dict] = {}
    for row in rows:
        pid = str(row.get("project_id") or "")
        if not pid:
            continue
        best = float(row.get("best_verified") or 0)
        out[pid] = {"level": level_for(row.get("status"), best, pid), "best": best}
    return out


def markdown_bold(text: str) -> str:
    """**Design** → <strong>Design</strong>; phần còn lại đã được Jinja escape."""
    return re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", text)


def bold(text) -> "Markup":
    """Bộ lọc Jinja: escape rồi đổi **x** thành <strong>x</strong>."""
    from markupsafe import Markup, escape

    return Markup(markdown_bold(str(escape(text or ""))))
