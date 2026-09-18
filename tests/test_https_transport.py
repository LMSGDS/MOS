"""Data plane is HTTPS only — no SSH/scp to the GPU."""
from __future__ import annotations

import hashlib
import hmac
import os
from pathlib import Path

from fastapi.testclient import TestClient

from app.main import app

ROOT = Path(__file__).resolve().parent.parent


def test_ssh_connect_refuses_to_run():
    import subprocess

    proc = subprocess.run(["bash", str(ROOT / "scripts" / "ssh-connect.sh")], capture_output=True, text=True)
    assert proc.returncode == 2
    assert "SSH" in proc.stderr
    assert "mos.gds.edu.vn" in proc.stderr


def test_git_sync_rejects_ssh_origin(tmp_path):
    import subprocess

    repo = tmp_path / "mos"
    repo.mkdir()
    subprocess.run(["git", "init", "-q"], cwd=repo, check=True)
    subprocess.run(["git", "remote", "add", "origin", "git@github.com:LMSGDS/MOS.git"], cwd=repo, check=True)
    env = {
        "PATH": os.environ.get("PATH", "/usr/bin:/bin"),
        "HOME": str(tmp_path),
        "MOS_ROOT": str(repo.resolve()),
        "MOS_RESTART": "0",
        "MOS_GIT_URL": "https://github.com/LMSGDS/MOS.git",
    }
    proc = subprocess.run(
        ["bash", str(ROOT / "scripts" / "git-sync.sh")],
        cwd=repo,
        env=env,
        capture_output=True,
        text=True,
    )
    assert proc.returncode == 2, proc.stdout + proc.stderr
    assert "HTTPS" in proc.stderr


def test_readme_and_ci_have_no_gpu_ssh_secrets():
    readme = (ROOT / "README.md").read_text(encoding="utf-8")
    assert "SSH_PASSWORD" not in readme
    assert "160.191" not in readme
    assert "https://mos.gds.edu.vn/api/v1/" in readme
    assert "Chuyển dữ liệu — chỉ HTTPS" in readme
    for path in (ROOT / ".github" / "workflows").glob("*.yml"):
        text = path.read_text(encoding="utf-8")
        assert "sshpass" not in text
        assert "SSH_PASSWORD" not in text
        assert "SSH via OpenVPN" not in text


def test_github_webhook_disabled_without_secret(monkeypatch):
    monkeypatch.delenv("MOS_GITHUB_WEBHOOK_SECRET", raising=False)
    client = TestClient(app)
    denied = client.post("/api/v1/hooks/github", content=b"{}")
    assert denied.status_code == 404


def test_git_sync_rejects_ssh_url_env(tmp_path):
    import subprocess

    repo = tmp_path / "mos"
    repo.mkdir()
    subprocess.run(["git", "init", "-q"], cwd=repo, check=True)
    subprocess.run(["git", "remote", "add", "origin", "https://github.com/LMSGDS/MOS.git"], cwd=repo, check=True)
    env = {
        "PATH": os.environ.get("PATH", "/usr/bin:/bin"),
        "HOME": str(tmp_path),
        "MOS_ROOT": str(repo.resolve()),
        "MOS_RESTART": "0",
        "MOS_GIT_URL": "git@github.com:LMSGDS/MOS.git",
    }
    proc = subprocess.run(
        ["bash", str(ROOT / "scripts" / "git-sync.sh")],
        cwd=repo,
        env=env,
        capture_output=True,
        text=True,
    )
    assert proc.returncode == 2, proc.stdout + proc.stderr


def test_github_webhook_https_hmac(monkeypatch):
    monkeypatch.setenv("MOS_GITHUB_WEBHOOK_SECRET", "unit-test-secret")
    monkeypatch.setenv("MOS_GIT_SYNC", "0")
    client = TestClient(app)
    body = b'{"ref":"refs/heads/main"}'
    mac = hmac.new(b"unit-test-secret", body, hashlib.sha256).hexdigest()
    denied = client.post("/api/v1/hooks/github", content=body)
    assert denied.status_code == 401
    ping = client.post(
        "/api/v1/hooks/github",
        content=b"{}",
        headers={"X-Hub-Signature-256": "sha256=" + hmac.new(b"unit-test-secret", b"{}", hashlib.sha256).hexdigest(), "X-GitHub-Event": "ping"},
    )
    assert ping.status_code == 200
    assert ping.json()["transport"] == "https"
    ok = client.post(
        "/api/v1/hooks/github",
        content=body,
        headers={"X-Hub-Signature-256": "sha256=" + mac, "X-GitHub-Event": "push"},
    )
    assert ok.status_code == 200
    assert ok.json()["transport"] == "https"
    assert ok.json()["dry_run"] is True


def test_desktop_portal_is_https_only():
    portal = (ROOT / "desktop" / "MosDock" / "Portal.cs").read_text(encoding="utf-8")
    assert "https://mos.gds.edu.vn" in portal
    assert "ssh://" not in portal.lower()
    assert "scp" not in portal.lower()
