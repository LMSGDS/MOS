#!/usr/bin/env bash
# SSH through the current network (call scripts/openvpn-up.sh first when the host is private).
set -euo pipefail

if [[ -z "${SSH_HOST:-}" || -z "${SSH_USER:-}" ]]; then
  echo "Missing SSH_HOST or SSH_USER." >&2
  exit 1
fi

if [[ -z "${SSH_PRIVATE_KEY:-}" && -z "${SSH_PASSWORD:-}" ]]; then
  echo "Missing SSH_PRIVATE_KEY or SSH_PASSWORD." >&2
  exit 1
fi

STATE_DIR="${MOS_VPN_STATE_DIR:-/tmp/mos-openvpn}"
mkdir -p "${STATE_DIR}"
chmod 700 "${STATE_DIR}"
PORT="${SSH_PORT:-22}"
SSH_OPTS=(-p "${PORT}" -o StrictHostKeyChecking=accept-new -o ConnectTimeout=15)

if [[ -n "${SSH_PRIVATE_KEY:-}" ]]; then
  KEY="${STATE_DIR}/ssh_key"
  umask 077
  printf '%s\n' "${SSH_PRIVATE_KEY}" > "${KEY}"
  chmod 600 "${KEY}"
  exec ssh -i "${KEY}" -o IdentitiesOnly=yes "${SSH_OPTS[@]}" "${SSH_USER}@${SSH_HOST}" "$@"
fi

if ! command -v sshpass >/dev/null 2>&1; then
  echo "sshpass is required for SSH_PASSWORD auth." >&2
  exit 1
fi
export SSHPASS="${SSH_PASSWORD}"
exec sshpass -e ssh \
  -o PreferredAuthentications=password \
  -o PubkeyAuthentication=no \
  "${SSH_OPTS[@]}" \
  "${SSH_USER}@${SSH_HOST}" \
  "$@"
