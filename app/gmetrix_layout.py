"""Bố cục khung MOS Dock + cửa sổ Word — không chồng lấp (kiểu GMetrix)."""
from __future__ import annotations

from dataclasses import dataclass

BOTTOM_RATIO = 0.28
SIDE_RATIO = 0.30
MINIMIZED_HEIGHT = 48
MIN_WORD = 400


@dataclass(frozen=True)
class Rect:
    x: int
    y: int
    w: int
    h: int

    @property
    def right(self) -> int:
        return self.x + self.w

    @property
    def bottom(self) -> int:
        return self.y + self.h


def _clamp_word(word: Rect, work: Rect) -> Rect:
    w = max(MIN_WORD, min(word.w, work.w))
    h = max(MIN_WORD, min(word.h, work.h))
    x = max(work.x, min(word.x, work.right - w))
    y = max(work.y, min(word.y, work.bottom - h))
    return Rect(x, y, w, h)


def compute(work: Rect, state: str) -> tuple[Rect, Rect]:
    """Trả về (khung_dock, cua_so_word) trên vùng làm việc, không giao nhau."""
    state = (state or "bottom").lower()
    if state == "left":
        dock_w = max(280, int(work.w * SIDE_RATIO))
        dock = Rect(work.x, work.y, dock_w, work.h)
        word = Rect(dock.right, work.y, work.w - dock_w, work.h)
    elif state == "right":
        dock_w = max(280, int(work.w * SIDE_RATIO))
        dock = Rect(work.right - dock_w, work.y, dock_w, work.h)
        word = Rect(work.x, work.y, work.w - dock_w, work.h)
    elif state == "minimized":
        dock = Rect(work.x, work.bottom - MINIMIZED_HEIGHT, work.w, MINIMIZED_HEIGHT)
        word = Rect(work.x, work.y, work.w, work.h - MINIMIZED_HEIGHT)
    else:  # bottom — mặc định
        dock_h = max(180, int(work.h * BOTTOM_RATIO))
        dock = Rect(work.x, work.bottom - dock_h, work.w, dock_h)
        word = Rect(work.x, work.y, work.w, work.h - dock_h)
    return dock, _clamp_word(word, work)


def overlap(a: Rect, b: Rect) -> bool:
    return not (a.right <= b.x or b.right <= a.x or a.bottom <= b.y or b.bottom <= a.y)
