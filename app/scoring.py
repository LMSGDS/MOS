"""Chấm bài MOS-KulKul: rubric tiêu chí; không cho điểm kỹ năng chỉ vì đủ chữ."""
from __future__ import annotations

import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

from app.grade import grade_path, load_rubric


def _xml_text(xml_bytes: bytes) -> str:
    try:
        root = ET.fromstring(xml_bytes)
    except ET.ParseError:
        return ""
    return " ".join((t or "").strip() for t in root.itertext() if (t or "").strip())


def extract_text(path: Path) -> str:
    if not path.is_file():
        return ""
    names = []
    suffix = path.suffix.lower()
    if suffix == ".docx":
        names = ["word/document.xml"]
    elif suffix == ".xlsx":
        names = ["xl/sharedStrings.xml", "xl/workbook.xml"]
    elif suffix == ".pptx":
        names = []
    chunks: list[str] = []
    try:
        with zipfile.ZipFile(path) as z:
            targets = names or [n for n in z.namelist() if n.startswith("ppt/slides/slide") and n.endswith(".xml")]
            for name in targets:
                if name in z.namelist():
                    chunks.append(_xml_text(z.read(name)))
    except zipfile.BadZipFile:
        return ""
    return "\n".join(chunks)


def score_file(
    path: Path | None,
    rubric: dict | None = None,
    evidence: list | None = None,
    lang: str | None = None,
) -> dict:
    rubric = load_rubric(rubric)
    if rubric.get("criteria"):
        return grade_path(path, rubric, evidence, lang)

    if path is None:
        path = Path("")
    path = Path(path)
    text = extract_text(path)
    checks = []
    points = 0
    total = 0

    def add(ok: bool, label: str, weight: int = 1) -> None:
        nonlocal points, total
        total += weight
        if ok:
            points += weight
        checks.append({"ok": ok, "label": label, "weight": weight, "status": "pass" if ok else "fail"})

    add(path.is_file() and path.stat().st_size > 64, "Nộp được tệp Office", 2)
    min_chars = int(rubric.get("min_chars") or 10)
    add(len(text) >= min_chars, f"Nội dung ≥ {min_chars} ký tự", 2)
    for needle in rubric.get("contains_text") or []:
        add(str(needle).lower() in text.lower(), f"Có chuỗi «{needle}»", 2)

    max_score = 100
    score = round(100 * points / total, 1) if total else 0
    return {
        "score": min(max_score, score),
        "verified": min(max_score, score),
        "pending": 0,
        "max_score": max_score,
        "complete": True,
        "status": "final",
        "checks": checks,
        "criteria": [],
        "chars": len(text),
        "grader_version": "legacy-text",
        "rubric_version": rubric.get("rubric_version") or "legacy",
    }
