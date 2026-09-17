#!/usr/bin/env bash
set -euo pipefail
STATE_DIR="${MOS_VPN_STATE_DIR:-/tmp/mos-openvpn}"
PID_FILE="${STATE_DIR}/openvpn.pid"
if [[ -f "${PID_FILE}" ]]; then
  pid="$(cat "${PID_FILE}")"
  if kill -0 "${pid}" 2>/dev/null; then
    sudo kill "${pid}" || true
    echo "Stopped OpenVPN pid ${pid}."
  fi
  rm -f "${PID_FILE}"
else
  echo "OpenVPN is not running."
fi
