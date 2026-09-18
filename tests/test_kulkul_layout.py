from app.kulkul_layout import (
    BAR_H_MAX,
    BAR_W_MAX,
    CLUSTER_H,
    CLUSTER_W,
    DOCK_MAX_PCT,
    EXPANDED_SIDE_W,
    HELP_H,
    HELP_W,
    Rect,
    compute,
    grow_for_help,
    measure,
    overlap,
    size_for,
)

WORK = Rect(0, 0, 1920, 1040)
LAPTOP = Rect(0, 0, 1366, 728)
QHD = Rect(0, 0, 2560, 1400)
UHD = Rect(0, 0, 3840, 2120)

SCREENS = (
    ("HD+", Rect(0, 0, 1366, 728)),
    ("FHD", Rect(0, 0, 1920, 1040)),
    ("FHD-taskbar", Rect(0, 40, 1920, 1000)),
    ("QHD", Rect(0, 0, 2560, 1400)),
    ("UHD", Rect(0, 0, 3840, 2120)),
    ("SXGA", Rect(0, 0, 1280, 984)),
    ("WXGA+", Rect(0, 0, 1440, 860)),
)


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
    dock, word = compute(WORK, "bottom", compact=True)
    nav = measure(WORK)
    assert dock.w == WORK.w
    assert dock.h == nav.cluster_h == CLUSTER_H
    assert dock.x == WORK.x
    assert dock.y == WORK.bottom - nav.cluster_h
    assert word.h == WORK.h - nav.cluster_h
    assert not overlap(dock, word)

    left, word_l = compute(WORK, "left", compact=True)
    assert left.w == nav.cluster_w == CLUSTER_W
    assert left.h == WORK.h
    assert word_l.x == left.right
    assert not overlap(left, word_l)

    right, word_r = compute(WORK, "right", compact=True)
    assert right.h == WORK.h
    assert right.right == WORK.right
    assert not overlap(right, word_r)

    top, word_t = compute(WORK, "top", compact=True)
    assert top.w == WORK.w
    assert top.h == nav.cluster_h
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
    nav = measure(WORK)
    assert grown.w == WORK.w
    assert grown.h == dock.h + nav.help_h
    assert grown.h <= WORK.h * DOCK_MAX_PCT // 100
    assert grown.bottom == dock.bottom
    leftover = Rect(WORK.x, WORK.y, WORK.w, grown.y - WORK.y)
    assert not overlap(grown, leftover)


def test_grow_for_help_keeps_top_bar_at_top():
    dock, _ = compute(WORK, "top", compact=True)
    grown = grow_for_help(dock, WORK, "top")
    assert grown.y == dock.y
    assert grown.w == WORK.w
    assert grown.h > dock.h
    assert grown.h <= WORK.h * DOCK_MAX_PCT // 100


def test_help_overlay_stays_on_full_vertical_edge():
    dock, word = compute(WORK, "left", compact=True)
    grown = grow_for_help(dock, WORK, "left")
    cap = WORK.w * DOCK_MAX_PCT // 100
    assert not overlap(dock, word)
    assert grown.h == WORK.h
    assert grown.x == WORK.x
    assert dock.w < grown.w <= cap
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


def test_measured_screens_navigation_never_covers_word():
    """Đo từng working area: cạnh dài = 100% màn; cạnh ngắn ≤ 16%; Word không đè."""
    for name, work in SCREENS:
        nav = measure(work)
        assert BAR_W_MAX >= nav.cluster_w >= 52
        assert BAR_H_MAX >= nav.cluster_h >= 52
        for state in ("left", "right", "top", "bottom"):
            dock, word = compute(work, state, compact=True)
            grown = grow_for_help(dock, work, state)
            assert not overlap(dock, word), f"{name} {state} dock/word overlap"
            if state in ("left", "right"):
                assert dock.h == work.h, f"{name} {state} must span full height"
                assert dock.w == nav.cluster_w
                assert dock.w / work.w <= 0.08
                assert grown.h == work.h
                assert grown.w <= work.w * DOCK_MAX_PCT // 100
            else:
                assert dock.w == work.w, f"{name} {state} must span full width"
                assert dock.h == nav.cluster_h
                assert dock.h / work.h <= 0.09
                assert grown.w == work.w
                assert grown.h <= work.h * DOCK_MAX_PCT // 100
            leftover = (
                Rect(grown.right, work.y, work.right - grown.right, work.h)
                if state == "left"
                else Rect(work.x, work.y, grown.x - work.x, work.h)
                if state == "right"
                else Rect(work.x, grown.bottom, work.w, work.bottom - grown.bottom)
                if state == "top"
                else Rect(work.x, work.y, work.w, grown.y - work.y)
            )
            assert leftover.w >= 400 or leftover.h >= 400 or min(work.w, work.h) < 500
            assert not overlap(grown, leftover), f"{name} {state} help covers leftover Word"


def test_laptop_desktop_shrinks_navigation_thickness():
    nav = measure(LAPTOP)
    assert nav.fit < 1
    bottom, word_b = compute(LAPTOP, "bottom", compact=True)
    assert bottom.w == LAPTOP.w
    assert bottom.h == nav.cluster_h
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
            assert grown.w <= LAPTOP.w * DOCK_MAX_PCT // 100
        else:
            assert grown.w == LAPTOP.w
            assert grown.h <= LAPTOP.h * DOCK_MAX_PCT // 100


def test_large_desktop_keeps_navigation_thin():
    qhd, word_q = compute(QHD, "bottom", compact=True)
    uhd, word_u = compute(UHD, "left", compact=True)
    assert qhd.w == QHD.w
    assert qhd.h <= BAR_H_MAX
    assert not overlap(qhd, word_q)
    assert uhd.h == UHD.h
    assert uhd.w <= BAR_W_MAX
    assert not overlap(uhd, word_u)


def test_compact_scales_with_desktop_size():
    big = Rect(0, 0, 2880, 1560)
    dock, word = compute(big, "bottom", compact=True)
    assert dock.w == big.w
    assert dock.h <= BAR_H_MAX
    assert word.h == big.h - dock.h
    grown = grow_for_help(dock, big, "bottom")
    assert grown.w == big.w
    assert grown.h <= big.h * DOCK_MAX_PCT // 100
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
    assert grown.h <= LAPTOP.h * DOCK_MAX_PCT // 100
    assert grown.y > LAPTOP.y + LAPTOP.h // 2
