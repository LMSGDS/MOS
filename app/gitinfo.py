"""Revision đang chạy trên máy chủ — để đối chiếu với GitHub, không lộ secret."""
from __future__ import annotations

import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def git_revision() -> dict[str, str | None]:
    def _run(*args: str) -> str | None:
        try:
            out = subprocess.check_output(
                ["git", *args],
                cwd=ROOT,
                text=True,
                timeout=5,
                stderr=subprocess.DEVNULL,
            )
        except (OSError, subprocess.SubprocessError):
            return None
        text = (out or "").strip()
        return text or None

    return {
        "sha": _run("rev-parse", "--short", "HEAD"),
        "ref": _run("rev-parse", "--abbrev-ref", "HEAD"),
    }
