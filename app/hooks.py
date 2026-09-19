"""GitHub HTTPS webhook — cập nhật mã, không nhận dữ liệu qua SSH."""
from __future__ import annotations

import hashlib
import hmac
import json
import os
import subprocess
from pathlib import Path

from fastapi import APIRouter, HTTPException, Request

ROOT = Path(__file__).resolve().parent.parent
router = APIRouter()


def _secret() -> str:
    return (os.environ.get("MOS_GITHUB_WEBHOOK_SECRET") or "").strip()


def _valid_sig(secret: str, body: bytes, header: str | None) -> bool:
    if not secret or not header:
        return False
    got = header.strip()
    if got.lower().startswith("sha256="):
        got = got.split("=", 1)[1]
    mac = hmac.new(secret.encode("utf-8"), body, hashlib.sha256).hexdigest()
    return hmac.compare_digest(mac, got)


@router.post("/api/v1/hooks/github")
async def github_push(request: Request):
    secret = _secret()
    if not secret:
        raise HTTPException(status_code=404, detail="webhook")
    body = await request.body()
    header = request.headers.get("x-hub-signature-256") or request.headers.get("X-Hub-Signature-256")
    if not _valid_sig(secret, body, header):
        raise HTTPException(status_code=401, detail="signature")
    event = (request.headers.get("x-github-event") or request.headers.get("X-GitHub-Event") or "").lower()
    if event == "ping":
        return {"ok": True, "pong": True, "transport": "https"}
    if event != "push":
        return {"ok": True, "ignored": event, "transport": "https"}
    try:
        payload = json.loads(body.decode("utf-8") or "{}")
    except json.JSONDecodeError:
        payload = {}
    want = os.environ.get("MOS_GIT_REF", "main")
    ref = str(payload.get("ref") or "")
    if ref and ref not in {f"refs/heads/{want}", want}:
        return {"ok": True, "ignored_ref": ref, "transport": "https"}
    if os.environ.get("MOS_GIT_SYNC", "1") != "1":
        return {"ok": True, "dry_run": True, "transport": "https"}
    script = ROOT / "scripts" / "git-sync.sh"
    if not script.is_file():
        raise HTTPException(status_code=500, detail="sync_script")
    env = os.environ.copy()
    env["MOS_ROOT"] = str(ROOT)
    env["MOS_RESTART"] = env.get("MOS_RESTART", "1")
    proc = subprocess.run(
        ["bash", str(script)],
        cwd=str(ROOT),
        env=env,
        capture_output=True,
        text=True,
        timeout=300,
        check=False,
    )
    if proc.returncode != 0:
        raise HTTPException(status_code=500, detail="sync_failed")
    return {
        "ok": True,
        "transport": "https",
        "output": (proc.stdout or "").strip().splitlines()[-2:],
    }
