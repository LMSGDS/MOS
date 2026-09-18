from app.kulkul_layout import (
    CLUSTER_H,
    CLUSTER_W,
    EXPANDED_SIDE_W,
    HELP_H,
    HELP_W,
    OVERLAY_CAP_PCT,
    Rect,
    compute,
    grow_for_help,
    measure,
    overlap,
    size_for,
)

WORK = Rect(0, 0, 1920, 1040)  # 1080p trừ taskbar — tỉ lệ chuẩn = 1
LAPTOP = Rect(0, 0, 1366, 728)
QHD = Rect(0, 0, 2560, 1400)
UHD = Rect(0, 0, 3840, 2120)


def test_bottom_default_no_overlap():
    dock, word = compute(WORK, "bottom")
    assert dock.y + dock.h == WORK.bottom
    assert word.y == WORK.y
    assert word.h + dock.h == WORK.h
    assert not overlap(dock, word)
    assert word.h >= 400


def test_left_word_is_to_the_right():
    dock, word = compute(WORK, "left")
    assert dock.x == WORK.x
    assert dock.w == EXPANDED_SIDE_W
    assert word.x == dock.right
    assert word.w + dock.w == WORK.w
    assert not overlap(dock, word)


def test_right_word_is_to_the_left():
    dock, word = compute(WORK, "right")
    assert dock.right == WORK.right
    assert word.x == WORK.x
    assert word.right == dock.x
    assert not overlap(dock, word)


def test_top_word_is_below():
    dock, word = compute(WORK, "top")
    assert dock.y == WORK.y
    assert word.y == dock.bottom
    assert not overlap(dock, word)


def test_compact_spans_full_screen_edge():
    """Top/bottom bung hết ngang; left/right bung hết dọc; Word lấy phần còn lại."""
    dock, word = compute(WORK, "bottom", compact=True)
    assert dock.w == WORK.w
    assert dock.h == CLUSTER_H
    assert dock.x == WORK.x
    assert dock.y == WORK.bottom - CLUSTER_H
    assert word.h == WORK.h - CLUSTER_H
    assert word.w == WORK.w
    assert not overlap(dock, word)

    left, word_l = compute(WORK, "left", compact=True)
    assert left.w == CLUSTER_W
    assert left.h == WORK.h
    assert left.x == WORK.x
    assert left.y == WORK.y
    assert word_l.x == left.right
    assert word_l.w == WORK.w - CLUSTER_W
    assert not overlap(left, word_l)

    right, word_r = compute(WORK, "right", compact=True)
    assert right.w == CLUSTER_W
    assert right.h == WORK.h
    assert right.right == WORK.right
    assert word_r.right == right.x
    assert not overlap(right, word_r)

    top, word_t = compute(WORK, "top", compact=True)
    assert top.w == WORK.w
    assert top.h == CLUSTER_H
    assert top.y == WORK.y
    assert word_t.y == top.bottom
    assert not overlap(top, word_t)


def test_minimized_matches_compact_bottom():
    dock, word = compute(WORK, "minimized")
    dock2, word2 = compute(WORK, "bottom", compact=True)
    assert dock == dock2
    assert word == word2


def test_grow_for_help_keeps_full_bottom_span():
    dock, word = compute(WORK, "bottom", compact=True)
    grown = grow_for_help(dock, WORK, "bottom")
    assert grown.w == WORK.w
    assert grown.h == CLUSTER_H + HELP_H
    assert grown.x == WORK.x
    assert grown.bottom == dock.bottom
    assert grown.y == dock.y - HELP_H
    leftover = Rect(WORK.x, WORK.y, WORK.w, grown.y - WORK.y)
    assert not overlap(grown, leftover)
    assert leftover.h == word.h - HELP_H


def test_grow_for_help_keeps_top_bar_at_top():
    dock, _ = compute(WORK, "top", compact=True)
    grown = grow_for_help(dock, WORK, "top")
    assert grown.y == dock.y
    assert grown.w == WORK.w
    assert grown.h == CLUSTER_H + HELP_H


def test_help_overlay_stays_on_full_vertical_edge():
    dock, word = compute(WORK, "left", compact=True)
    grown = grow_for_help(dock, WORK, "left")
    cap = WORK.w * OVERLAY_CAP_PCT // 100
    assert word.x == dock.right
    assert not overlap(dock, word)
    assert grown.h == WORK.h
    assert grown.x == WORK.x
    assert grown.y == WORK.y
    assert CLUSTER_W < grown.w <= cap
    assert grown.w <= CLUSTER_W + HELP_W
    assert grown.w < WORK.w / 4


def test_reference_desktop_fit_is_one():
    nav = measure(WORK)
    assert nav.fit == 1.0
    assert nav.cluster_w == CLUSTER_W
    assert nav.cluster_h == CLUSTER_H
    assert nav.help_w == HELP_W
    assert nav.help_h == HELP_H


def test_each_position_uses_measured_full_edge():
    nav = measure(WORK)
    for state in ("left", "right"):
        w, h = size_for(WORK, state, compact=True)
        assert (w, h) == (nav.cluster_w, WORK.h)
        dock, word = compute(WORK, state, compact=True)
        assert dock.w == w
        assert dock.h == h
        assert not overlap(dock, word)
    for state in ("top", "bottom"):
        w, h = size_for(WORK, state, compact=True)
        assert (w, h) == (WORK.w, nav.cluster_h)
        dock, word = compute(WORK, state, compact=True)
        assert dock.w == w
        assert dock.h == h
        assert not overlap(dock, word)


def test_laptop_desktop_shrinks_navigation_thickness():
    nav = measure(LAPTOP)
    assert nav.fit < 1
    assert nav.cluster_w < CLUSTER_W
    assert nav.cluster_h < CLUSTER_H
    bottom, word_b = compute(LAPTOP, "bottom", compact=True)
    assert bottom.w == LAPTOP.w
    assert bottom.h == nav.cluster_h
    assert word_b.h == LAPTOP.h - nav.cluster_h
    assert not overlap(bottom, word_b)
    left, word_l = compute(LAPTOP, "left", compact=True)
    assert left.h == LAPTOP.h
    assert left.w == nav.cluster_w
    assert not overlap(left, word_l)
    for state in ("left", "right", "top", "bottom"):
        dock, word = compute(LAPTOP, state, compact=True)
        grown = grow_for_help(dock, LAPTOP, state)
        assert not overlap(dock, word)
        if state in ("left", "right"):
            assert grown.h == LAPTOP.h
            assert grown.w <= LAPTOP.w * OVERLAY_CAP_PCT // 100
        else:
            assert grown.w == LAPTOP.w
            assert grown.h <= LAPTOP.h * OVERLAY_CAP_PCT // 100


def test_large_desktop_keeps_navigation_thin():
    nav_qhd = measure(QHD)
    nav_uhd = measure(UHD)
    assert nav_qhd.fit > 1
    assert nav_uhd.fit >= nav_qhd.fit
    qhd, word_q = compute(QHD, "bottom", compact=True)
    uhd, word_u = compute(UHD, "left", compact=True)
    assert qhd.w == QHD.w
    assert CLUSTER_H <= qhd.h <= 100
    assert word_q.h == QHD.h - qhd.h
    assert uhd.h == UHD.h
    assert uhd.w <= 240
    assert word_u.w == UHD.w - uhd.w
    assert not overlap(qhd, word_q)
    assert not overlap(uhd, word_u)


def test_compact_scales_with_desktop_size():
    big = Rect(0, 0, 2880, 1560)
    dock, word = compute(big, "bottom", compact=True)
    assert dock.w == big.w
    assert CLUSTER_H <= dock.h <= 100
    assert word.h == big.h - dock.h
    grown = grow_for_help(dock, big, "bottom")
    assert grown.w == big.w
    assert grown.h <= big.h * OVERLAY_CAP_PCT // 100
    assert grown.h >= dock.h
    assert grown.h < big.h / 4
    assert grown.bottom == dock.bottom


def test_unknown_state_defaults_to_bottom():
    dock, word = compute(WORK, None)
    dock2, word2 = compute(WORK, "bottom")
    assert dock == dock2
    assert word == word2
    assert not overlap(dock, word)


def test_laptop_help_bar_stays_on_screen_edge():
    dock, word = compute(LAPTOP, "bottom", compact=True)
    grown = grow_for_help(dock, LAPTOP, "bottom")
    assert not overlap(dock, word)
    assert grown.bottom == dock.bottom
    assert grown.w == LAPTOP.w
    assert grown.h <= LAPTOP.h * OVERLAY_CAP_PCT // 100
    assert grown.y > LAPTOP.y + LAPTOP.h // 2
