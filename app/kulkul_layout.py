"""Bố cục khung MOS-KulKul + cửa sổ Office.

Đo working area thật của máy, nhân tỉ lệ chuẩn (tham chiếu 1920×1040) để ra
kích thước thanh Navigation ở left / right / top / bottom.

Nguyên tắc cạnh:
- top / bottom: bung hết chiều ngang màn hình, dày ClusterH (compact) hoặc ExpandedEdgeH.
- left / right: bung hết chiều dọc màn hình, rộng ClusterW (compact) hoặc ExpandedSideW.
Word luôn chiếm phần working area còn lại — thanh Navigation không đè lên tài liệu.
"""
from __future__ import annotations

from dataclasses import dataclass

REF_WORK_W = 1920
REF_WORK_H = 1040
# Thanh compact: đo % working area, kẹp mỏng để không che Word.
BAR_H_PCT = 65  # 6.5% chiều cao
BAR_W_PCT = 38  # 3.8% chiều ngang
BAR_H_MIN, BAR_H_MAX = 52, 72
BAR_W_MIN, BAR_W_MAX = 52, 72
HELP_H_PCT = 90  # 9% thêm khi mở hướng dẫn
HELP_W_PCT = 90
DOCK_MAX_PCT = 16  # icon + help không quá 16% cạnh ngắn
CLUSTER_W = 72  # kẹp BAR_W_MAX sau round(1920 * 3.8%)
CLUSTER_H = 68  # round(1040 * 6.5%)
CLUSTER_MARGIN = 0
HELP_W = 173  # round(1920 * 9%)
HELP_H = 94  # round(1040 * 9%)
EXPANDED_SIDE_W = 340
SUMMARY_W = 1020
SUMMARY_H = 680
MIN_WORD = 400
OVERLAY_MIN_W = 48
OVERLAY_MIN_H = 48
REF_ICON = 32
REF_ICON_GAP = 2
REF_CHROME_PAD = 4
OVERLAY_CAP_PCT = 16


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
    bar_h = max(BAR_H_MIN, min(BAR_H_MAX, int(round(work.h * BAR_H_PCT / 1000))))
    bar_w = max(BAR_W_MIN, min(BAR_W_MAX, int(round(work.w * BAR_W_PCT / 1000))))
    pad = 4
    gap = 2
    icon = max(24, min(40, min(bar_h, bar_w) - 2 * pad))
    cap_w = max(bar_w, work.w * DOCK_MAX_PCT // 100)
    cap_h = max(bar_h, work.h * DOCK_MAX_PCT // 100)
    help_w = max(0, min(int(round(work.w * HELP_W_PCT / 1000)), cap_w - bar_w))
    help_h = max(0, min(int(round(work.h * HELP_H_PCT / 1000)), cap_h - bar_h))
    side = _scale(EXPANDED_SIDE_W, ratio, 220, max(220, work.w // 4))
    edge = max(bar_h + help_h, work.h * DOCK_MAX_PCT // 100)
    summary_w = min(_scale(SUMMARY_W, ratio, 480, max(480, work.w - 40)), max(480, work.w - 40))
    summary_h = min(_scale(SUMMARY_H, ratio, 360, max(360, work.h - 40)), max(360, work.h - 40))
    return NavMetrics(
        bar_w,
        bar_h,
        0,
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


def horizontal(state: str | None) -> bool:
    return (state or "bottom").lower() not in ("left", "right")


def size_for(work: Rect, state: str, compact: bool = True) -> tuple[int, int]:
    nav = measure(work)
    state = (state or "bottom").lower()
    compact = compact or state == "minimized"
    if state in ("left", "right"):
        return (nav.cluster_w if compact else nav.expanded_side_w), work.h
    return work.w, (nav.cluster_h if compact else nav.expanded_edge_h)


def _clamp_word(word: Rect, work: Rect) -> Rect:
    w = max(MIN_WORD, min(word.w, work.w))
    h = max(MIN_WORD, min(word.h, work.h))
    x = max(work.x, min(word.x, work.right - w))
    y = max(work.y, min(word.y, work.bottom - h))
    return Rect(x, y, w, h)


def _cap(work_span: int, wanted: int, minimum: int) -> int:
    return max(minimum, min(wanted, work_span * OVERLAY_CAP_PCT // 100))


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


def word_beside(work: Rect, dock: Rect, state: str | None) -> Rect:
    """Word occupies leftover working area so Navigation never covers the document."""
    state = (state or "bottom").lower()
    if state == "left":
        word = Rect(dock.right, work.y, work.right - dock.right, work.h)
    elif state == "right":
        word = Rect(work.x, work.y, dock.x - work.x, work.h)
    elif state == "top":
        word = Rect(work.x, dock.bottom, work.w, work.bottom - dock.bottom)
    else:
        word = Rect(work.x, work.y, work.w, dock.y - work.y)
    return _clamp_word(word, work)


def compute(work: Rect, state: str, compact: bool = False, scale: float = 1.0) -> tuple[Rect, Rect]:
    state = (state or "bottom").lower()
    compact = compact or state == "minimized"
    nav = measure(_scale_work(work, scale))
    side = nav.cluster_w if compact else nav.expanded_side_w
    edge = nav.cluster_h if compact else nav.expanded_edge_h
    if state == "left":
        dock = Rect(work.x, work.y, side, work.h)
    elif state == "right":
        dock = Rect(work.right - side, work.y, side, work.h)
    elif state == "top":
        dock = Rect(work.x, work.y, work.w, edge)
    else:
        dock = Rect(work.x, work.bottom - edge, work.w, edge)
    return dock, word_beside(work, dock, state)


def grow_for_help(dock: Rect, work: Rect, state: str, scale: float = 1.0) -> Rect:
    """Giữ cạnh dài = 100% working area; dày thêm help, tổng ≤ 16% cạnh ngắn."""
    nav = measure(_scale_work(work, scale))
    state = (state or "bottom").lower()
    if state in ("left", "right"):
        cap = max(nav.cluster_w, work.w * DOCK_MAX_PCT // 100)
        w = min(max(dock.w, dock.w + nav.help_w), cap, max(dock.w, work.w - MIN_WORD))
        x = work.x if state == "left" else work.right - w
        return Rect(x, work.y, w, work.h)
    cap = max(nav.cluster_h, work.h * DOCK_MAX_PCT // 100)
    h = min(max(dock.h, dock.h + nav.help_h), cap, max(dock.h, work.h - MIN_WORD))
    y = work.y if state == "top" else work.bottom - h
    return Rect(work.x, y, work.w, h)


def overlap(a: Rect, b: Rect) -> bool:
    return not (a.right <= b.x or b.right <= a.x or a.bottom <= b.y or b.bottom <= a.y)
