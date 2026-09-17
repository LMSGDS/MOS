#!/usr/bin/env bash
# Bring up an OpenVPN client from Cursor/GitHub secrets. Never prints secret values.
set -euo pipefail

if [[ -z "${OPENVPN_CONFIG:-}" ]]; then
  echo "Missing OPENVPN_CONFIG (paste the full .ovpn file into this secret)." >&2
  exit 1
fi

if ! command -v openvpn >/dev/null 2>&1; then
  echo "openvpn is not installed." >&2
  exit 1
fi

STATE_DIR="${MOS_VPN_STATE_DIR:-/tmp/mos-openvpn}"
mkdir -p "${STATE_DIR}"
chmod 700 "${STATE_DIR}"
CFG="${STATE_DIR}/client.ovpn"
AUTH="${STATE_DIR}/auth.txt"
LOG="${STATE_DIR}/openvpn.log"
PID_FILE="${STATE_DIR}/openvpn.pid"

umask 077
printf '%s\n' "${OPENVPN_CONFIG}" > "${CFG}"

if [[ -n "${OPENVPN_USERNAME:-}" || -n "${OPENVPN_PASSWORD:-}" ]]; then
  if [[ -z "${OPENVPN_USERNAME:-}" || -z "${OPENVPN_PASSWORD:-}" ]]; then
    echo "Set both OPENVPN_USERNAME and OPENVPN_PASSWORD, or neither." >&2
    exit 1
  fi
  printf '%s\n%s\n' "${OPENVPN_USERNAME}" "${OPENVPN_PASSWORD}" > "${AUTH}"
  if grep -qE '^[[:space:]]*auth-user-pass([[:space:]]|$)' "${CFG}"; then
    sed -i 's|^[[:space:]]*auth-user-pass.*|auth-user-pass '"${AUTH}"'|' "${CFG}"
  else
    printf '\nauth-user-pass %s\n' "${AUTH}" >> "${CFG}"
  fi
fi

EXTRA=()
if [[ "${MOS_OVPN_FULL_TUNNEL:-0}" != "1" ]]; then
  # Keep GitHub/Cursor connectivity unless a full tunnel is requested.
  EXTRA+=(--pull-filter ignore redirect-gateway)
fi

if [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" 2>/dev/null; then
  echo "OpenVPN already running (pid $(cat "${PID_FILE}"))."
  exit 0
fi

: > "${LOG}"
sudo openvpn \
  --config "${CFG}" \
  --cd "${STATE_DIR}" \
  --writepid "${PID_FILE}" \
  --log-append "${LOG}" \
  --auth-nocache \
  --daemon mos-openvpn \
  "${EXTRA[@]}"

for _ in $(seq 1 30); do
  if grep -q "Initialization Sequence Completed" "${LOG}" 2>/dev/null; then
    echo "OpenVPN connected."
    ip -br a | grep -E 'tun|tap' || true
    exit 0
  fi
  if grep -Eqi "AUTH_FAILED|Cannot resolve|TLS Error|Cannot open TUN/TAP" "${LOG}" 2>/dev/null; then
    echo "OpenVPN failed. Last log lines:" >&2
    tail -n 20 "${LOG}" >&2
    exit 1
  fi
  sleep 1
done

echo "OpenVPN did not become ready in 30s. Last log lines:" >&2
tail -n 40 "${LOG}" >&2
exit 1
