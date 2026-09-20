"""Tabbed Học sinh admin: search, import template, button hierarchy."""
from __future__ import annotations

import secrets

import pytest
from fastapi.testclient import TestClient

from app.accounts import create_account
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


@pytest.fixture
def admin(pg):
    client = TestClient(app)
    client.post("/dang-nhap", data={"username": "admin", "password": "Mos@Gds2026"})
    return client


def test_students_page_uses_tabs_and_plain_language(admin):
    page = admin.get("/quan-tri/hoc-sinh")
    assert page.status_code == 200
    assert "Danh sách Học sinh" in page.text
    assert "Nhập liệu hàng loạt" in page.text
    assert "Quản lý Phòng máy" in page.text
    assert "One-click import CSV" in page.text
    assert "Tạo tài khoản" in page.text
    assert "Lưu thông tin" in page.text
    assert "Tải File Mẫu (Template)" in page.text
    assert "Xuất báo cáo" in page.text
    assert "btn-danger-outline" in page.text
    assert "Lưu vào PostgreSQL" not in page.text
    assert "PostgreSQL" not in page.text
    assert 'name="q"' in page.text
    assert "Reset mật khẩu lớp" in page.text
    assert "data-confirm" in page.text


def test_students_search_and_status_filter(admin):
    found = admin.get("/quan-tri/hoc-sinh", params={"q": "HS001"})
    assert found.status_code == 200
    assert "HS001" in found.text
    assert "hocsinh2" not in found.text
    started = admin.get("/quan-tri/hoc-sinh", params={"muc": "chua_bat_dau"})
    assert started.status_code == 200
    assert "Chưa bắt đầu" in started.text


def test_import_template_and_report(admin):
    template = admin.get("/quan-tri/hoc-sinh/mau.csv")
    assert template.status_code == 200
    assert "Họ tên" in template.text
    assert "Mã HS" in template.text
    report = admin.get("/quan-tri/hoc-sinh/bao-cao.csv", params={"q": "HS001"})
    assert report.status_code == 200
    assert "HS001" in report.text
    assert "hocsinh2" not in report.text


def test_manual_create_and_row_actions(admin):
    tag = secrets.token_hex(3)
    created = admin.post(
        "/quan-tri/hoc-sinh",
        data={
            "username": f"tab-{tag}",
            "name": f"Học sinh tab {tag}",
            "password": "Mos@Gds2026",
            "student_code": f"TAB{tag[:4]}",
            "class_id": "",
            "role": "student",
        },
        follow_redirects=False,
    )
    assert created.status_code == 303
    assert "tao=1" in created.headers["location"]
    page = admin.get("/quan-tri/hoc-sinh", params={"q": tag})
    assert f"Học sinh tab {tag}" in page.text
    assert "Đổi mật khẩu cá nhân" in page.text
    assert "Sửa thông tin" in page.text

    from app.db import cursor

    with cursor() as cur:
        cur.execute("SELECT id FROM users WHERE username = %s", (f"tab-{tag}",))
        uid = cur.fetchone()["id"]
    reset = admin.post(f"/quan-tri/hoc-sinh/{uid}/mat-khau", follow_redirects=False)
    assert reset.status_code == 303
    assert "reset=1" in reset.headers["location"]
    edited = admin.post(
        f"/quan-tri/hoc-sinh/{uid}/sua",
        data={"name": f"Đã sửa {tag}", "student_code": f"TAB{tag[:4]}", "class_id": "", "password": ""},
        follow_redirects=False,
    )
    assert edited.status_code == 303
    assert "sua=1" in edited.headers["location"]
    removed = admin.post(f"/quan-tri/hoc-sinh/{uid}/xoa", follow_redirects=False)
    assert removed.status_code == 303
    assert "xoa=" in removed.headers["location"]


def test_teacher_still_sees_class_roster(pg):
    teacher = TestClient(app)
    teacher.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"})
    page = teacher.get("/quan-tri/hoc-sinh")
    assert page.status_code == 200
    assert "HS001" in page.text
    assert "10A1" in page.text
    guest = create_account(
        username=f"other-{secrets.token_hex(3)}",
        name="Học sinh lớp khác",
        password="Mos@Gds2026",
        role="student",
    )
    hidden = teacher.get("/quan-tri/hoc-sinh", params={"q": guest["username"]})
    assert f">{guest['name']}</a>" not in hidden.text
    assert "Không có học sinh khớp bộ lọc." in hidden.text
