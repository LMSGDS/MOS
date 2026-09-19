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
    body = r.json()
    assert body["postgres"] is True
    assert body["ok"] is True
    assert body["transport"] == "https"
    assert body["git"]["sha"]
    assert body["git"]["ref"]
    assert isinstance(body["installers"], list)
    assert "version" in body


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
    assert "word-objective-1-1" in ids
    assert "word-objective-1-2" in ids
    assert "word-objective-1-3" in ids
    assert "word-objective-1-4" in ids
    assert "word-objective-2-1" in ids
    assert "word-objective-3-1" in ids
    assert "word-objective-4-2a" in ids
    assert "word-objective-5-1" in ids
    assert "word-objective-6-2" in ids
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
    hit = next(a for a in history["attempts"] if a["id"] == attempt_id)
    assert hit["status"] == "submitted"
    assert hit["program"] == "word"


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
    assert "Bằng chứng thao tác" in page.text


def test_openxml_scoring_reads_sample_docx(pg):
    text = extract_text(DOCX)
    assert len(text) > 8
    result = score_file(DOCX, {"contains_text": ["MOS"], "min_chars": 10})
    assert result["score"] > 0
    assert result["checks"]


WORD11 = Path(__file__).resolve().parent / "fixtures" / "word-objective-1-1" / "Word_1-1.docx"
WORD11_RESULTS = Path(__file__).resolve().parent / "fixtures" / "word-objective-1-1" / "Word_1-1_results.docx"
RESULTS_DIR = Path(__file__).resolve().parent.parent / "data" / "results"
WORD_MIME = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"


def test_word_11_manifest_checkpoint_and_submit(client):
    token = _token(client, "hocsinh")
    headers = {"Authorization": f"Bearer {token}"}
    manifest = client.get("/api/v1/projects/word-objective-1-1/manifest", headers=headers).json()
    assert manifest["ok"] is True
    assert manifest["sha256"] == "c29dec782138d02025f4f6d915c71bf9cb8e44a8c0cce60cb210ba0d29a753c8"
    assert manifest["rubric_version"] == "1.0.0"
    assert len(manifest["criteria"]) == 16
    ids = [c["id"] for c in manifest["criteria"]]
    assert "W11-B01" in ids
    assert all(len(c.get("help_steps") or []) >= 2 for c in manifest["criteria"])
    starter = client.get("/api/v1/projects/word-objective-1-1/file", headers=headers)
    assert starter.status_code == 200
    assert starter.content[:2] == b"PK"
    keyed = client.get("/api/v1/projects/word-objective-1-1/file", headers=headers, params={"kind": "results"})
    assert keyed.status_code == 200
    assert keyed.content[:2] == b"PK"
    assert keyed.content != starter.content
    six = client.get("/api/v1/projects/word-objective-6-2/file", headers=headers, params={"kind": "results"})
    assert six.status_code == 200
    assert six.content[:2] == b"PK"
    denied = client.get("/api/v1/projects/word-objective-6-2/file", params={"kind": "results"})
    assert denied.status_code == 401

    started = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    attempt_id = started.json()["attempt_id"]
    check = client.post(
        f"/api/v1/attempts/{attempt_id}/checkpoints",
        headers=headers,
        files={"file": ("Word_1-1.docx", WORD11.read_bytes(), WORD_MIME)},
    )
    assert check.status_code == 200
    starter_score = check.json()["score"]
    assert starter_score["verified"] == 0
    assert starter_score["pending"] == 38

    submitted = client.post(
        f"/api/v1/attempts/{attempt_id}/submit",
        headers=headers,
        files={"file": ("Word_1-1_results.docx", WORD11_RESULTS.read_bytes(), WORD_MIME)},
    )
    assert submitted.status_code == 200
    score = submitted.json()["score"]
    assert score["verified"] == 62
    assert score["pending"] == 38
    assert score["score"] == 62
    assert score["complete"] is False
    statuses = {c["criterion_id"]: c["status"] for c in score["criteria"]}
    assert statuses["W11-B01"] == "pass"
    assert statuses["W11-H01"] == "pass"
    assert statuses["W11-S01"] == "unverified"
    submission_id = submitted.json()["submission_id"]
    first_key = client.post(
        f"/api/v1/attempts/{attempt_id}/submissions",
        headers={**headers, "Idempotency-Key": "same-key"},
        files={"file": ("Word_1-1_results.docx", WORD11_RESULTS.read_bytes(), WORD_MIME)},
    )
    assert first_key.status_code == 200
    replayed = client.post(
        f"/api/v1/attempts/{attempt_id}/submissions",
        headers={**headers, "Idempotency-Key": "same-key"},
        files={"file": ("Word_1-1.docx", WORD11.read_bytes(), WORD_MIME)},
    )
    assert replayed.json()["replayed"] is True
    assert replayed.json()["submission_id"] == first_key.json()["submission_id"]
    got = client.get(f"/api/v1/submissions/{submission_id}", headers=headers)
    assert got.status_code == 200
    testing = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-objective-1-1", "mode": "testing"},
    )
    hidden = client.post(
        f"/api/v1/attempts/{testing.json()['attempt_id']}/checkpoints",
        headers=headers,
        files={"file": ("Word_1-1_results.docx", WORD11_RESULTS.read_bytes(), WORD_MIME)},
    )
    assert hidden.status_code == 200
    assert "score" not in hidden.json()
    assert hidden.json()["saved"] is True


def test_cannot_write_another_students_attempt(client):
    from app.db import cursor

    token_a = _token(client, "hocsinh")
    headers_a = {"Authorization": f"Bearer {token_a}"}
    started = client.post(
        "/api/v1/attempts",
        headers=headers_a,
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    attempt_id = started.json()["attempt_id"]
    with cursor() as cur:
        cur.execute("SELECT password_hash FROM users WHERE username = %s", ("hocsinh",))
        pw = cur.fetchone()["password_hash"]
        cur.execute(
            """
            INSERT INTO users (username, name, role, password_hash, org_id)
            VALUES ('hocsinh2', 'Học sinh 2', 'student', %s, 1)
            ON CONFLICT (username) DO UPDATE SET password_hash = EXCLUDED.password_hash
            """,
            (pw,),
        )
    token_b = _token(client, "hocsinh2")
    headers_b = {"Authorization": f"Bearer {token_b}"}
    dest = RESULTS_DIR / attempt_id
    before = set(dest.glob("*")) if dest.exists() else set()
    tel = client.post(
        f"/api/v1/attempts/{attempt_id}/telemetry",
        headers=headers_b,
        json={"events": [{"event_id": "x1", "action": "spy"}]},
    )
    assert tel.status_code == 403
    submitted = client.post(
        f"/api/v1/attempts/{attempt_id}/submit",
        headers=headers_b,
        files={"file": ("Word_1-1.docx", WORD11.read_bytes(), WORD_MIME)},
    )
    assert submitted.status_code == 403
    after = set(dest.glob("*")) if dest.exists() else set()
    assert after == before
    dup = client.post(
        f"/api/v1/attempts/{attempt_id}/telemetry",
        headers=headers_a,
        json={"events": [{"event_id": "same-evt", "action": "open"}, {"event_id": "same-evt", "action": "open"}]},
    )
    assert dup.status_code == 200
    assert dup.json()["accepted"] == 1


def test_word_11_checkpoint_with_demo_evidence(client):
    import json

    from tests.test_word_actions import DEMO_W11

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
        data={"evidence": json.dumps({"events": DEMO_W11})},
    )
    assert check.status_code == 200
    score = check.json()["score"]
    assert score["verified"] == 100
    assert score["pending"] == 0
    assert score["complete"] is True
    statuses = {c["criterion_id"]: c["status"] for c in score["criteria"]}
    assert statuses["W11-S01"] == "pass"
    assert statuses["W11-N03"] == "pass"
    assert check.json()["evidence_stored"] >= 8

    stored = client.get(f"/api/v1/attempts/{attempt_id}/evidence", headers=headers)
    assert stored.status_code == 200
    body = stored.json()
    assert body["count"] >= 8
    actions = {e["action"] for e in body["events"]}
    assert "find" in actions
    assert "goto_bookmark" in actions
    assert body["verified_score"] == 100
    assert body["pending_score"] == 0
    listed = client.get("/api/v1/attempts", headers=headers).json()
    hit = next(a for a in listed["attempts"] if a["id"] == attempt_id)
    assert hit["verified_score"] == 100
    assert hit["pending_score"] == 0

    teacher = _token(client, "giaovien")
    admin = client.get(
        f"/api/v1/attempts/{attempt_id}/evidence",
        headers={"Authorization": f"Bearer {teacher}"},
    )
    assert admin.status_code == 200
    assert admin.json()["count"] >= 8


def test_post_evidence_json_is_stored(client):
    token = _token(client, "hocsinh")
    headers = {"Authorization": f"Bearer {token}"}
    started = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    attempt_id = started.json()["attempt_id"]
    posted = client.post(
        f"/api/v1/attempts/{attempt_id}/evidence",
        headers=headers,
        json={"events": [{"action": "find", "query": "to", "source": "navigation_pane", "event_id": "ev-to-1"}]},
    )
    assert posted.status_code == 200
    assert posted.json()["count"] >= 1
    again = client.post(
        f"/api/v1/attempts/{attempt_id}/evidence",
        headers=headers,
        json={"events": [{"action": "find", "query": "to", "source": "navigation_pane", "event_id": "ev-to-1"}]},
    )
    assert again.json()["stored"] == 0
    got = client.get(f"/api/v1/attempts/{attempt_id}/evidence", headers=headers).json()
    assert any(e.get("query") == "to" for e in got["events"])
    assert posted.json().get("regraded") is False


def test_post_evidence_regrades_existing_checkpoint(client):
    import json

    from tests.test_word_actions import DEMO_W11

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
    assert check.json()["score"]["verified"] == 62
    assert check.json()["score"]["pending"] == 38

    posted = client.post(
        f"/api/v1/attempts/{attempt_id}/evidence",
        headers=headers,
        json={"events": DEMO_W11},
    )
    assert posted.status_code == 200, posted.text
    body = posted.json()
    assert body["regraded"] is True
    assert body["score"]["verified"] == 100
    assert body["score"]["pending"] == 0
    assert body["score"]["complete"] is True
    assert body["score"]["grader_version"] == "1.4.0"
    statuses = {c["criterion_id"]: c["status"] for c in body["score"]["criteria"]}
    assert statuses["W11-S01"] == "pass"
    assert statuses["W11-N02"] == "pass"
    assert statuses["W11-H03"] == "pass"

    stored = client.get(f"/api/v1/attempts/{attempt_id}/evidence", headers=headers)
    assert stored.status_code == 200
    assert stored.json()["verified_score"] == 100
    assert stored.json()["pending_score"] == 0


def test_late_evidence_regrades_submitted_attempt(client):
    import json

    from tests.test_word_actions import DEMO_W11

    token = _token(client, "hocsinh")
    headers = {"Authorization": f"Bearer {token}"}
    started = client.post(
        "/api/v1/attempts",
        headers=headers,
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    attempt_id = started.json()["attempt_id"]
    submitted = client.post(
        f"/api/v1/attempts/{attempt_id}/submit",
        headers=headers,
        files={"file": ("Word_1-1_results.docx", WORD11_RESULTS.read_bytes(), WORD_MIME)},
    )
    assert submitted.status_code == 200
    assert submitted.json()["score"]["verified"] == 62
    assert submitted.json()["score"]["pending"] == 38
    submission_id = submitted.json()["submission_id"]

    posted = client.post(
        f"/api/v1/attempts/{attempt_id}/evidence",
        headers=headers,
        json={"events": DEMO_W11},
    )
    assert posted.status_code == 200, posted.text
    assert posted.json()["regraded"] is True
    assert posted.json()["score"]["verified"] == 100
    assert posted.json()["score"]["pending"] == 0

    listed = client.get("/api/v1/attempts", headers=headers).json()
    hit = next(a for a in listed["attempts"] if a["id"] == attempt_id)
    assert hit["verified_score"] == 100
    assert hit["pending_score"] == 0
    got = client.get(f"/api/v1/submissions/{submission_id}", headers=headers)
    assert got.status_code == 200
    payload = got.json()["submission"]["payload"]
    if isinstance(payload, str):
        payload = json.loads(payload)
    assert payload["verified"] == 100
    assert payload["pending"] == 0
