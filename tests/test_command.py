"""Command Center: import roster, join code, LAN lock, adaptive unlock, live colors."""
from __future__ import annotations

from datetime import datetime, timedelta, timezone

import secrets

import pytest
from fastapi.testclient import TestClient

from app.accounts import create_account
from app.assign import apply_adaptive, configure_assignment, ip_allowed
from app.db import cursor, init_schema
from app.insights import annotate_sessions, class_radar
from app.main import app
from app.roster import bulk_reset, ensure_join_code, import_csv, join_by_code
from app.seed import seed
from tests.test_platform import _token, postgres_ready


@pytest.fixture(scope="module")
def pg():
    if not postgres_ready():
        pytest.skip("PostgreSQL chưa sẵn sàng (DATABASE_URL)")
    init_schema()
    seed()


@pytest.fixture
def client(pg):
    return TestClient(app)


def test_csv_import_join_and_bulk_reset(client):
    with cursor() as cur:
        cur.execute("INSERT INTO classes (name, org_id) VALUES ('ImportLab', 1) RETURNING id")
        cid = cur.fetchone()["id"]
    tag = secrets.token_hex(3)
    csv_text = f"Họ tên,Mã HS\nNguyễn Văn A,Z{tag}1\nTrần Thị B,Z{tag}2\n"
    result = import_csv(csv_text, class_id=cid)
    assert result["created"] >= 2
    code = ensure_join_code(cid)
    assert len(code) >= 4
    guest = create_account(
        username=f"join-{tag}",
        name="Khách join",
        password="Mos@Gds2026",
        role="student",
    )
    joined = join_by_code(guest["id"], code)
    assert joined["class_id"] == cid
    creds = bulk_reset(cid)
    assert len(creds) >= 3
    assert any(c["username"].startswith("z") for c in creds)
    admin = TestClient(app)
    admin.post("/dang-nhap", data={"username": "admin", "password": "Mos@Gds2026"})
    page = admin.get("/quan-tri/hoc-sinh")
    assert page.status_code == 200
    assert "One-click import CSV" in page.text
    assert "Join code" in page.text or "join" in page.text.lower()
    engine = admin.get("/quan-tri/bai-tap")
    assert "Cỗ máy giao bài" in engine.text
    assert "Chỉ mạng LAN" in engine.text


def test_lan_lock_and_adaptive_rule(client):
    assert ip_allowed("192.168.1.20", "192.168.0.0/16") is True
    assert ip_allowed("8.8.8.8", "192.168.0.0/16") is False
    configure_assignment(
        1,
        "word-objective-1-1",
        mode="testing",
        ip_allow="10.0.0.0/8",
        unlock_below=50,
        unlock_project_id="word-objective-1-2",
    )
    token = _token(client, "hocsinh")
    blocked = client.post(
        "/api/v1/attempts",
        headers={"Authorization": f"Bearer {token}", "X-Forwarded-For": "8.8.8.8"},
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    assert blocked.status_code == 403
    opened = client.post(
        "/api/v1/attempts",
        headers={"Authorization": f"Bearer {token}", "X-Forwarded-For": "10.10.28.40"},
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    assert opened.status_code == 200
    assert opened.json()["mode"] == "testing"
    with cursor() as cur:
        cur.execute("SELECT id FROM users WHERE username = %s", ("hocsinh",))
        uid = cur.fetchone()["id"]
    unlocked = apply_adaptive(uid, "word-objective-1-1", 20)
    assert "word-objective-1-2" in unlocked
    configure_assignment(1, "word-objective-1-1", mode="training", ip_allow="")


def test_live_colors_and_class_radar():
    now = datetime.now(timezone.utc)
    idle = annotate_sessions(
        [{"session_id": "i1", "status": "IN_PROGRESS", "updated_at": now - timedelta(minutes=4), "student": "A"}]
    )
    assert idle[0]["color"] == "yellow"
    stuck = annotate_sessions(
        [{"session_id": "s1", "status": "IN_PROGRESS", "updated_at": now - timedelta(minutes=6), "student": "B"}]
    )
    assert stuck[0]["color"] == "red"
    assert any(a["code"] == "stuck" for a in stuck[0]["alerts"])
    ok = annotate_sessions(
        [{"session_id": "g1", "status": "IN_PROGRESS", "updated_at": now - timedelta(seconds=20), "student": "C"}]
    )
    assert ok[0]["color"] == "green"
    assert class_radar(1)
    teacher = TestClient(app)
    teacher.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"})
    gaps = teacher.get("/quan-tri/lo-hong")
    assert gaps.status_code == 200
    assert "Radar lớp" in gaps.text
