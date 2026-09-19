#!/usr/bin/env bash
# Cập nhật MOS trên chính máy chủ qua GitHub HTTPS — không SSH, không scp.
set -euo pipefail

ROOT="${MOS_ROOT:-$(cd "$(dirname "$0")/.." && pwd)}"
REF="${MOS_GIT_REF:-main}"
REMOTE_URL="${MOS_GIT_URL:-https://github.com/LMSGDS/MOS.git}"
cd "$ROOT"

GIT_ARGS=()
if [[ -n "${MOS_GITHUB_TOKEN:-}" ]]; then
  # Git HTTPS wants Basic x-access-token, not Bearer (Bearer → invalid credentials).
  BASIC="$(printf 'x-access-token:%s' "$MOS_GITHUB_TOKEN" | base64 -w0 2>/dev/null || printf 'x-access-token:%s' "$MOS_GITHUB_TOKEN" | base64 | tr -d '\n')"
  GIT_ARGS=(-c "http.extraheader=AUTHORIZATION: basic ${BASIC}")
fi

origin_url() {
  git remote get-url origin 2>/dev/null || true
}

refuse_ssh_url() {
  local url="$1"
  if [[ "$url" == git@* || "$url" == ssh://* || "$url" == *://git@* ]]; then
    echo "origin phải là HTTPS, không được SSH" >&2
    exit 2
  fi
}

if [[ ! -d .git ]]; then
  echo "Chưa phải git repo. Clone HTTPS vào $ROOT trên máy chủ (không scp)." >&2
  exit 3
fi

refuse_ssh_url "$REMOTE_URL"
refuse_ssh_url "$(origin_url)"

git "${GIT_ARGS[@]}" fetch --prune origin "$REF"
# GitHub là nguồn: bỏ chỉnh local trên file tracked (data/, .venv giữ vì untracked/gitignore).
git checkout -f -B "$REF" "origin/${REF}"
git reset --hard "origin/${REF}"

if [[ -x "$ROOT/.venv/bin/pip" ]]; then
  "$ROOT/.venv/bin/pip" install -q -r "$ROOT/requirements.txt" || echo "pip: bo qua (thieu goi moi)" >&2
elif command -v python3 >/dev/null 2>&1; then
  python3 -m pip install -q -r "$ROOT/requirements.txt" || echo "pip: bo qua (thieu goi moi)" >&2
fi

# Bật cổng trước, rồi mới tải Full.exe (~120MB) để /healthz không đứng 502.
if [[ "${MOS_RESTART:-1}" == "1" ]]; then
  if command -v systemctl >/dev/null 2>&1; then
    systemctl restart mos.service 2>/dev/null || sudo -n systemctl restart mos.service 2>/dev/null || true
  fi
fi

if [[ "${MOS_SYNC_INSTALLERS:-1}" == "1" ]]; then
  bash "$ROOT/scripts/sync-installers.sh" || echo "sync-installers: bo qua (khong chan git pull)" >&2
fi

git rev-parse --short HEAD
git log -1 --oneline
