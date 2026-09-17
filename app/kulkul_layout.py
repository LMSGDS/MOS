"""Bố cục khung MOS-KulKul + cửa sổ Office.

Compact = cụm icon GMetrix (260×108) đè TopMost, Office giữ gần như full màn hình.
Expanded = khung nhiệm vụ cạnh Office, không chồng lấp.
"""
from __future__ import annotations

from dataclasses import dataclass

CLUSTER_W = 260
CLUSTER_H = 108
CLUSTER_MARGIN = 8
HELP_W = 320
HELP_H = 360
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


def cluster(work: Rect, state: str) -> Rect:
    w, h, m = CLUSTER_W, CLUSTER_H, CLUSTER_MARGIN
    state = (state or "bottom").lower()
    if state == "left":
        return Rect(work.x + m, work.y + max(m, (work.h - h) // 2), w, h)
    if state == "right":
        return Rect(work.right - w - m, work.y + max(m, (work.h - h) // 2), w, h)
    if state == "top":
        return Rect(work.x + max(m, (work.w - w) // 2), work.y + m, w, h)
    return Rect(work.x + max(m, (work.w - w) // 2), work.bottom - h - m, w, h)


def compute(work: Rect, state: str, compact: bool = False) -> tuple[Rect, Rect]:
    state = (state or "bottom").lower()
    compact = compact or state == "minimized"
    if compact:
        return cluster(work, state), work
    if state == "left":
        dock = Rect(work.x, work.y, EXPANDED_SIDE_W, work.h)
        word = Rect(dock.right, work.y, work.w - EXPANDED_SIDE_W, work.h)
    elif state == "right":
        dock = Rect(work.right - EXPANDED_SIDE_W, work.y, EXPANDED_SIDE_W, work.h)
        word = Rect(work.x, work.y, work.w - EXPANDED_SIDE_W, work.h)
    elif state == "top":
        dock_h = max(200, int(work.h * 0.22))
        dock = Rect(work.x, work.y, work.w, dock_h)
        word = Rect(work.x, dock.bottom, work.w, work.h - dock_h)
    else:
        dock_h = max(200, int(work.h * 0.22))
        dock = Rect(work.x, work.bottom - dock_h, work.w, dock_h)
        word = Rect(work.x, work.y, work.w, work.h - dock_h)
    return dock, _clamp_word(word, work)


def grow_for_help(dock: Rect, work: Rect, state: str) -> Rect:
    """Mở rộng cụm dock để chứa khung Hướng dẫn phía trên (trừ khi dock đang ở mép trên)."""
    w = max(dock.w, HELP_W)
    h = dock.h + HELP_H
    x = dock.x - (w - dock.w) // 2
    y = dock.y if (state or "").lower() == "top" else dock.y - HELP_H
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
