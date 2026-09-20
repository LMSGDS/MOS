"""Super Admin subsystems from the school design doc."""
from __future__ import annotations

import pytest
from fastapi.testclient import TestClient

from app.db import cursor, init_schema
from app.main import app
from app.seed import seed
from app.superadmin import adoption_series, get_setting, grade_radar, host_load, roster_matrix, save_matrix
from tests.test_platform import postgres_ready


@pytest.fixture(scope="module")
def pg():
    if not postgres_ready():
        pytest.skip("PostgreSQL chưa sẵn sàng (DATABASE_URL)")
    init_schema()
    seed()


def test_macro_dashboard_and_hierarchy(pg):
    admin = TestClient(app)
    admin.post("/dang-nhap", data={"username": "admin", "password": "Mos@Gds2026"})
    home = admin.get("/quan-tri")
    assert home.status_code == 200
    assert "Đang online" in home.text
    assert "Tải máy chủ" in home.text
    assert "Radar toàn trường" in home.text
    assert "Phủ sóng luyện tập" in home.text
    assert adoption_series()["month_n"] >= 0
    assert "10" in grade_radar()
    assert host_load()["cpu_pct"] >= 0

    hub = admin.get("/quan-tri/phan-cap")
    assert hub.status_code == 200
    assert "Ma trận phân công" in hub.text
    assert "Force Reset Password" in hub.text
    matrix = admin.get("/quan-tri/phan-cap", params={"tab": "ma-tran"})
    assert "Lưu ma trận" in matrix.text
    data = roster_matrix()
    assert data["teachers"]
    pairs = [(tid, cid) for cid, tid in data["assigned"].items() if tid]
    if not pairs:
        pairs = [(data["teachers"][0]["id"], data["classes"][0]["id"])]
    saved = save_matrix(pairs)
    assert saved == len({cid for _tid, cid in pairs})

    students = admin.get("/quan-tri/phan-cap", params={"tab": "hoc-sinh"})
    assert "Học sinh toàn trường" in students.text
    assert "Đình chỉ" in students.text

    teacher = TestClient(app)
    teacher.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"})
    blocked = teacher.get("/quan-tri/phan-cap", follow_redirects=False)
    assert blocked.status_code == 303


def test_soft_lock_and_transfer(pg):
    admin = TestClient(app)
    admin.post("/dang-nhap", data={"username": "admin", "password": "Mos@Gds2026"})
    with cursor() as cur:
        cur.execute("SELECT id FROM users WHERE username = %s", ("hocsinh3",))
        student = cur.fetchone()
        cur.execute("SELECT id FROM classes WHERE name = %s", ("11A1",))
        klass = cur.fetchone()
        if not klass:
            cur.execute("INSERT INTO classes (name, org_id) VALUES ('11A1', 1) RETURNING id")
            klass = cur.fetchone()
    assert student and klass
    moved = admin.post(
        "/quan-tri/phan-cap/chuyen-lop",
        data={"user_id": str(student["id"]), "class_id": str(klass["id"])},
        follow_redirects=False,
    )
    assert moved.status_code == 303
    locked = admin.post(
        "/quan-tri/phan-cap/trang-thai",
        data={"user_id": str(student["id"]), "active": "0", "tab": "hoc-sinh"},
        follow_redirects=False,
    )
    assert locked.status_code == 303
    denied = TestClient(app)
    fail = denied.post("/dang-nhap", data={"username": "hocsinh3", "password": "Mos@Gds2026"}, follow_redirects=False)
    assert fail.status_code == 303
    assert "loi=sai" in fail.headers.get("location", "")
    admin.post(
        "/quan-tri/phan-cap/trang-thai",
        data={"user_id": str(student["id"]), "active": "1", "tab": "hoc-sinh"},
    )


def test_bank_pedagogy_and_system_copy(pg):
    admin = TestClient(app)
    admin.post("/dang-nhap", data={"username": "admin", "password": "Mos@Gds2026"})
    bank = admin.get("/quan-tri/ngan-hang")
    assert "Ngân hàng đề" in bank.text
    assert "Q-Matrix" in bank.text
    ped = admin.get("/quan-tri/su-pham")
    assert "Đối sánh hai lớp" in ped.text
    assert "IRT-lite" in ped.text
    sys_page = admin.get("/quan-tri/lti")
    assert "Tường lửa phòng máy" in sys_page.text
    assert "Client MOS-KulKul" in sys_page.text
    saved = admin.post("/quan-tri/lti/ip", data={"exam_ip_allow": "10.0.0.0/8"}, follow_redirects=False)
    assert saved.status_code == 303
    assert get_setting("exam_ip_allow") == "10.0.0.0/8"
