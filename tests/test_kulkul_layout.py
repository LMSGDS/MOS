from app.kulkul_layout import (
    CLUSTER_H,
    CLUSTER_MARGIN,
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


def test_compact_is_small_gmetrix_cluster():
    dock, word = compute(WORK, "bottom", compact=True)
    assert dock.w == CLUSTER_W
    assert dock.h == CLUSTER_H
    assert word == WORK
    assert dock.y == WORK.bottom - CLUSTER_H - CLUSTER_MARGIN

    left, word_l = compute(WORK, "left", compact=True)
    assert left.w == CLUSTER_W
    assert left.h == CLUSTER_H
    assert left.x == WORK.x + CLUSTER_MARGIN
    assert word_l == WORK

    right, _ = compute(WORK, "right", compact=True)
    assert right.x == WORK.right - CLUSTER_W - CLUSTER_MARGIN

    top, _ = compute(WORK, "top", compact=True)
    assert top.y == WORK.y + CLUSTER_MARGIN


def test_minimized_matches_compact_bottom():
    dock, word = compute(WORK, "minimized")
    dock2, word2 = compute(WORK, "bottom", compact=True)
    assert dock == dock2
    assert word == word2


def test_grow_for_help_sits_above_bottom_cluster():
    dock, _ = compute(WORK, "bottom", compact=True)
    grown = grow_for_help(dock, WORK, "bottom")
    assert grown.w == HELP_W
    assert grown.h == CLUSTER_H + HELP_H
    assert grown.bottom == dock.bottom
    assert grown.y == dock.y - HELP_H


def test_grow_for_help_keeps_top_cluster_at_top():
    dock, _ = compute(WORK, "top", compact=True)
    grown = grow_for_help(dock, WORK, "top")
    assert grown.y == dock.y
    assert grown.h == CLUSTER_H + HELP_H


def test_help_overlay_is_small_gmetrix_card():
    """Thanh Navigation + Help không được chiếm nửa màn hình như panel 960×600."""
    dock, word = compute(WORK, "left", compact=True)
    grown = grow_for_help(dock, WORK, "left")
    assert word == WORK
    assert grown.w <= HELP_W
    assert grown.h == CLUSTER_H + HELP_H
    assert grown.w < WORK.w / 5
    assert grown.h < WORK.h * OVERLAY_CAP_PCT / 100 + 1
    assert grown.w <= 260
    assert grown.h <= 220
    assert grown.x == WORK.x + CLUSTER_MARGIN


def test_reference_desktop_fit_is_one():
    nav = measure(WORK)
    assert nav.fit == 1.0
    assert nav.cluster_w == CLUSTER_W
    assert nav.cluster_h == CLUSTER_H
    assert nav.help_w == HELP_W
    assert nav.help_h == HELP_H


def test_each_position_uses_measured_cluster_size():
    nav = measure(WORK)
    for state in ("left", "right", "top", "bottom"):
        w, h = size_for(WORK, state, compact=True)
        assert (w, h) == (nav.cluster_w, nav.cluster_h)
        dock, word = compute(WORK, state, compact=True)
        assert dock.w == w
        assert dock.h == h
        assert word == WORK


def test_laptop_desktop_shrinks_navigation():
    nav = measure(LAPTOP)
    assert nav.fit < 1
    assert nav.cluster_w < CLUSTER_W
    assert nav.cluster_h < CLUSTER_H
    for state in ("left", "right", "top", "bottom"):
        dock, word = compute(LAPTOP, state, compact=True)
        assert dock.w == nav.cluster_w
        assert dock.h == nav.cluster_h
        assert dock.w < LAPTOP.w / 5
        assert dock.h < LAPTOP.h / 6
        assert word == LAPTOP
        grown = grow_for_help(dock, LAPTOP, state)
        assert grown.w < LAPTOP.w / 4
        assert grown.h <= LAPTOP.h * OVERLAY_CAP_PCT // 100
        assert grown.h < 220


def test_large_desktop_keeps_navigation_compact():
    nav_qhd = measure(QHD)
    nav_uhd = measure(UHD)
    assert nav_qhd.fit > 1
    assert nav_uhd.fit >= nav_qhd.fit
    qhd, _ = compute(QHD, "bottom", compact=True)
    uhd, _ = compute(UHD, "left", compact=True)
    assert CLUSTER_W <= qhd.w <= 240
    assert CLUSTER_H <= qhd.h <= 100
    assert uhd.w <= 240
    assert uhd.h <= 100
    assert uhd.w < UHD.w / 8
    assert uhd.h < UHD.h / 8


def test_compact_scales_with_desktop_size():
    big = Rect(0, 0, 2880, 1560)
    dock, _ = compute(big, "bottom", compact=True)
    assert CLUSTER_W <= dock.w <= 240
    assert CLUSTER_H <= dock.h <= 100
    grown = grow_for_help(dock, big, "bottom")
    assert grown.w <= 260
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


def test_laptop_help_card_stays_on_screen_edge():
    dock, word = compute(LAPTOP, "bottom", compact=True)
    grown = grow_for_help(dock, LAPTOP, "bottom")
    assert word == LAPTOP
    assert grown.bottom == dock.bottom
    assert grown.h <= 160
    assert grown.w <= 240
    assert grown.y > LAPTOP.y + LAPTOP.h // 2
