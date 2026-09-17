#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
if [[ ! -d .venv ]]; then
  python3 -m venv .venv
fi
.venv/bin/pip install -q -r requirements.txt
exec .venv/bin/python -m uvicorn app.main:app --host 127.0.0.1 --port "${PORT:-8088}" --reload
