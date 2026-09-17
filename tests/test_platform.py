"""PostgreSQL platform: JWT, projects, telemetry, OpenXML scoring, admin."""
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from app.db import connect, init_schema
from app.main import app
from app.scoring import extract_text, score_file
from app.seed import seed

STATIC = Path(__file__).resolve().parent.parent / "app" / "static"
DOCX = STATIC / "mau-van-ban.docx"


def postgres_ready() -> bool:
    try:
        with connect() as conn:
            conn.execute("SELECT 1")
        return True
    except Exception:
        return False


@pytest.fixture(scope="module")
def pg():
    if not postgres_ready():
        pytest.skip("PostgreSQL chưa sẵn sàng (DATABASE_URL)")
    init_schema()
    seed()


@pytest.fixture
def client(pg):
    return TestClient(app)


def _token(client: TestClient, username: str = "hocsinh") -> str:
    r = client.post(
        "/api/v1/auth/login",
        json={"username": username, "password": "Mos@Gds2026", "chuong_trinh": "word"},
    )
    assert r.status_code == 200, r.text
    body = r.json()
    assert body["ok"] is True
    assert body["token"]
    return body["token"]


def test_healthz_reports_postgres(client):
    r = client.get("/healthz")
    assert r.status_code == 200
    assert r.json()["postgres"] is True


def test_jwt_login_and_me(client):
    token = _token(client, "giaovien")
    denied = client.get("/api/v1/me")
    assert denied.status_code == 401
    me = client.get("/api/v1/me", headers={"Authorization": f"Bearer {token}"})
    assert me.status_code == 200
    user = me.json()["user"]
    assert user["username"] == "giaovien"
    assert user["role"] == "teacher"


def test_projects_download_attempt_telemetry_submit(client):
    token = _token(client, "hocsinh")
    headers = {"Authorization": f"Bearer {token}"}
    projects = client.get("/api/v1/projects", headers=headers, params={"program": "word"}).json()
    assert projects["ok"] is True
    ids = [p["id"] for p in projects["projects"]]
    assert "word-mail-merge" in ids
    blob = client.get("/api/v1/projects/word-mail-merge/file", headers=headers)
    assert blob.status_code == 200
    assert blob.content[:2] == b"PK"
    started = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-mail-merge", "mode": "testing"},
    )
    assert started.status_code == 200
    attempt_id = started.json()["attempt_id"]
    tel = client.post(
        f"/api/v1/attempts/{attempt_id}/telemetry",
        headers=headers,
        json={"events": [{"skill": "Mail Merge", "action": "open", "detail": {"app": "word"}}]},
    )
    assert tel.status_code == 200
    assert tel.json()["accepted"] == 1
    submitted = client.post(
        f"/api/v1/attempts/{attempt_id}/submit",
        headers=headers,
        files={"file": ("mau-van-ban.docx", DOCX.read_bytes(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document")},
    )
    assert submitted.status_code == 200
    score = submitted.json()["score"]
    assert score["score"] >= 0
    assert score["max_score"] == 100
    history = client.get("/api/v1/attempts", headers=headers).json()
    assert any(a["id"] == attempt_id and a["status"] == "submitted" for a in history["attempts"])


def test_admin_dashboard_staff_only(client):
    student = TestClient(app)
    student.post("/dang-nhap", data={"username": "hocsinh", "password": "Mos@Gds2026"})
    denied = student.get("/quan-tri", follow_redirects=False)
    assert denied.status_code == 303
    teacher = TestClient(app)
    teacher.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"})
    page = teacher.get("/quan-tri")
    assert page.status_code == 200
    assert "Bảng điều khiển MOS-KulKul" in page.text
    assert "10A1" in page.text
    assert "Học sinh" in page.text


def test_openxml_scoring_reads_sample_docx(pg):
    text = extract_text(DOCX)
    assert len(text) > 8
    result = score_file(DOCX, {"contains_text": ["MOS"], "min_chars": 10})
    assert result["score"] > 0
    assert result["checks"]
