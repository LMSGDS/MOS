#!/usr/bin/env bash
# Tải bộ cài MOS-KulKul mới nhất từ GitHub Actions (HTTPS). Không dùng SSH.
set -euo pipefail

ROOT="${MOS_ROOT:-$(cd "$(dirname "$0")/.." && pwd)}"
cd "$ROOT"

if [[ "${1:-}" == "--help" ]]; then
  echo "Tải artifact CI vào data/installers/ qua https://api.github.com (cần MOS_GITHUB_TOKEN)."
  exit 0
fi

TOKEN="${MOS_GITHUB_TOKEN:-${GITHUB_TOKEN:-}}"
if [[ -z "$TOKEN" ]]; then
  echo "Bo qua sync-installers: thieu MOS_GITHUB_TOKEN" >&2
  exit 0
fi

python3 - "$ROOT" "$TOKEN" <<'PY'
import json, os, shutil, sys, tempfile, urllib.error, urllib.request, zipfile
from pathlib import Path

root = Path(sys.argv[1])
token = sys.argv[2]
dest = root / "data" / "installers"
dest.mkdir(parents=True, exist_ok=True)
repo = os.environ.get("MOS_GITHUB_REPO", "LMSGDS/MOS")
# MOS_GITHUB_API chỉ để test trỏ vào máy chủ giả; production luôn là api.github.com.
api_base = os.environ.get("MOS_GITHUB_API", "https://api.github.com").rstrip("/")
api = f"{api_base}/repos/{repo}/actions/artifacts?per_page=30"
wanted = {
    "MOS-KulKul-Setup-Windows": (
        "MOS-KulKul-Setup-Windows.exe",
        "MOS-KulKul-Setup-Windows.zip",
        "MOS-KulKul-Setup-Windows-Full.exe",
        "MOS-KulKul-Setup-Windows-Full.zip",
        "SHA256.txt",
    ),
    # Chỉ lấy artifact của job "macOS Setup.app + .pkg". Artifact
    # MOS-KulKul-Setup-macOS-src (bộ script install.sh, ~70 KB) cũng mang tên tệp
    # MOS-KulKul-Setup-macOS.zip; tải nó sau sẽ ghi đè bản pkg nén (~1,5 MB) mà
    # GitHub Release công bố, khiến /cai-dat/macos trả bản khác Release.
    "MOS-KulKul-Setup-macOS": (
        "MOS-KulKul-Setup-macOS.zip",
        "MOS-KulKul-Setup-macOS.pkg",
        "SHA256-macOS.txt",
    ),
}


class StripAuthRedirect(urllib.request.HTTPRedirectHandler):
    """Azure blob signed URLs reject the GitHub Authorization header."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return urllib.request.Request(
            newurl,
            headers={"User-Agent": "MOS-KulKul-sync-installers"},
        )


def get(url: str, binary: bool = False):
    req = urllib.request.Request(
        url,
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "MOS-KulKul-sync-installers",
        },
    )
    opener = urllib.request.build_opener(StripAuthRedirect)
    with opener.open(req, timeout=300) as resp:
        data = resp.read()
    return data if binary else json.loads(data.decode("utf-8"))

try:
    listing = get(api)
except urllib.error.HTTPError as exc:
    sys.stderr.write(f"Khong liet ke artifact GitHub: {exc}\n")
    sys.exit(1)

artifacts = [a for a in listing.get("artifacts") or [] if not a.get("expired")]
# Prefer Windows Full.exe built from main (Help / không trợ giúp). Override
# with MOS_INSTALLER_ARTIFACT_ID or MOS_INSTALLER_BRANCH if needed.
preferred_id = os.environ.get("MOS_INSTALLER_ARTIFACT_ID", "").strip()
preferred_branch = os.environ.get("MOS_INSTALLER_BRANCH", "main").strip()
copied = 0
for name, files in wanted.items():
    matches = [a for a in artifacts if a.get("name") == name]
    matches.sort(key=lambda a: a.get("created_at") or "", reverse=True)
    pick = []
    if preferred_id:
        pick = [a for a in matches if str(a.get("id")) == preferred_id]
    if not pick and preferred_branch:
        pick = [
            a
            for a in matches
            if (a.get("workflow_run") or {}).get("head_branch") == preferred_branch
        ]
    if not pick:
        pick = [
            a
            for a in matches
            if not str((a.get("workflow_run") or {}).get("head_branch") or "").startswith(
                "dependabot/"
            )
        ]
    if not pick:
        sys.stderr.write(f"Khong co artifact {name}\n")
        continue
    art = pick[0]
    url = art.get("archive_download_url") or f"{api_base}/repos/{repo}/actions/artifacts/{art['id']}/zip"
    with tempfile.TemporaryDirectory(prefix="mos-art-") as tmp:
        zpath = Path(tmp) / "art.zip"
        zpath.write_bytes(get(url, binary=True))
        with zipfile.ZipFile(zpath) as zf:
            zf.extractall(tmp)
        for fname in files:
            found = next(Path(tmp).rglob(fname), None)
            if found is None or not found.is_file():
                continue
            shutil.copy2(found, dest / fname)
            copied += 1
            branch = (art.get("workflow_run") or {}).get("head_branch") or "?"
            print(f"{fname}  {found.stat().st_size}  artifact={art['id']}  branch={branch}")

print(f"copied={copied} dir={dest}")
if copied == 0:
    sys.exit(1)
PY
