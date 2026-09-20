"""Role-split menus: Super Admin, teacher, student — hide, do not disable."""
from __future__ import annotations

import pytest
from fastapi.testclient import TestClient

from app.db import init_schema
from app.main import app
from app.seed import seed
from tests.test_platform import postgres_ready


@pytest.fixture(scope="module")
def pg():
    if not postgres_ready():
        pytest.skip("PostgreSQL chưa sẵn sàng (DATABASE_URL)")
    init_schema()
    seed()


def test_teacher_menu_hides_system_tools(pg):
    teacher = TestClient(app)
    teacher.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"})
    home = teacher.get("/quan-tri")
    assert home.status_code == 200
    assert "Danh sách Học sinh" in home.text
    assert "Giao bài tập" in home.text
    assert "Giám sát phòng thi" in home.text
    assert "Sổ điểm" in home.text
    assert "Lỗ hổng kiến thức" in home.text
    assert "Cấu hình hệ thống" not in home.text
    assert "LTI 1.3" not in home.text
    assert "Chất lượng sư phạm" not in home.text
    assert "Ngân hàng đề" not in home.text
    assert "Tiến độ của tôi" not in home.text
    lti = teacher.get("/quan-tri/lti", follow_redirects=False)
    assert lti.status_code == 303
    bank = teacher.get("/quan-tri/ngan-hang", follow_redirects=False)
    assert bank.status_code == 303
    assert bank.headers["location"] == "/quan-tri/bai-tap"
    grades = teacher.get("/quan-tri/so-diem")
    assert grades.status_code == 200
    assert "Sổ điểm" in grades.text


def test_student_portal_has_no_staff_chrome(pg):
    student = TestClient(app)
    student.post("/dang-nhap", data={"username": "hocsinh", "password": "Mos@Gds2026"})
    blocked = student.get("/quan-tri", follow_redirects=False)
    assert blocked.status_code == 303
    page = student.get("/tien-do")
    assert page.status_code == 200
    assert "Tiến độ của tôi" in page.text
    assert "student-top" in page.text
    assert "ic-app-header" not in page.text
    assert "Command Center" not in page.text
    assert "Super Admin" not in page.text
    tasks = student.get("/tien-do/nhiem-vu")
    assert tasks.status_code == 200
    assert "Nhiệm vụ cần làm" in tasks.text
    history = student.get("/tien-do/lich-su")
    assert history.status_code == 200
    assert "Lịch sử bài nộp" in history.text


def test_admin_keeps_macro_tools(pg):
    admin = TestClient(app)
    admin.post("/dang-nhap", data={"username": "admin", "password": "Mos@Gds2026"})
    page = admin.get("/quan-tri")
    assert "Super Admin" in page.text
    assert "Tổng quan trường" in page.text
    assert "MOS-KulKul đang mở" in page.text
    assert "Cấu hình hệ thống" in page.text
    lti = admin.get("/quan-tri/lti")
    assert lti.status_code == 200
