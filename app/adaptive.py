"""Lộ trình thích ứng UDL trên LMS riêng — không bị kẹt trong iframe Canvas.

Chỉ giao bài MOS / Q-Matrix. Không forum, luận, hay quiz chung.
"""
from __future__ import annotations

from app.db import cursor

WORD11 = "word-objective-1-1"
STREAK = 2
LOCATE_SCORE = 50


def adaptive_cards(user_id: int) -> list[dict]:
    """Nếu rớt liên tục Word Objective 1.1 (Định vị), đẩy thẻ luyện bổ trợ lên đầu."""
    if not user_id:
        return []
    with cursor() as cur:
        cur.execute(
            """
            SELECT a.id, a.score, a.verified_score, a.submitted_at, p.title
            FROM attempts a
            JOIN projects p ON p.id = a.project_id
            WHERE a.user_id = %s
              AND a.project_id = %s
              AND a.status IN ('submitted', 'graded')
            ORDER BY a.submitted_at DESC NULLS LAST
            LIMIT %s
            """,
            (user_id, WORD11, STREAK),
        )
        rows = list(cur.fetchall())
        if len(rows) < STREAK:
            return []
        stuck = True
        for row in rows:
            score = float(row.get("verified_score") if row.get("verified_score") is not None else row.get("score") or 0)
            cur.execute(
                """
                SELECT locate, tool FROM q_matrix_results
                WHERE attempt_id = %s
                ORDER BY id
                LIMIT 1
                """,
                (row["id"],),
            )
            q1 = cur.fetchone() or {}
            locate_fail = (q1.get("locate") == "fail") or score < LOCATE_SCORE
            if not locate_fail:
                stuck = False
                break
        if not stuck:
            return []
        cur.execute("SELECT id, title FROM projects WHERE id = %s", (WORD11,))
        project = cur.fetchone() or {"id": WORD11, "title": "Word Objective 1.1"}
    return [
        {
            "code": "locate-remedial",
            "project_id": project["id"],
            "title": "Luyện tập bổ trợ kỹ năng Định vị",
            "reason": (
                f"Rớt {STREAK} lần liên tiếp ở {project.get('title') or 'Word 1.1'} "
                "(bước Định vị / điểm thấp). Làm lại bài này trước — không đợi giáo viên giao tay."
            ),
            "skill": "Định vị",
            "program": "word",
        }
    ]
