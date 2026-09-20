"""Ngân hàng đề 3 tầng: cây MOS 2019, assembler 5–7/25–35, Q-Matrix, RBAC."""
from __future__ import annotations

import pytest
from fastapi.testclient import TestClient

from app.bank import (
    CUT_SCORE,
    SCALE_MAX,
    auto_generate_exam,
    compile_payload,
    exam_counts,
    knowledge_tree,
    list_exams,
    list_tasks,
    pack_exam_projects,
    passed,
    save_exam,
    save_task,
    scale_score,
    seed_bank,
    session_for_project,
    set_exam_status,
    set_task_status,
)
from app.db import cursor, init_schema
from app.main import app
from app.seed import seed
from tests.test_platform import _token, postgres_ready


@pytest.fixture(scope="module")
def pg():
    if not postgres_ready():
        pytest.skip("PostgreSQL chưa sẵn sàng (DATABASE_URL)")
    init_schema()
    seed()
    seed_bank()


@pytest.fixture
def client(pg):
    return TestClient(app)


def test_scale_and_cut():
    assert scale_score(7, 10) == 700
    assert scale_score(0, 0) == 0
    assert passed(700) is True
    assert passed(699) is False
    assert SCALE_MAX == 1000
    assert CUT_SCORE == 700


def test_knowledge_tree_mo100(pg):
    tree = knowledge_tree("MO-100")
    assert len(tree) == 1
    root = tree[0]
    assert root["id"] == "MO-100"
    codes = [c["code"] for c in root["children"]]
    assert "1" in codes
    obj1 = next(c for c in root["children"] if c["code"] == "1")
    leaves = [c["code"] for c in obj1["children"]]
    assert "1.1" in leaves
    assert any("Navigate" in (c["title"] or "") for c in obj1["children"])
    with cursor() as cur:
        cur.execute("SELECT COUNT(*) AS n FROM objective_domains WHERE subject = 'MO-200'")
        assert cur.fetchone()["n"] >= 6
        cur.execute("SELECT COUNT(*) AS n FROM objective_domains WHERE subject = 'MO-300'")
        assert cur.fetchone()["n"] >= 6


def test_exam_validator_and_autogen(pg):
    packed = pack_exam_projects("word")
    assert 5 <= len(packed) <= 7
    sample = exam_counts("exam-word-mock-1")
    assert sample["ok"] is True
    assert 5 <= sample["projects"] <= 7
    assert 25 <= sample["tasks"] <= 35
    eid = auto_generate_exam(program="word", title="Đề xáo trộn pytest")
    counts = exam_counts(eid)
    assert counts["ok"] is True
    payload = compile_payload("exam-word-mock-1")
    assert payload["score_scale"] == 1000
    assert payload["cut_score"] == 700
    assert payload["hints"] is False
    assert payload["partial_credit"] is False
    assert payload["ui"] == "certiport_split"
    assert payload["focus_lock"] is True
    assert payload["force_submit"] is True
    assert payload["navigation"]["mark_for_review"] is True
    assert payload["duration_minutes"] == 50
    assert payload["projects"]


def test_practice_payload_keeps_hints(pg):
    practice = session_for_project("word-objective-1-1", "training")
    assert practice["hints"] is True
    assert practice["elapsed_only"] is True
    assert practice["partial_credit"] is True
    assert practice["ui"] == "practice"
    exam = session_for_project("word-objective-1-1", "testing")
    assert exam["hints"] is False
    assert exam["focus_lock"] is True
    assert exam["ui"] == "certiport_split"


def test_published_task_cannot_update(pg):
    tid = save_task(
        task_id=None,
        objective_id="mo-100-2-2",
        instruction="Áp dụng style Intense Quote cho đoạn 2",
        weight=2,
        valid_paths=["Ribbon_Copy", "Shortcut_CtrlC", "ContextMenu_Copy"],
        locate="Tab Home",
        tool="Styles",
        configure="Intense Quote",
        hint1="Tab Home nhóm Styles",
        hint2="Chọn Intense Quote",
        hint3="Đoạn văn thứ hai",
    )
    set_task_status(tid, "published")
    with pytest.raises(ValueError, match="published"):
        save_task(
            task_id=tid,
            objective_id="mo-100-2-2",
            instruction="Không được sửa",
            weight=1,
        )
    orphans = list_tasks(orphans=True)
    assert any(t["id"] == tid for t in orphans)
    published = list_tasks(published_only=True)
    assert any(t["id"] == tid for t in published)


def test_publish_exam_locked_when_structure_bad(pg):
    eid = save_exam(
        exam_id=None,
        title="Đề thiếu project",
        exam_type="CERTIFICATION_MOCK",
        program="word",
        project_ids=pack_exam_projects("word")[:2],
    )
    assert exam_counts(eid)["ok"] is False
    with pytest.raises(ValueError, match="structure"):
        set_exam_status(eid, "published")
    drafts = [e for e in list_exams() if e["id"] == eid]
    assert drafts and drafts[0]["status"] == "draft"
    hidden = list_exams(published_only=True)
    assert all(e["id"] != eid for e in hidden)


def test_super_admin_tabs_and_teacher_readonly(client):
    admin = TestClient(app)
    admin.post("/dang-nhap", data={"username": "admin", "password": "Mos@Gds2026"})
    page = admin.get("/quan-tri/ngan-hang")
    assert page.status_code == 200
    body = page.text
    assert "Cây kiến thức" in body
    assert "Lắp ráp đề thi" in body
    assert "Q-Matrix" in body
    assert "Độ tin cậy ngân hàng đề" in body
    assert "Navigate within documents" in body or "1.1" in body
    tree = admin.get("/quan-tri/ngan-hang?tab=cay&mon=MO-200")
    assert "Create and format tables" in tree.text or "3.1" in tree.text
    assembler = admin.get("/quan-tri/ngan-hang?tab=lap-rap&de=exam-word-mock-1")
    assert "5–7" in assembler.text
    assert "25–35" in assembler.text
    assert "exam-canvas" in assembler.text
    created = admin.post(
        "/quan-tri/ngan-hang/task",
        data={
            "objective_id": "mo-100-1-1",
            "instruction": "Dùng Go To tới trang 5",
            "weight": "1",
            "valid_paths": "Ribbon_GoTo, Shortcut_CtrlG",
            "hint1": "Tab Home",
        },
        follow_redirects=False,
    )
    assert created.status_code == 303
    assert "ok=1" in created.headers["location"]

    teacher = TestClient(app)
    teacher.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"})
    denied = teacher.get("/quan-tri/ngan-hang", follow_redirects=False)
    assert denied.status_code == 303
    assert denied.headers["location"] == "/quan-tri/kho-de"
    write = teacher.post(
        "/quan-tri/ngan-hang/task",
        data={"objective_id": "mo-100-1-1", "instruction": "hack", "weight": "3"},
        follow_redirects=False,
    )
    assert write.status_code == 303
    assert write.headers["location"] == "/quan-tri"
    catalog = teacher.get("/quan-tri/kho-de")
    assert catalog.status_code == 200
    assert "Đề thi thử Word MO-100 số 1" in catalog.text
    assert "Không sửa Q-Matrix" in catalog.text
    flag = teacher.post(
        "/quan-tri/kho-de/bao-loi",
        data={"exam_id": "exam-word-mock-1", "detail": "File gốc hỏng"},
        follow_redirects=False,
    )
    assert flag.status_code == 303
    reliability = admin.get("/quan-tri/ngan-hang?tab=do-tin-cay")
    assert "File gốc hỏng" in reliability.text

    student = TestClient(app)
    student.post("/dang-nhap", data={"username": "hocsinh", "password": "Mos@Gds2026"})
    blocked = student.get("/quan-tri/ngan-hang", follow_redirects=False)
    assert blocked.status_code == 303
    assert blocked.headers["location"] == "/tien-do"


def test_five_architecture_layers(pg, client):
    from app.bank import apply_certiport_scale, assign_objective_drill, compile_payload, save_objective

    oid = save_objective(subject="MO-100", code="9.9", title="Extra skill", parent_id="mo-100-1")
    assert oid == "mo-100-9-9"
    exam = apply_certiport_scale(
        {"criteria": [{"earned": 1, "possible": 1, "weight": 1}, {"earned": 0.5, "possible": 1, "weight": 1}]},
        "testing",
    )
    assert exam["scaled_1000"] == 500
    assert exam["passed"] is False
    train = apply_certiport_scale(
        {"criteria": [{"earned": 1, "possible": 1, "weight": 1}, {"earned": 0.5, "possible": 1, "weight": 1}]},
        "training",
    )
    assert train["scaled_1000"] == 750
    one = compile_payload("exam-word-mock-1", student_id=1)
    two = compile_payload("exam-word-mock-1", student_id=2)
    assert {p["project_id"] for p in one["projects"]} == {p["project_id"] for p in two["projects"]}
    assert one.get("version_hash") is not None
    sources = assign_objective_drill(1, "mo-100-2-2")
    assert sources
    assert "word-mail-merge" not in sources
    admin = TestClient(app)
    admin.post("/dang-nhap", data={"username": "admin", "password": "Mos@Gds2026"})
    assert "Cập nhật Tầng 1" in admin.get("/quan-tri/ngan-hang?tab=cay").text
    teacher = TestClient(app)
    teacher.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"})
    catalog = teacher.get("/quan-tri/kho-de")
    assert "Giao luyện tập theo Objective" in catalog.text
    assert "Practice_Mode" in catalog.text
    denied = teacher.post(
        "/quan-tri/ngan-hang/objective",
        data={"subject": "MO-100", "code": "8.8", "title": "hack"},
        follow_redirects=False,
    )
    assert denied.status_code == 303
    assert denied.headers["location"] == "/quan-tri"
    drill = teacher.post(
        "/quan-tri/kho-de/giao-objective",
        data={"objective_id": "mo-100-2-2", "class_id": "1"},
        follow_redirects=False,
    )
    assert drill.status_code == 303
    gaps = teacher.get("/quan-tri/lo-hong")
    assert gaps.status_code == 200
    assert "Objective_Domains" in gaps.text or "Tầng" in gaps.text


def test_client_start_includes_bank_flags(client):
    token = _token(client, "hocsinh")
    started = client.post(
        "/api/v1/attempts",
        headers={"Authorization": f"Bearer {token}"},
        json={"project_id": "word-objective-1-1", "mode": "training"},
    )
    assert started.status_code == 200
    bank = started.json()["bank"]
    assert bank["hints"] is True
    assert bank["score_scale"] == 1000
    assert bank["cut_score"] == 700
    exam = client.post(
        "/api/v1/attempts",
        headers={"Authorization": f"Bearer {token}"},
        json={"project_id": "word-objective-1-1", "mode": "testing"},
    )
    assert exam.status_code == 200
    flags = exam.json()["bank"]
    assert flags["hints"] is False
    assert flags["ui"] == "certiport_split"
    assert flags["focus_lock"] is True
