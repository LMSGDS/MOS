"""Bố cục khung MOS-KulKul + cửa sổ Office.

Compact = cụm icon GMetrix (260×108) đè TopMost, Office giữ gần như full màn hình.
Help gắn vào cụm đó thành thẻ nhỏ (không quá 2/5 màn hình).
Expanded = khung nhiệm vụ cạnh Office, không chồng lấp.
"""
from __future__ import annotations

from dataclasses import dataclass

CLUSTER_W = 260
CLUSTER_H = 108
CLUSTER_MARGIN = 8
HELP_W = 300
HELP_H = 248
EXPANDED_SIDE_W = 340
MIN_WORD = 400
OVERLAY_MIN_W = 80
OVERLAY_MIN_H = 48


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


def px(logical: int, scale: float = 1.0) -> int:
    return max(1, int(round(logical * max(0.5, scale))))


def _clamp_word(word: Rect, work: Rect) -> Rect:
    w = max(MIN_WORD, min(word.w, work.w))
    h = max(MIN_WORD, min(word.h, work.h))
    x = max(work.x, min(word.x, work.right - w))
    y = max(work.y, min(word.y, work.bottom - h))
    return Rect(x, y, w, h)


def _cap(work_span: int, wanted: int, minimum: int) -> int:
    return max(minimum, min(wanted, work_span * 2 // 5))


def place(work: Rect, state: str, w: int, h: int) -> Rect:
    m = CLUSTER_MARGIN
    state = (state or "bottom").lower()
    w = max(OVERLAY_MIN_W, w)
    h = max(OVERLAY_MIN_H, h)
    if state == "left":
        return Rect(work.x + m, work.y + max(m, (work.h - h) // 2), w, h)
    if state == "right":
        return Rect(work.right - w - m, work.y + max(m, (work.h - h) // 2), w, h)
    if state == "top":
        return Rect(work.x + max(m, (work.w - w) // 2), work.y + m, w, h)
    return Rect(work.x + max(m, (work.w - w) // 2), work.bottom - h - m, w, h)


def cluster(work: Rect, state: str, scale: float = 1.0) -> Rect:
    return place(work, state, px(CLUSTER_W, scale), px(CLUSTER_H, scale))


def compute(work: Rect, state: str, compact: bool = False, scale: float = 1.0) -> tuple[Rect, Rect]:
    state = (state or "bottom").lower()
    compact = compact or state == "minimized"
    if compact:
        return cluster(work, state, scale), work
    side = px(EXPANDED_SIDE_W, scale)
    if state == "left":
        dock = Rect(work.x, work.y, side, work.h)
        word = Rect(dock.right, work.y, work.w - side, work.h)
    elif state == "right":
        dock = Rect(work.right - side, work.y, side, work.h)
        word = Rect(work.x, work.y, work.w - side, work.h)
    elif state == "top":
        dock_h = max(px(200, scale), int(work.h * 0.22))
        dock = Rect(work.x, work.y, work.w, dock_h)
        word = Rect(work.x, dock.bottom, work.w, work.h - dock_h)
    else:
        dock_h = max(px(200, scale), int(work.h * 0.22))
        dock = Rect(work.x, work.bottom - dock_h, work.w, dock_h)
        word = Rect(work.x, work.y, work.w, work.h - dock_h)
    return dock, _clamp_word(word, work)


def grow_for_help(dock: Rect, work: Rect, state: str, scale: float = 1.0) -> Rect:
    """Gắn thẻ Hướng dẫn vào cụm dock, không vượt 2/5 cạnh màn hình."""
    help_w = px(HELP_W, scale)
    help_h = px(HELP_H, scale)
    w = max(dock.w, help_w)
    h = dock.h + help_h
    w = min(w, _cap(work.w, w, px(CLUSTER_W, scale)))
    h = min(h, _cap(work.h, h, dock.h + px(160, scale)))
    x = dock.x - (w - dock.w) // 2
    y = dock.y if (state or "").lower() == "top" else dock.y - (h - dock.h)
    if x < work.x:
        x = work.x + CLUSTER_MARGIN
    if x + w > work.right:
        x = work.right - w - CLUSTER_MARGIN
    if y < work.y:
        y = work.y + CLUSTER_MARGIN
    if y + h > work.bottom:
        y = work.bottom - h - CLUSTER_MARGIN
    return Rect(x, y, w, h)


def overlap(a: Rect, b: Rect) -> bool:
    return not (a.right <= b.x or b.right <= a.x or a.bottom <= b.y or b.bottom <= a.y)
