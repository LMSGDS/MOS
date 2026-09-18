"""Bố cục khung MOS-KulKul + cửa sổ Office.

Đo working area thật của máy, nhân tỉ lệ chuẩn (tham chiếu 1920×1040) để ra
kích thước thanh Navigation ở left / right / top / bottom.
Compact = cụm icon GMetrix đè TopMost, Office giữ full màn hình.
"""
from __future__ import annotations

from dataclasses import dataclass

REF_WORK_W = 1920
REF_WORK_H = 1040
CLUSTER_W = 260
CLUSTER_H = 108
CLUSTER_MARGIN = 8
HELP_W = 300
HELP_H = 248
EXPANDED_SIDE_W = 340
SUMMARY_W = 780
SUMMARY_H = 560
MIN_WORD = 400
OVERLAY_MIN_W = 80
OVERLAY_MIN_H = 48
REF_ICON = 40
REF_ICON_GAP = 3
REF_CHROME_PAD = 4


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


@dataclass(frozen=True)
class NavMetrics:
    cluster_w: int
    cluster_h: int
    margin: int
    help_w: int
    help_h: int
    expanded_side_w: int
    expanded_edge_h: int
    summary_w: int
    summary_h: int
    icon: int
    icon_gap: int
    chrome_pad: int
    fit: float


def px(logical: int, scale: float = 1.0) -> int:
    return max(1, int(round(logical * max(0.5, scale))))


def fit(work: Rect) -> float:
    sx = max(1, work.w) / REF_WORK_W
    sy = max(1, work.h) / REF_WORK_H
    return min(2.4, max(0.55, min(sx, sy)))


def _scale(reference: int, ratio: float, lo: int, hi: int) -> int:
    return max(lo, min(max(lo, hi), int(round(reference * ratio))))


def measure(work: Rect) -> NavMetrics:
    ratio = fit(work)
    icon = _scale(REF_ICON, ratio, 28, 56)
    gap = _scale(REF_ICON_GAP, ratio, 2, 6)
    pad = _scale(REF_CHROME_PAD, ratio, 3, 10)
    cap_w = max(180, work.w * 2 // 5)
    cap_h = max(80, work.h * 2 // 5)
    cluster_w = max(_scale(CLUSTER_W, ratio, 180, cap_w), 5 * (icon + 2 * gap) + 2 * pad)
    cluster_h = max(_scale(CLUSTER_H, ratio, 80, cap_h), 2 * (icon + 2 * gap) + 2 * pad)
    help_w = _scale(HELP_W, ratio, cluster_w, cap_w)
    help_h = _scale(HELP_H, ratio, 160, cap_h)
    margin = _scale(CLUSTER_MARGIN, ratio, 6, 16)
    side = _scale(EXPANDED_SIDE_W, ratio, 220, max(220, work.w // 3))
    edge = max(_scale(200, ratio, 140, 420), int(work.h * 0.22))
    summary_w = min(_scale(SUMMARY_W, ratio, 480, max(480, work.w - 40)), max(480, work.w - 40))
    summary_h = min(_scale(SUMMARY_H, ratio, 360, max(360, work.h - 40)), max(360, work.h - 40))
    return NavMetrics(
        cluster_w,
        cluster_h,
        margin,
        help_w,
        help_h,
        side,
        edge,
        summary_w,
        summary_h,
        icon,
        gap,
        pad,
        ratio,
    )


def size_for(work: Rect, state: str, compact: bool = True) -> tuple[int, int]:
    nav = measure(work)
    state = (state or "bottom").lower()
    if compact or state == "minimized":
        return nav.cluster_w, nav.cluster_h
    if state in ("left", "right"):
        return nav.expanded_side_w, work.h
    return work.w, nav.expanded_edge_h


def _clamp_word(word: Rect, work: Rect) -> Rect:
    w = max(MIN_WORD, min(word.w, work.w))
    h = max(MIN_WORD, min(word.h, work.h))
    x = max(work.x, min(word.x, work.right - w))
    y = max(work.y, min(word.y, work.bottom - h))
    return Rect(x, y, w, h)


def _cap(work_span: int, wanted: int, minimum: int) -> int:
    return max(minimum, min(wanted, work_span * 2 // 5))


def _scale_work(work: Rect, scale: float) -> Rect:
    if 0.99 < scale < 1.01:
        return work
    s = max(0.5, scale)
    return Rect(work.x, work.y, max(1, int(round(work.w * s))), max(1, int(round(work.h * s))))


def place(work: Rect, state: str, w: int, h: int, margin: int = CLUSTER_MARGIN) -> Rect:
    m = max(4, margin)
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
    nav = measure(_scale_work(work, scale))
    return place(work, state, nav.cluster_w, nav.cluster_h, nav.margin)


def compute(work: Rect, state: str, compact: bool = False, scale: float = 1.0) -> tuple[Rect, Rect]:
    state = (state or "bottom").lower()
    compact = compact or state == "minimized"
    nav = measure(_scale_work(work, scale))
    if compact:
        return place(work, state, nav.cluster_w, nav.cluster_h, nav.margin), work
    if state == "left":
        dock = Rect(work.x, work.y, nav.expanded_side_w, work.h)
        word = Rect(dock.right, work.y, work.w - nav.expanded_side_w, work.h)
    elif state == "right":
        dock = Rect(work.right - nav.expanded_side_w, work.y, nav.expanded_side_w, work.h)
        word = Rect(work.x, work.y, work.w - nav.expanded_side_w, work.h)
    elif state == "top":
        dock = Rect(work.x, work.y, work.w, nav.expanded_edge_h)
        word = Rect(work.x, dock.bottom, work.w, work.h - nav.expanded_edge_h)
    else:
        dock = Rect(work.x, work.bottom - nav.expanded_edge_h, work.w, nav.expanded_edge_h)
        word = Rect(work.x, work.y, work.w, work.h - nav.expanded_edge_h)
    return dock, _clamp_word(word, work)


def grow_for_help(dock: Rect, work: Rect, state: str, scale: float = 1.0) -> Rect:
    """Gắn thẻ Hướng dẫn vào cụm dock, tỉ lệ theo màn hình, không vượt 2/5 cạnh."""
    nav = measure(_scale_work(work, scale))
    w = max(dock.w, nav.help_w)
    h = dock.h + nav.help_h
    w = min(w, _cap(work.w, w, nav.cluster_w))
    h = min(h, _cap(work.h, h, dock.h + max(160, nav.help_h // 2)))
    x = dock.x - (w - dock.w) // 2
    y = dock.y if (state or "").lower() == "top" else dock.y - (h - dock.h)
    if x < work.x:
        x = work.x + nav.margin
    if x + w > work.right:
        x = work.right - w - nav.margin
    if y < work.y:
        y = work.y + nav.margin
    if y + h > work.bottom:
        y = work.bottom - h - nav.margin
    return Rect(x, y, w, h)


def overlap(a: Rect, b: Rect) -> bool:
    return not (a.right <= b.x or b.right <= a.x or a.bottom <= b.y or b.bottom <= a.y)
