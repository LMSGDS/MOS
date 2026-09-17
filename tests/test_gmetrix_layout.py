from app.gmetrix_layout import Rect, compute, overlap

WORK = Rect(0, 0, 1920, 1040)  # 1080p trừ taskbar


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
    assert word.x == dock.right
    assert word.w + dock.w == WORK.w
    assert not overlap(dock, word)


def test_right_word_is_to_the_left():
    dock, word = compute(WORK, "right")
    assert dock.right == WORK.right
    assert word.x == WORK.x
    assert word.right == dock.x
    assert not overlap(dock, word)


def test_minimized_is_thin_bottom_bar():
    dock, word = compute(WORK, "minimized")
    assert dock.h == 96
    assert word.h == WORK.h - 96
    assert not overlap(dock, word)


def test_compact_after_open_is_controls_only():
    dock, word = compute(WORK, "bottom", compact=True)
    assert dock.h == 96
    assert word.h == WORK.h - 96
    assert not overlap(dock, word)
    left, word_l = compute(WORK, "left", compact=True)
    assert left.w == 248
    assert word_l.x == 248
    assert not overlap(left, word_l)


def test_unknown_state_defaults_to_bottom():
    dock, word = compute(WORK, None)
    dock2, word2 = compute(WORK, "bottom")
    assert dock == dock2
    assert word == word2
    assert not overlap(dock, word)
