"""LTI 1.3 Tool Provider: OIDC login, launch, JWKS — Dual-role, admin đăng ký platform."""
from __future__ import annotations

import json
import time

import jwt
import pytest
from cryptography.hazmat.primitives.asymmetric import rsa
from fastapi.testclient import TestClient

from app.db import cursor, init_schema
from app.identity import upsert_lis_person
from app.lti import CLAIM_AGS, CLAIM_CTX, CLAIM_DEP, CLAIM_MSG, CLAIM_RES, CLAIM_ROLES, CLAIM_VER, complete_launch, register_platform, tool_jwks
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
def client(pg):
    return TestClient(app)


def _platform_key():
    return rsa.generate_private_key(public_exponent=65537, key_size=2048)


def _jwk(pub) -> dict:
    numbers = pub.public_numbers()

    def b64u(n: int) -> str:
        raw = n.to_bytes((n.bit_length() + 7) // 8, "big")
        return jwt.utils.base64url_encode(raw).decode()

    return {"kty": "RSA", "use": "sig", "alg": "RS256", "kid": "canvas-test", "n": b64u(numbers.n), "e": b64u(numbers.e)}


def test_jwks_and_info(client):
    jwks = client.get("/lti/jwks")
    assert jwks.status_code == 200
    assert jwks.json()["keys"][0]["kty"] == "RSA"
    info = client.get("/lti/info")
    assert info.status_code == 200
    body = info.json()
    assert body["stack"] == "python-fastapi"
    assert body["role"] == "lti_1_3_tool_provider"
    assert "forum" in body["not_built"]


def test_oidc_login_redirect(client):
    key = _platform_key()
    register_platform(
        name="Canvas test",
        issuer="https://canvas.test.example",
        client_id="cid-1",
        auth_login_url="https://canvas.test.example/api/lti/authorize_redirect",
        jwks_json={"keys": [_jwk(key.public_key())]},
    )
    r = client.get(
        "/lti/login",
        params={
            "iss": "https://canvas.test.example",
            "login_hint": "hint-99",
            "target_link_uri": "https://mos.gds.edu.vn/lti/launch",
            "client_id": "cid-1",
        },
        follow_redirects=False,
    )
    assert r.status_code == 302
    loc = r.headers["location"]
    assert "canvas.test.example/api/lti/authorize_redirect" in loc
    assert "response_type=id_token" in loc
    assert "login_hint=hint-99" in loc


def test_launch_creates_lis_user(client):
    key = _platform_key()
    platform = register_platform(
        name="Moodle test",
        issuer="https://moodle.test.example",
        client_id="cid-2",
        auth_login_url="https://moodle.test.example/mod/lti/auth.php",
        jwks_json={"keys": [_jwk(key.public_key())]},
    )
    nonce = "nonce-launch-1"
    now = int(time.time())
    claims = {
        "iss": "https://moodle.test.example",
        "aud": "cid-2",
        "sub": "student-lti-7",
        "name": "Học sinh Canvas",
        "nonce": nonce,
        "iat": now,
        "exp": now + 600,
        CLAIM_VER: "1.3.0",
        CLAIM_MSG: "LtiResourceLinkRequest",
        CLAIM_DEP: "dep-1",
        CLAIM_ROLES: ["http://purl.imsglobal.org/vocab/lis/v2/membership#Learner"],
        CLAIM_CTX: {"id": "ctx-10b", "title": "10B LTI"},
        CLAIM_RES: {"id": "word-objective-1-1"},
        CLAIM_AGS: {"lineitem": "", "scope": []},
    }
    launched = complete_launch(claims, platform)
    assert launched["user"]["role"] == "student"
    assert launched["user"]["name"] == "Học sinh Canvas"
    assert launched["project_id"] == "word-objective-1-1"
    with cursor() as cur:
        cur.execute("SELECT lis_sourced_id, lti_sub FROM users WHERE id = %s", (launched["user"]["id"],))
        row = cur.fetchone()
    assert row["lti_sub"] == "student-lti-7"


def test_admin_registers_platform_teacher_blocked(client):
    teacher = TestClient(app)
    teacher.post("/dang-nhap", data={"username": "giaovien", "password": "Mos@Gds2026"})
    blocked = teacher.get("/quan-tri/lti", follow_redirects=False)
    assert blocked.status_code in {303, 307}
    admin = TestClient(app)
    admin.post("/dang-nhap", data={"username": "admin", "password": "Mos@Gds2026"})
    page = admin.get("/quan-tri/lti")
    assert page.status_code == 200
    assert "OIDC Login" in page.text
    assert "Không xây forum" in page.text
    saved = admin.post(
        "/quan-tri/lti",
        data={
            "name": "Canvas GDS",
            "issuer": "https://canvas.gds.test",
            "client_id": "admin-cid",
            "auth_login_url": "https://canvas.gds.test/api/lti/authorize_redirect",
            "auth_token_url": "https://canvas.gds.test/login/oauth2/token",
            "jwks_url": "https://canvas.gds.test/api/lti/security/jwks",
        },
        follow_redirects=False,
    )
    assert saved.status_code in {303, 307}


def test_lis_instructor_is_teacher(pg):
    person = upsert_lis_person(
        issuer="https://canvas.test.example",
        sub="gv-9",
        name="Cô LTI",
        roles=["http://purl.imsglobal.org/vocab/lis/v2/membership#Instructor"],
        context_id="ctx-gv",
        context_title="Lớp LTI GV",
        deployment_id="d1",
    )
    assert person["role"] == "teacher"
    assert tool_jwks()["keys"]
