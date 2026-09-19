"""Data plane is HTTPS only — no SSH/scp to the GPU."""
from __future__ import annotations

import hashlib
import hmac
import os
from pathlib import Path

from fastapi.testclient import TestClient

from app.main import app

ROOT = Path(__file__).resolve().parent.parent


def test_healthz_reports_git_revision():
    client = TestClient(app)
    body = client.get("/healthz").json()
    assert body["ok"] is True
    assert body["transport"] == "https"
    assert body["git"]["sha"]
    assert len(str(body["git"]["sha"])) >= 7
    assert isinstance(body["installers"], list)


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


def test_webhook_secret_reads_git_sync_env(tmp_path, monkeypatch):
    from app import hooks

    monkeypatch.setattr(hooks, "ROOT", tmp_path)
    monkeypatch.delenv("MOS_GITHUB_WEBHOOK_SECRET", raising=False)
    (tmp_path / "data").mkdir()
    (tmp_path / "data" / "git-sync.env").write_text(
        "MOS_GITHUB_WEBHOOK_SECRET=from-file\nMOS_GITHUB_TOKEN=not-a-secret-for-test\n",
        encoding="utf-8",
    )
    assert hooks._secret() == "from-file"


def test_mos_service_loads_git_sync_env():
    unit = (ROOT / "deploy" / "mos.service").read_text(encoding="utf-8")
    timer = (ROOT / "deploy" / "mos-git-sync.timer").read_text(encoding="utf-8")
    assert "EnvironmentFile=-/home/plhien/MOS/data/git-sync.env" in unit
    assert "OnUnitActiveSec=5min" in timer


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


def test_git_sync_env_example_has_no_secret():
    example = (ROOT / "data" / "git-sync.env.example").read_text(encoding="utf-8")
    assert "MOS_GITHUB_TOKEN=" in example
    assert "github_pat_thay_bang_token" in example
    assert "github_pat_" in example
    assert len([ln for ln in example.splitlines() if ln.startswith("MOS_GITHUB_TOKEN=github_pat_") and "thay_bang" not in ln]) == 0


def test_github_sync_downloads_installers_over_https():
    root = ROOT
    sync = (root / "scripts" / "sync-installers.sh").read_text(encoding="utf-8")
    git_sync = (root / "scripts" / "git-sync.sh").read_text(encoding="utf-8")
    main = (root / "app" / "main.py").read_text(encoding="utf-8")
    assert "MOS_INSTALLER_BRANCH" in sync
    assert 'os.environ.get("MOS_INSTALLER_BRANCH", "main")' in sync
    assert "MOS_INSTALLER_ARTIFACT_ID" in sync
    assert "dependabot/" in sync
    assert "https://api.github.com/repos/" in sync
    assert "MOS-KulKul-Setup-Windows-Full.exe" in sync
    assert "MOS-KulKul-Setup-macOS.pkg" in sync
    assert "ssh://" not in sync.lower()
    assert "scp " not in sync.lower()
    assert "sshpass" not in sync.lower()
    assert "sync-installers.sh" in git_sync
    assert 'pip" install' in git_sync or "pip install" in git_sync
    assert "requirements.txt" in git_sync
    assert git_sync.find("systemctl restart") < git_sync.rfind("sync-installers.sh")
    assert "x-access-token:" in git_sync
    assert "AUTHORIZATION: basic" in git_sync
    assert "reset --hard" in git_sync
    assert "merge --ff-only" not in git_sync
    assert "AUTHORIZATION: bearer" not in git_sync.lower()
    assert "-GDS.exe" not in main
    assert "from app.lti import router" in main
    assert "LTI chua san sang" in main
    tokens = (root / "app" / "tokens.py").read_text(encoding="utf-8")
    assert "import jwt" not in tokens
    from app.tokens import decode, issue

    tok = issue({"username": "hs-boot", "name": "Học sinh", "role": "student", "id": 7})
    data = decode(tok)
    assert data["sub"] == "hs-boot"
    assert data["uid"] == 7
    assert decode("not-a-token") is None
    hooks = (root / "app" / "hooks.py").read_text(encoding="utf-8")
    assert "timeout=300" in hooks
    portal = (ROOT / "desktop" / "MosDock" / "Portal.cs").read_text(encoding="utf-8")
    assert "https://mos.gds.edu.vn" in portal
    assert "ssh://" not in portal.lower()
    assert "scp" not in portal.lower()
