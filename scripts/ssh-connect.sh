#!/usr/bin/env bash
# SSH through the current network (call scripts/openvpn-up.sh first when the host is private).
set -euo pipefail

if [[ -z "${SSH_HOST:-}" || -z "${SSH_USER:-}" || -z "${SSH_PRIVATE_KEY:-}" ]]; then
  echo "Missing SSH_HOST, SSH_USER, or SSH_PRIVATE_KEY." >&2
  exit 1
fi

STATE_DIR="${MOS_VPN_STATE_DIR:-/tmp/mos-openvpn}"
mkdir -p "${STATE_DIR}"
chmod 700 "${STATE_DIR}"
KEY="${STATE_DIR}/ssh_key"
umask 077
printf '%s\n' "${SSH_PRIVATE_KEY}" > "${KEY}"
chmod 600 "${KEY}"

PORT="${SSH_PORT:-22}"
exec ssh \
  -i "${KEY}" \
  -p "${PORT}" \
  -o IdentitiesOnly=yes \
  -o StrictHostKeyChecking=accept-new \
  -o ConnectTimeout=15 \
  "${SSH_USER}@${SSH_HOST}" \
  "$@"
