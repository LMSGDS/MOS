"""Bốn chỉ số sư phạm: gợi ý sớm, sai lần 1, kẹt, dấu vết giáo viên."""
from __future__ import annotations

import hashlib

import pytest
from fastapi.testclient import TestClient

from app.db import cursor, init_schema
from app.main import app
from app.pedagogy import class_first_attempt_fail, class_hint_dependency, pedagogy_alerts
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


def test_hint_and_first_attempt_metrics(client):
    token = _token(client, "hocsinh")
    headers = {"Authorization": f"Bearer {token}"}
    started = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    attempt_id = started.json()["attempt_id"]
    hinted = client.post(
        f"/api/v1/attempts/{attempt_id}/telemetry",
        headers=headers,
        json={"events": [{"action": "hint", "detail": {"elapsed_ms": 8000}}]},
    )
    assert hinted.status_code == 200
    data = WORD11_RESULTS.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    submitted = client.post(
        f"/api/v1/attempts/{attempt_id}/submit",
        headers={**headers, "X-MOS-Artifact-SHA256": digest},
        files={"file": ("Word_1-1_results.docx", data, WORD_MIME)},
    )
    assert submitted.status_code == 200
    with cursor() as cur:
        cur.execute("SELECT attempt_id FROM first_attempt_q WHERE attempt_id = %s", (attempt_id,))
        assert cur.fetchone()
    hints = class_hint_dependency(24)
    assert any(int(r.get("early_hint") or 0) >= 1 for r in hints)
    firsts = class_first_attempt_fail(24)
    assert any(int(r.get("students") or 0) >= 1 for r in firsts)


def test_pedagogy_page_is_leadership_only(client):
    teacher = _token(client, "giaovien")
    denied = client.get(
        "/api/v1/insights/pedagogy",
        headers={"Authorization": f"Bearer {teacher}"},
    )
    assert denied.status_code == 403
    admin = _token(client, "admin")
    ok = client.get(
        "/api/v1/insights/pedagogy",
        headers={"Authorization": f"Bearer {admin}"},
    )
    assert ok.status_code == 200
    body = ok.json()
    assert "hints" in body and "first_fail" in body and "stuck" in body
    assert "teachers" in body and "alerts" in body
    web_teacher = TestClient(app)
    web_teacher.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"})
    live = web_teacher.get("/quan-tri/giam-sat")
    assert live.status_code == 200
    ping = web_teacher.post("/api/v1/staff/heartbeat", json={"live": True, "path": "/quan-tri/giam-sat"})
    assert ping.status_code == 200
    blocked = web_teacher.get("/quan-tri/su-pham", follow_redirects=False)
    assert blocked.status_code in {303, 307}
    web_admin = TestClient(app)
    web_admin.post("/dang-nhap", data={"username": "admin", "password": "Mos@Gds2026"})
    page = web_admin.get("/quan-tri/su-pham")
    assert page.status_code == 200
    assert "Chỉ số sư phạm" in page.text
    assert "Lạm dụng Gợi ý" in page.text
    assert pedagogy_alerts(24) is not None
