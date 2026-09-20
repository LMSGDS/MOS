"""Exercise catalog, student links, progress and evaluation."""
from __future__ import annotations

from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from app.db import cursor, init_schema
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


def test_exercise_catalog_and_class_assignment(client):
    token = _token(client, "giaovien")
    headers = {"Authorization": f"Bearer {token}"}
    catalog = client.get("/api/v1/exercises", headers=headers)
    assert catalog.status_code == 200
    ids = [e["id"] for e in catalog.json()["exercises"]]
    assert "word-objective-1-1" in ids
    assert "word-objective-6-2" in ids
    assert "powerpoint-objective-1-1" in ids
    assert "powerpoint-objective-5-3" in ids
    roster = client.get("/api/v1/students", headers=headers)
    assert roster.status_code == 200
    names = {s["username"] for s in roster.json()["students"]}
    assert "hocsinh" in names
    assert "hocsinh2" in names
    linked = next(s for s in roster.json()["students"] if s["username"] == "hocsinh")
    assert linked["class_name"] == "10A1"
    assert linked["student_code"] == "HS001"


def test_student_progress_updates_on_checkpoint(client):
    token = _token(client, "hocsinh")
    headers = {"Authorization": f"Bearer {token}"}
    started = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    attempt_id = started.json()["attempt_id"]
    check = client.post(
        f"/api/v1/attempts/{attempt_id}/checkpoints",
        headers=headers,
        files={"file": ("Word_1-1_results.docx", WORD11_RESULTS.read_bytes(), WORD_MIME)},
    )
    assert check.status_code == 200
    mine = client.get("/api/v1/progress", headers=headers)
    assert mine.status_code == 200
    body = mine.json()
    assert body["evaluation"]["exercises_started"] >= 1
    row = next(e for e in body["exercises"] if e["project_id"] == "word-objective-1-1")
    assert row["status"] in {"in_progress", "submitted", "mastered"}
    assert float(row["best_verified"] or 0) >= 62
    assert body["evaluation"]["level"] in {"bat_dau", "dang_tien_bo", "dat", "xuat_sac"}
    assert "Word" in (body["evaluation"]["summary"] or "") or "word" in (body["evaluation"]["summary"] or "").lower()


def test_student_cannot_read_other_evaluation(client):
    from app.db import cursor

    token_a = _token(client, "hocsinh")
    with cursor() as cur:
        cur.execute("SELECT id FROM users WHERE username = %s", ("hocsinh2",))
        other = cur.fetchone()
    if not other:
        return
    denied = client.get(
        f"/api/v1/students/{other['id']}/evaluation",
        headers={"Authorization": f"Bearer {token_a}"},
    )
    assert denied.status_code == 403
    teacher = _token(client, "giaovien")
    ok = client.get(
        f"/api/v1/students/{other['id']}/evaluation",
        headers={"Authorization": f"Bearer {teacher}"},
    )
    assert ok.status_code == 200
    assert ok.json()["evaluation"]["username"] == "hocsinh2"


def test_admin_student_pages(client):
    teacher = TestClient(app)
    teacher.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"})
    home = teacher.get("/quan-tri")
    assert home.status_code == 200
    assert "Đánh giá học sinh" in home.text
    assert "Bài tập" in home.text
    students = teacher.get("/quan-tri/hoc-sinh")
    assert students.status_code == 200
    assert "HS001" in students.text
    assert "10A1" in students.text
    exercises = teacher.get("/quan-tri/bai-tap")
    assert exercises.status_code == 200
    assert "word-objective-1-1" in exercises.text
    with cursor() as cur:
        cur.execute("SELECT id FROM users WHERE username = %s", ("hocsinh",))
        uid = cur.fetchone()["id"]
    detail = teacher.get(f"/quan-tri/hoc-sinh/{uid}")
    assert detail.status_code == 200
    assert "Tiến độ từng bài tập" in detail.text
    student = TestClient(app)
    student.post("/dang-nhap", data={"username": "hocsinh", "password": "Mos@Gds2026"})
    page = student.get("/tien-do")
    assert page.status_code == 200
    assert "Tiến độ" in page.text


def test_mastered_after_full_score(client):
    import json

    from tests.test_word_actions import DEMO_W11

    token = _token(client, "hocsinh")
    headers = {"Authorization": f"Bearer {token}"}
    started = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-objective-1-2", "mode": "training"},
    )
    attempt_id = started.json()["attempt_id"]
    from pathlib import Path

    results = Path(__file__).resolve().parent / "fixtures" / "word-objective-1-2" / "Word_1-2_results.docx"
    submitted = client.post(
        f"/api/v1/attempts/{attempt_id}/submit",
        headers=headers,
        files={"file": ("Word_1-2_results.docx", results.read_bytes(), WORD_MIME)},
        data={"evidence": json.dumps({"events": DEMO_W11})},
    )
    assert submitted.status_code == 200
    assert submitted.json()["score"]["verified"] == 100
    mine = client.get("/api/v1/progress", headers=headers).json()
    row = next(e for e in mine["exercises"] if e["project_id"] == "word-objective-1-2")
    assert row["status"] == "mastered"
    assert float(row["best_verified"]) == 100
