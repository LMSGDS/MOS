"""UDL adaptive path: rớt Word 1.1 liên tiếp → thẻ Định vị trên trang chủ."""
from __future__ import annotations

import pytest
from fastapi.testclient import TestClient

from app.accounts import create_account
from app.adaptive import WORD11, adaptive_cards
from app.db import cursor, init_schema
from app.main import app
from app.seed import seed
from tests.test_platform import postgres_ready


@pytest.fixture(scope="module")
def pg():
    if not postgres_ready():
        pytest.skip("PostgreSQL chưa sẵn sàng (DATABASE_URL)")
    init_schema()
    seed()


@pytest.fixture
def client(pg):
    return TestClient(app)


def test_locate_fail_streak_surfaces_card(client):
    try:
        person = create_account(
            username="adapt-locate",
            name="HS Adaptive",
            password="Mos@Gds2026",
            role="student",
            class_id=1,
        )
    except ValueError:
        with cursor() as cur:
            cur.execute("SELECT id FROM users WHERE username = %s", ("adapt-locate",))
            person = cur.fetchone()
    uid = person["id"]
    with cursor() as cur:
        for i in range(2):
            aid = f"adapt-w11-{uid}-{i}"
            cur.execute("DELETE FROM attempts WHERE id = %s", (aid,))
            cur.execute(
                """
                INSERT INTO attempts (
                  id, user_id, project_id, class_id, mode, status,
                  score, verified_score, started_at, submitted_at
                ) VALUES (
                  %s, %s, %s, 1, 'training', 'submitted',
                  20, 20, now() - interval '2 hours', now() - interval '10 seconds' + (%s || ' seconds')::interval
                )
                """,
                (aid, uid, WORD11, str(i)),
            )
            cur.execute(
                """
                INSERT INTO q_matrix_results (
                  attempt_id, criterion_id, locate, tool, configure, status
                ) VALUES (%s, 'q1', 'fail', 'fail', '', 'fail')
                """,
                (aid,),
            )
    cards = adaptive_cards(uid)
    assert cards and cards[0]["code"] == "locate-remedial"
    assert "Định vị" in cards[0]["title"]
    web = TestClient(app)
    web.post("/dang-nhap", data={"username": "adapt-locate", "password": "Mos@Gds2026"})
    home = web.get("/")
    assert home.status_code == 200
    assert "Luyện tập bổ trợ kỹ năng Định vị" in home.text
    token = client.post(
        "/api/v1/auth/login",
        json={"username": "adapt-locate", "password": "Mos@Gds2026", "chuong_trinh": "word"},
    )
    headers = {"Authorization": f"Bearer {token.json()['token']}"}
    api = client.get("/api/v1/progress/adaptive", headers=headers)
    assert api.status_code == 200
    assert any(c.get("code") == "locate-remedial" for c in api.json().get("cards") or [])
