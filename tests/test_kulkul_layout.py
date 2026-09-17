from app.kulkul_layout import CLUSTER_H, CLUSTER_W, EXPANDED_SIDE_W, Rect, compute, overlap

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
    assert dock.y == WORK.bottom - CLUSTER_H - 8

    left, word_l = compute(WORK, "left", compact=True)
    assert left.w == CLUSTER_W
    assert left.h == CLUSTER_H
    assert left.x == WORK.x + 8
    assert word_l == WORK

    right, _ = compute(WORK, "right", compact=True)
    assert right.x == WORK.right - CLUSTER_W - 8

    top, _ = compute(WORK, "top", compact=True)
    assert top.y == WORK.y + 8


def test_minimized_matches_compact_bottom():
    dock, word = compute(WORK, "minimized")
    dock2, word2 = compute(WORK, "bottom", compact=True)
    assert dock == dock2
    assert word == word2


def test_unknown_state_defaults_to_bottom():
    dock, word = compute(WORK, None)
    dock2, word2 = compute(WORK, "bottom")
    assert dock == dock2
    assert word == word2
    assert not overlap(dock, word)
