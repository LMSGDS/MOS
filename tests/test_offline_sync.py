"""Offline-first sync: LWW, một phiên, hash artifact, Q-Matrix, SSE giáo viên."""
from __future__ import annotations

import hashlib

import pytest
from fastapi.testclient import TestClient

from app.db import cursor, init_schema
from app.main import app
from app.seed import seed
from app.sync import parse_ts, progress_from_score, q_axis, verify_artifact_sha256
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


def test_verify_artifact_sha256_and_q_axis():
    data = b"mos-kulkul"
    digest = hashlib.sha256(data).hexdigest()
    assert verify_artifact_sha256(data, digest) == digest
    assert verify_artifact_sha256(data, None) == digest
    with pytest.raises(ValueError):
        verify_artifact_sha256(data, "0" * 64)
    assert progress_from_score({"verified": 40, "max_score": 100}) == 40
    assert q_axis({"status": "pass"}) == ("pass", "pass", "pass")
    assert q_axis({"status": "fail", "reason_code": "bookmark_missing"}) == ("fail", "", "")
    assert q_axis({"status": "fail", "reason_code": "value_mismatch"}) == ("pass", "pass", "fail")
    assert parse_ts("2026-09-19T10:00:00Z") is not None


def test_nginx_live_upgrade_and_no_buffer():
    from pathlib import Path

    text = (Path(__file__).resolve().parent.parent / "deploy" / "nginx-mos.gds.edu.vn.conf").read_text(encoding="utf-8")
    assert "Upgrade $http_upgrade" in text
    assert "proxy_buffering off" in text
    assert "/api/v1/classes/" in text


def test_start_abandons_other_running_sessions(client):
    token = _token(client, "hocsinh")
    headers = {"Authorization": f"Bearer {token}"}
    first = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    assert first.status_code == 200
    old_id = first.json()["attempt_id"]
    second = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    assert second.status_code == 200
    assert second.json()["abandoned"] >= 1
    new_id = second.json()["attempt_id"]
    sessions = client.get("/api/v1/sessions", headers=headers).json()["sessions"]
    by_id = {s["session_id"]: s for s in sessions}
    assert by_id[old_id]["status"] == "ABANDONED"
    assert by_id[new_id]["status"] == "IN_PROGRESS"


def test_checkpoint_last_write_wins(client):
    token = _token(client, "hocsinh")
    headers = {"Authorization": f"Bearer {token}"}
    started = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    attempt_id = started.json()["attempt_id"]
    data = WORD11_RESULTS.read_bytes()
    newer = client.post(
        f"/api/v1/attempts/{attempt_id}/checkpoints",
        headers={**headers, "X-MOS-Updated-At": "2026-09-19T12:00:00+00:00"},
        files={"file": ("Word_1-1_results.docx", data, WORD_MIME)},
    )
    assert newer.status_code == 200
    assert newer.json().get("stale") is not True
    older = client.post(
        f"/api/v1/attempts/{attempt_id}/checkpoints",
        headers={**headers, "X-MOS-Updated-At": "2020-01-01T00:00:00+00:00"},
        files={"file": ("Word_1-1_results.docx", data, WORD_MIME)},
    )
    assert older.status_code == 200
    body = older.json()
    assert body["stale"] is True
    assert body["checkpoint"] is False


def test_state_does_not_accept_client_score(client):
    token = _token(client, "hocsinh")
    headers = {"Authorization": f"Bearer {token}"}
    started = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    attempt_id = started.json()["attempt_id"]
    pushed = client.post(
        f"/api/v1/attempts/{attempt_id}/state",
        headers={**headers, "X-MOS-Updated-At": "2026-09-19T13:00:00+00:00"},
        json={
            "progress_pct": 37,
            "local_grade": {"verified": 100, "score": 100, "max_score": 100},
            "events": [{"action": "save", "skill": "word", "detail": {"offline": True}}],
        },
    )
    assert pushed.status_code == 200
    assert pushed.json()["accepted"] is True
    assert pushed.json()["progress_pct"] == 37
    listed = client.get("/api/v1/attempts", headers=headers).json()["attempts"]
    row = next(a for a in listed if a["id"] == attempt_id)
    assert row["progress_pct"] == 37
    assert row["score"] is None
    assert row["verified_score"] is None


def test_submit_rejects_bad_artifact_hash_and_stores_q_matrix(client):
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
    bad = client.post(
        f"/api/v1/attempts/{attempt_id}/submit",
        headers={**headers, "X-MOS-Artifact-SHA256": "ab" * 32},
        files={"file": ("Word_1-1_results.docx", data, WORD_MIME)},
    )
    assert bad.status_code == 400
    assert bad.json()["detail"] == "artifact_sha256"
    good = client.post(
        f"/api/v1/attempts/{attempt_id}/submit",
        headers={**headers, "X-MOS-Artifact-SHA256": digest},
        files={"file": ("Word_1-1_results.docx", data, WORD_MIME)},
    )
    assert good.status_code == 200
    assert "score" in good.json()
    with cursor() as cur:
        cur.execute("SELECT COUNT(*) AS n FROM q_matrix_results WHERE attempt_id = %s", (attempt_id,))
        assert cur.fetchone()["n"] >= 1
        cur.execute("SELECT status FROM exam_sessions WHERE session_id = %s", (attempt_id,))
        assert cur.fetchone()["status"] == "SUBMITTED"


def test_teacher_live_sse_and_monitor_page(client):
    student = _token(client, "hocsinh")
    teacher = _token(client, "giaovien")
    denied = client.get("/api/v1/classes/1/live")
    assert denied.status_code == 401
    student_live = client.get(
        "/api/v1/classes/1/live",
        headers={"Authorization": f"Bearer {student}"},
    )
    assert student_live.status_code == 403
    with client.stream(
        "GET",
        "/api/v1/classes/0/live",
        headers={"Authorization": f"Bearer {teacher}"},
    ) as resp:
        assert resp.status_code == 200
        assert "text/event-stream" in resp.headers.get("content-type", "")
        first = next(resp.iter_lines())
        assert "hello" in first or "ok" in first
    sessions = client.get(
        "/api/v1/classes/1/sessions",
        headers={"Authorization": f"Bearer {teacher}"},
    )
    assert sessions.status_code == 200
    assert sessions.json()["ok"] is True
    web = TestClient(app)
    web.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"})
    page = web.get("/quan-tri/giam-sat")
    assert page.status_code == 200
    assert "Giám sát phòng thi" in page.text
    assert "/api/v1/classes/" in page.text
    students = web.get("/quan-tri")
    assert "Giám sát phòng thi" in students.text
