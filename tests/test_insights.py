"""Heatmap lỗ hổng, độ tin cậy đề, radar, giám sát có cảnh báo."""
from __future__ import annotations

import hashlib

import pytest
from fastapi.testclient import TestClient

from app.db import cursor, init_schema
from app.insights import annotate_sessions, bank_reliability, skill_gaps
from app.main import app
from app.seed import seed
from tests.test_platform import WORD11_RESULTS, WORD_MIME, _token, postgres_ready


@pytest.fixture(scope="module")
def pg():
    if not postgres_ready():
        pytest.skip("PostgreSQL chưa sẵn sàng (DATABASE_URL)")
    init_schema()
    seed()


@pytest.fixture
def client(pg):
    return TestClient(app)


def test_insights_api_and_pages(client):
    token = _token(client, "hocsinh")
    headers = {"Authorization": f"Bearer {token}"}
    started = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    attempt_id = started.json()["attempt_id"]
    data = WORD11_RESULTS.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    submitted = client.post(
        f"/api/v1/attempts/{attempt_id}/submit",
        headers={**headers, "X-MOS-Artifact-SHA256": digest},
        files={"file": ("Word_1-1_results.docx", data, WORD_MIME)},
    )
    assert submitted.status_code == 200

    student_denied = client.get("/api/v1/insights/gaps", headers=headers)
    assert student_denied.status_code == 403
    teacher = _token(client, "giaovien")
    th = {"Authorization": f"Bearer {teacher}"}
    gaps = client.get("/api/v1/insights/gaps", headers=th)
    assert gaps.status_code == 200
    assert gaps.json()["ok"] is True
    bank = client.get("/api/v1/insights/bank", headers=th)
    assert bank.status_code == 200
    assert any(row["exam_id"] == "word-objective-1-1" for row in bank.json()["bank"])
    radar = client.get("/api/v1/progress/radar", headers=headers)
    assert radar.status_code == 200
    labels = {a["label"] for a in radar.json()["axes"]}
    assert labels == {"Word", "Excel", "PowerPoint"}
    word = next(a for a in radar.json()["axes"] if a["program"] == "word")
    assert word["score"] >= 0

    web = TestClient(app)
    web.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"})
    assert "Lỗ hổng kiến thức" in web.get("/quan-tri/lo-hong").text
    bank_page = web.get("/quan-tri/ngan-hang", follow_redirects=False)
    assert bank_page.status_code == 303
    assert bank_page.headers["location"] == "/quan-tri/bai-tap"
    admin = TestClient(app)
    admin.post("/dang-nhap", data={"username": "admin", "password": "Mos@Gds2026"})
    assert "Độ tin cậy ngân hàng đề" in admin.get("/quan-tri/ngan-hang").text
    assert "Q-Matrix" in admin.get("/quan-tri/ngan-hang").text
    live = web.get("/quan-tri/giam-sat")
    assert live.status_code == 200
    assert "proctor-grid" in live.text
    assert "Luyện tập" in live.text or "Thi" in live.text


def test_annotate_stuck_session():
    from datetime import datetime, timedelta, timezone

    old = datetime.now(timezone.utc) - timedelta(minutes=12)
    rows = annotate_sessions(
        [
            {
                "session_id": "ghost",
                "status": "IN_PROGRESS",
                "updated_at": old,
                "student": "HS",
            }
        ]
    )
    assert rows[0]["alert"] is True
    assert any(a["code"] == "stuck" for a in rows[0]["alerts"])
    assert rows[0]["color"] == "red"


def test_skill_gaps_and_bank_query(client):
    assert isinstance(skill_gaps(0), list)
    assert isinstance(bank_reliability(), list)
    with cursor() as cur:
        cur.execute("SELECT COUNT(*) AS n FROM q_matrix_results")
        assert cur.fetchone()["n"] >= 0
