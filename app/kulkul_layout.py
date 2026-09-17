"""Bố cục khung MOS-KulKul + cửa sổ Word — không chồng lấp.

Thanh điều hướng kiểu GMetrix: compact = thanh icon 48px (nút Trái/Phải nằm ngang),
không chiếm cả chiều cao màn hình.
"""
from __future__ import annotations

from dataclasses import dataclass

ICON_BAR_H = 48
ICON_BAR_W = 360
EXPANDED_SIDE_W = 340
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
    """Trả về (khung_dock, cua_so_word). compact = thanh icon ngang 48px."""
    state = (state or "bottom").lower()
    compact = compact or state == "minimized"
    if state == "left":
        if compact:
            dock = Rect(work.x, work.bottom - ICON_BAR_H, ICON_BAR_W, ICON_BAR_H)
            word = Rect(work.x, work.y, work.w, work.h - ICON_BAR_H)
        else:
            dock = Rect(work.x, work.y, EXPANDED_SIDE_W, work.h)
            word = Rect(dock.right, work.y, work.w - EXPANDED_SIDE_W, work.h)
    elif state == "right":
        if compact:
            dock = Rect(work.right - ICON_BAR_W, work.bottom - ICON_BAR_H, ICON_BAR_W, ICON_BAR_H)
            word = Rect(work.x, work.y, work.w, work.h - ICON_BAR_H)
        else:
            dock = Rect(work.right - EXPANDED_SIDE_W, work.y, EXPANDED_SIDE_W, work.h)
            word = Rect(work.x, work.y, work.w - EXPANDED_SIDE_W, work.h)
    elif state == "minimized":
        dock = Rect(work.x, work.bottom - ICON_BAR_H, work.w, ICON_BAR_H)
        word = Rect(work.x, work.y, work.w, work.h - ICON_BAR_H)
    else:  # bottom
        dock_h = ICON_BAR_H if compact else max(200, int(work.h * 0.22))
        dock = Rect(work.x, work.bottom - dock_h, work.w, dock_h)
        word = Rect(work.x, work.y, work.w, work.h - dock_h)
    return dock, _clamp_word(word, work)


def overlap(a: Rect, b: Rect) -> bool:
    return not (a.right <= b.x or b.right <= a.x or a.bottom <= b.y or b.bottom <= a.y)
