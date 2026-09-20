"""Starter extras that travel with a MOS project folder (outline, images, 3mf)."""
from __future__ import annotations

from pathlib import Path

EXTRA_SUFFIXES = {".docx", ".xlsx", ".jpg", ".jpeg", ".png", ".3mf"}


def project_extra_files(file_path: str | Path | None) -> list[Path]:
    if not file_path:
        return []
    path = Path(file_path)
    folder = path.parent
    if not folder.is_dir():
        return []
    extras: list[Path] = []
    for item in sorted(folder.iterdir()):
        if not item.is_file() or item.name == path.name:
            continue
        if item.suffix.lower() in EXTRA_SUFFIXES:
            extras.append(item)
    return extras


def resolve_extra(file_path: str | Path | None, name: str) -> Path | None:
    safe = Path(name or "").name
    if not safe or safe != name or "/" in name or "\\" in name:
        return None
    extras = {item.name: item for item in project_extra_files(file_path)}
    return extras.get(safe)
