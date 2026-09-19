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
api = f"https://api.github.com/repos/{repo}/actions/artifacts?per_page=30"
wanted = {
    "MOS-KulKul-Setup-Windows": (
        "MOS-KulKul-Setup-Windows.exe",
        "MOS-KulKul-Setup-Windows.zip",
        "MOS-KulKul-Setup-Windows-Full.exe",
        "MOS-KulKul-Setup-Windows-Full.zip",
        "SHA256.txt",
    ),
    "MOS-KulKul-Setup-macOS": (
        "MOS-KulKul-Setup-macOS.zip",
        "MOS-KulKul-Setup-macOS.pkg",
        "SHA256-macOS.txt",
    ),
    "MOS-KulKul-Setup-macOS-zip": ("MOS-KulKul-Setup-macOS.zip",),
    "MOS-KulKul-Setup-macOS-src": ("MOS-KulKul-Setup-macOS.zip",),
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
copied = 0
for name, files in wanted.items():
    matches = [a for a in artifacts if a.get("name") == name]
    matches.sort(key=lambda a: a.get("created_at") or "", reverse=True)
    pick = [
        a
        for a in matches
        if (a.get("workflow_run") or {}).get("head_branch") == "main"
    ] or [
        a
        for a in matches
        if not str((a.get("workflow_run") or {}).get("head_branch") or "").startswith("dependabot/")
    ]
    if not pick:
        sys.stderr.write(f"Khong co artifact {name}\n")
        continue
    art = pick[0]
    url = art.get("archive_download_url") or f"https://api.github.com/repos/{repo}/actions/artifacts/{art['id']}/zip"
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
            print(f"{fname}  {found.stat().st_size}  artifact={art['id']}")

print(f"copied={copied} dir={dest}")
if copied == 0:
    sys.exit(1)
PY
