"""LTI 1.3 Tool Provider — mos.gds.edu.vn là engine MOS, Canvas/Moodle giữ sổ điểm.

OIDC login → launch → JWKS → AGS Grade Passback.
Không Deep Linking, forum, hay quiz engine.
"""
from __future__ import annotations

import json
import os
import secrets
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import urlencode

import jwt
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import RedirectResponse

from app.db import cursor
from app.identity import public_user, upsert_lis_person
from app.tokens import issue

ROOT = Path(__file__).resolve().parent.parent
KEY_PATH = ROOT / "data" / "lti_tool.pem"
KID = "mos-lti-1"
router = APIRouter()

LTI_VERSION = "1.3.0"
CLAIM_MSG = "https://purl.imsglobal.org/spec/lti/claim/message_type"
CLAIM_VER = "https://purl.imsglobal.org/spec/lti/claim/version"
CLAIM_DEP = "https://purl.imsglobal.org/spec/lti/claim/deployment_id"
CLAIM_CTX = "https://purl.imsglobal.org/spec/lti/claim/context"
CLAIM_RES = "https://purl.imsglobal.org/spec/lti/claim/resource_link"
CLAIM_ROLES = "https://purl.imsglobal.org/spec/lti/claim/roles"
CLAIM_LIS = "https://purl.imsglobal.org/spec/lti/claim/lis"
CLAIM_CUSTOM = "https://purl.imsglobal.org/spec/lti/claim/custom"
CLAIM_AGS = "https://purl.imsglobal.org/spec/lti-ags/claim/endpoint"
AGS_SCORE = "https://purl.imsglobal.org/spec/lti-ags/scope/score"


def _tool_private():
    KEY_PATH.parent.mkdir(parents=True, exist_ok=True)
    if KEY_PATH.exists():
        return serialization.load_pem_private_key(KEY_PATH.read_bytes(), password=None)
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    KEY_PATH.write_bytes(
        key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.TraditionalOpenSSL,
            encryption_algorithm=serialization.NoEncryption(),
        )
    )
    try:
        KEY_PATH.chmod(0o600)
    except OSError:
        pass
    return key


def tool_jwks() -> dict:
    pub = _tool_private().public_key()
    numbers = pub.public_numbers()

    def b64u(n: int) -> str:
        raw = n.to_bytes((n.bit_length() + 7) // 8, "big")
        return jwt.utils.base64url_encode(raw).decode()

    return {
        "keys": [
            {
                "kty": "RSA",
                "use": "sig",
                "alg": "RS256",
                "kid": KID,
                "n": b64u(numbers.n),
                "e": b64u(numbers.e),
            }
        ]
    }


def _base(request: Request) -> str:
    env = os.environ.get("MOS_PUBLIC_URL", "").rstrip("/")
    if env:
        return env
    host = request.headers.get("host") or "mos.gds.edu.vn"
    scheme = "https" if request.url.scheme == "https" or host.endswith("edu.vn") else request.url.scheme
    return f"{scheme}://{host}"


def list_platforms() -> list[dict]:
    with cursor() as cur:
        cur.execute("SELECT * FROM lti_platforms ORDER BY name, issuer")
        return list(cur.fetchall())


def get_platform(issuer: str, client_id: str | None = None) -> dict | None:
    with cursor() as cur:
        if client_id:
            cur.execute(
                "SELECT * FROM lti_platforms WHERE issuer = %s AND client_id = %s",
                (issuer, client_id),
            )
        else:
            cur.execute("SELECT * FROM lti_platforms WHERE issuer = %s", (issuer,))
        return cur.fetchone()


def register_platform(
    *,
    name: str,
    issuer: str,
    client_id: str,
    auth_login_url: str,
    auth_token_url: str = "",
    jwks_url: str = "",
    jwks_json: dict | None = None,
) -> dict:
    with cursor() as cur:
        cur.execute(
            """
            INSERT INTO lti_platforms (
              name, issuer, client_id, auth_login_url, auth_token_url, jwks_url, jwks_json
            ) VALUES (%s, %s, %s, %s, %s, %s, %s::jsonb)
            ON CONFLICT (issuer) DO UPDATE SET
              name = EXCLUDED.name,
              client_id = EXCLUDED.client_id,
              auth_login_url = EXCLUDED.auth_login_url,
              auth_token_url = EXCLUDED.auth_token_url,
              jwks_url = EXCLUDED.jwks_url,
              jwks_json = COALESCE(EXCLUDED.jwks_json, lti_platforms.jwks_json)
            RETURNING *
            """,
            (
                name.strip() or issuer,
                issuer.strip(),
                client_id.strip(),
                auth_login_url.strip(),
                (auth_token_url or "").strip(),
                (jwks_url or "").strip(),
                json.dumps(jwks_json) if jwks_json else None,
            ),
        )
        return cur.fetchone()


def _save_nonce(state: str, nonce: str, target: str, hint: str) -> None:
    with cursor() as cur:
        cur.execute(
            """
            INSERT INTO lti_nonces (state, nonce, target_link_uri, login_hint)
            VALUES (%s, %s, %s, %s)
            """,
            (state, nonce, target, hint),
        )


def _take_nonce(state: str) -> dict | None:
    with cursor() as cur:
        cur.execute("SELECT * FROM lti_nonces WHERE state = %s", (state,))
        row = cur.fetchone()
        if row:
            cur.execute("DELETE FROM lti_nonces WHERE state = %s", (state,))
        return row


def _fetch_jwks(platform: dict) -> dict:
    cached = platform.get("jwks_json")
    if isinstance(cached, dict) and cached.get("keys"):
        return cached
    if isinstance(cached, str):
        try:
            data = json.loads(cached)
            if data.get("keys"):
                return data
        except json.JSONDecodeError:
            pass
    url = platform.get("jwks_url") or ""
    if not url:
        raise HTTPException(status_code=400, detail="jwks")
    req = urllib.request.Request(url, headers={"User-Agent": "MOS-KulKul-LTI"})
    with urllib.request.urlopen(req, timeout=8) as resp:
        return json.loads(resp.read().decode("utf-8"))


def _decode_id_token(id_token: str, platform: dict) -> dict:
    jwks = _fetch_jwks(platform)
    try:
        header = jwt.get_unverified_header(id_token)
    except jwt.PyJWTError as exc:
        raise HTTPException(status_code=400, detail="id_token") from exc
    kid = header.get("kid")
    key = None
    for item in jwks.get("keys") or []:
        if not kid or item.get("kid") == kid:
            key = jwt.algorithms.RSAAlgorithm.from_jwk(json.dumps(item))
            break
    if key is None:
        raise HTTPException(status_code=400, detail="kid")
    try:
        claims = jwt.decode(
            id_token,
            key=key,
            algorithms=["RS256"],
            audience=platform["client_id"],
            issuer=platform["issuer"],
        )
    except jwt.PyJWTError as exc:
        raise HTTPException(status_code=401, detail="id_token") from exc
    return claims


def _project_from_claims(claims: dict) -> str | None:
    custom = claims.get(CLAIM_CUSTOM) or {}
    if isinstance(custom, dict):
        for key in ("project_id", "project", "mos_project"):
            if custom.get(key):
                return str(custom[key])
    res = claims.get(CLAIM_RES) or {}
    if isinstance(res, dict) and res.get("id"):
        rid = str(res["id"])
        if rid.startswith("word-") or rid.startswith("excel-") or rid.startswith("ppt-"):
            return rid
    return None


def complete_launch(claims: dict, platform: dict) -> dict:
    if claims.get(CLAIM_VER) != LTI_VERSION:
        raise HTTPException(status_code=400, detail="lti_version")
    msg = claims.get(CLAIM_MSG) or ""
    if msg not in ("LtiResourceLinkRequest", "LtiStartAssessment"):
        raise HTTPException(status_code=400, detail="message_type")
    sub = str(claims.get("sub") or "")
    if not sub:
        raise HTTPException(status_code=400, detail="sub")
    ctx = claims.get(CLAIM_CTX) or {}
    lis = claims.get(CLAIM_LIS) or {}
    person = upsert_lis_person(
        issuer=platform["issuer"],
        sub=sub,
        name=str(claims.get("name") or claims.get("given_name") or ""),
        email=str(claims.get("email") or ""),
        roles=claims.get(CLAIM_ROLES) or [],
        sourced_id=str(lis.get("person_sourcedid") or ""),
        context_id=str(ctx.get("id") or ""),
        context_title=str(ctx.get("title") or ctx.get("label") or ""),
        deployment_id=str(claims.get(CLAIM_DEP) or ""),
    )
    ags = claims.get(CLAIM_AGS) or {}
    launch_id = secrets.token_hex(12)
    project_id = _project_from_claims(claims)
    res = claims.get(CLAIM_RES) or {}
    with cursor() as cur:
        cur.execute(
            """
            INSERT INTO lti_launches (
              id, user_id, platform_id, deployment_id, context_id, resource_link_id,
              project_id, lineitem, ags_scopes, lti_sub
            ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s::jsonb, %s)
            """,
            (
                launch_id,
                person["id"],
                platform["id"],
                str(claims.get(CLAIM_DEP) or ""),
                str(ctx.get("id") or ""),
                str(res.get("id") or ""),
                project_id,
                str(ags.get("lineitem") or ""),
                json.dumps(ags.get("scope") or []),
                sub,
            ),
        )
        if deployment_id := str(claims.get(CLAIM_DEP) or ""):
            cur.execute(
                """
                INSERT INTO lti_deployments (platform_id, deployment_id)
                VALUES (%s, %s) ON CONFLICT DO NOTHING
                """,
                (platform["id"], deployment_id),
            )
    token = issue(public_user(person))
    return {
        "launch_id": launch_id,
        "user": public_user(person),
        "class_id": person.get("class_id"),
        "project_id": project_id,
        "token": token,
        "lineitem": ags.get("lineitem") or "",
    }


def latest_launch(user_id: int) -> dict | None:
    with cursor() as cur:
        cur.execute(
            """
            SELECT * FROM lti_launches
            WHERE user_id = %s
            ORDER BY created_at DESC
            LIMIT 1
            """,
            (user_id,),
        )
        return cur.fetchone()


def client_assertion(platform: dict, token_url: str) -> str:
    now = int(time.time())
    return jwt.encode(
        {
            "iss": platform["client_id"],
            "sub": platform["client_id"],
            "aud": token_url,
            "iat": now,
            "exp": now + 300,
            "jti": secrets.token_hex(8),
        },
        _tool_private(),
        algorithm="RS256",
        headers={"kid": KID},
    )


def passback_score(launch: dict, score: float, max_score: float = 100) -> dict:
    """AGS Grade Passback — điểm chính thức từ lõi chấm MOS, không từ client."""
    lineitem = (launch or {}).get("lineitem") or ""
    if not lineitem or not launch.get("lti_sub"):
        return {"ok": False, "reason": "no_ags"}
    with cursor() as cur:
        cur.execute("SELECT * FROM lti_platforms WHERE id = %s", (launch.get("platform_id"),))
        platform = cur.fetchone()
    if not platform or not platform.get("auth_token_url"):
        return {"ok": False, "reason": "no_token_url"}
    token_url = platform["auth_token_url"]
    body = urlencode(
        {
            "grant_type": "client_credentials",
            "client_assertion_type": "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            "client_assertion": client_assertion(platform, token_url),
            "scope": AGS_SCORE,
        }
    ).encode()
    req = urllib.request.Request(
        token_url,
        data=body,
        headers={"Content-Type": "application/x-www-form-urlencoded", "User-Agent": "MOS-KulKul-LTI"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=8) as resp:
            tok = json.loads(resp.read().decode("utf-8"))
    except (urllib.error.URLError, TimeoutError, json.JSONDecodeError) as exc:
        return {"ok": False, "reason": "token", "error": str(exc)}
    access = tok.get("access_token")
    if not access:
        return {"ok": False, "reason": "token"}
    score_url = lineitem.rstrip("/") + "/scores"
    payload = {
        "timestamp": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "scoreGiven": float(score),
        "scoreMaximum": float(max_score or 100),
        "activityProgress": "Completed",
        "gradingProgress": "FullyGraded",
        "userId": launch["lti_sub"],
    }
    req = urllib.request.Request(
        score_url,
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Authorization": f"Bearer {access}",
            "Content-Type": "application/vnd.ims.lis.v1.score+json",
            "User-Agent": "MOS-KulKul-LTI",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=8) as resp:
            return {"ok": True, "status": resp.status}
    except (urllib.error.URLError, TimeoutError) as exc:
        return {"ok": False, "reason": "score", "error": str(exc)}


def passback_if_launch(user_id: int, score: float | None, max_score: float = 100) -> dict:
    if score is None or not user_id:
        return {"ok": False, "reason": "no_score"}
    launch = latest_launch(user_id)
    if not launch:
        return {"ok": False, "reason": "no_launch"}
    return passback_score(launch, float(score), max_score)


@router.get("/lti/jwks")
def lti_jwks():
    return tool_jwks()


@router.api_route("/lti/login", methods=["GET", "POST"])
async def lti_login(request: Request):
    data: dict[str, Any] = dict(request.query_params)
    if request.method == "POST":
        form = await request.form()
        data.update({k: str(v) for k, v in form.items()})
    iss = str(data.get("iss") or "")
    login_hint = str(data.get("login_hint") or "")
    target = str(data.get("target_link_uri") or "")
    client_id = str(data.get("client_id") or "")
    message_hint = str(data.get("lti_message_hint") or "")
    if not iss or not login_hint or not target:
        raise HTTPException(status_code=400, detail="oidc")
    platform = get_platform(iss, client_id or None)
    if not platform:
        raise HTTPException(status_code=404, detail="platform")
    state = secrets.token_urlsafe(24)
    nonce = secrets.token_urlsafe(24)
    _save_nonce(state, nonce, target, login_hint)
    params = {
        "scope": "openid",
        "response_type": "id_token",
        "response_mode": "form_post",
        "prompt": "none",
        "client_id": platform["client_id"],
        "redirect_uri": f"{_base(request)}/lti/launch",
        "login_hint": login_hint,
        "state": state,
        "nonce": nonce,
    }
    if message_hint:
        params["lti_message_hint"] = message_hint
    return RedirectResponse(f"{platform['auth_login_url']}?{urlencode(params)}", status_code=302)


@router.post("/lti/launch")
async def lti_launch(request: Request):
    form = await request.form()
    id_token = str(form.get("id_token") or "")
    state = str(form.get("state") or "")
    pending = _take_nonce(state)
    if not pending or not id_token:
        raise HTTPException(status_code=400, detail="state")
    unverified = jwt.decode(id_token, options={"verify_signature": False})
    iss = str(unverified.get("iss") or "")
    aud = unverified.get("aud")
    client_id = aud[0] if isinstance(aud, list) else aud
    platform = get_platform(iss, str(client_id) if client_id else None)
    if not platform:
        raise HTTPException(status_code=404, detail="platform")
    claims = _decode_id_token(id_token, platform)
    if claims.get("nonce") != pending["nonce"]:
        raise HTTPException(status_code=401, detail="nonce")
    launched = complete_launch(claims, platform)
    request.session["user"] = launched["user"]
    request.session["lti_launch_id"] = launched["launch_id"]
    request.session["chuong_trinh"] = "word"
    dest = "/"
    if launched.get("project_id"):
        dest = f"/?chuong-trinh=word&bai={urllib.parse.quote(launched['project_id'])}"
    return RedirectResponse(dest, status_code=303)


@router.get("/lti/info")
def lti_info(request: Request):
    base = _base(request)
    return {
        "ok": True,
        "role": "lti_1_3_tool_provider",
        "stack": "python-fastapi",
        "standalone": True,
        "login_url": f"{base}/lti/login",
        "launch_url": f"{base}/lti/launch",
        "jwks_url": f"{base}/lti/jwks",
        "not_built": ["forum", "essay", "quiz", "mail"],
    }
