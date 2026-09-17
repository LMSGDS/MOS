"""Bố cục khung MOS-KulKul + cửa sổ Word — không chồng lấp."""
from __future__ import annotations

from dataclasses import dataclass

BOTTOM_RATIO = 0.28
SIDE_RATIO = 0.30
CONTROLS_H = 96
CONTROLS_W = 248
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


def compute(work: Rect, state: str, compact: bool = False) -> tuple[Rect, Rect]:
    """Trả về (khung_dock, cua_so_word). compact = chỉ thanh điều khiển, vừa đủ nút."""
    state = (state or "bottom").lower()
    compact = compact or state == "minimized"
    if state == "left":
        dock_w = CONTROLS_W if compact else max(280, int(work.w * SIDE_RATIO))
        dock = Rect(work.x, work.y, dock_w, work.h)
        word = Rect(dock.right, work.y, work.w - dock_w, work.h)
    elif state == "right":
        dock_w = CONTROLS_W if compact else max(280, int(work.w * SIDE_RATIO))
        dock = Rect(work.right - dock_w, work.y, dock_w, work.h)
        word = Rect(work.x, work.y, work.w - dock_w, work.h)
    elif state == "minimized":
        dock = Rect(work.x, work.bottom - CONTROLS_H, work.w, CONTROLS_H)
        word = Rect(work.x, work.y, work.w, work.h - CONTROLS_H)
    else:  # bottom
        dock_h = CONTROLS_H if compact else max(180, int(work.h * BOTTOM_RATIO))
        dock = Rect(work.x, work.bottom - dock_h, work.w, dock_h)
        word = Rect(work.x, work.y, work.w, work.h - dock_h)
    return dock, _clamp_word(word, work)


def overlap(a: Rect, b: Rect) -> bool:
    return not (a.right <= b.x or b.right <= a.x or a.bottom <= b.y or b.bottom <= a.y)
