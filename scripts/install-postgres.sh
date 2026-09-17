#!/usr/bin/env bash
# Cài PostgreSQL 16 cho MOS-KulKul (máy GPU hoặc máy chủ web).
set -euo pipefail
if command -v docker >/dev/null 2>&1; then
  docker compose -f "$(dirname "$0")/../deploy/docker-compose.yml" up -d
  echo "PostgreSQL (docker): postgresql://mos:mos@127.0.0.1:5432/mos"
  exit 0
fi
if command -v apt-get >/dev/null 2>&1; then
  sudo apt-get update -y
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y postgresql postgresql-contrib
  sudo -u postgres psql -v ON_ERROR_STOP=1 <<'SQL'
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'mos') THEN
    CREATE ROLE mos LOGIN PASSWORD 'mos';
  END IF;
END$$;
SQL
  sudo -u postgres psql -v ON_ERROR_STOP=1 -c "SELECT 1 FROM pg_database WHERE datname='mos'" | grep -q 1 \
    || sudo -u postgres createdb -O mos mos
  echo "PostgreSQL (apt): postgresql://mos:mos@127.0.0.1:5432/mos"
  exit 0
fi
echo "Không tìm thấy docker hoặc apt-get để cài PostgreSQL." >&2
exit 1
