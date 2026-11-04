#!/usr/bin/env bash
# Esegue migrazioni e test su un PostgreSQL USA-E-GETTA (container dedicato «privacy-test»).
# Non tocca altri container o database. Uso: ./run_tests.sh [immagine]   (default postgres:17-alpine)
set -euo pipefail
cd "$(dirname "$0")"
IMG="${1:-postgres:17-alpine}"
NAME=privacy-test-sql   # diverso dal DB di sviluppo (start_test_db.sh)
docker rm -f "$NAME" >/dev/null 2>&1 || true
docker run -d --name "$NAME" -e POSTGRES_PASSWORD=x "$IMG" >/dev/null
trap 'docker rm -f "$NAME" >/dev/null 2>&1 || true' EXIT
for i in $(seq 1 60); do docker exec "$NAME" psql -U postgres -tAc "select 1" >/dev/null 2>&1 && break; sleep 1; done
sleep 2
docker exec "$NAME" psql -U postgres -qc "CREATE DATABASE privacy"
for f in 000_bootstrap_roles.sql 001_schema_v1.sql 002_catalogs.sql 003_rules_and_grants.sql 004_disposal_ledger.sql tests/test_v1.sql; do
  docker cp "$f" "$NAME:/tmp/$(basename "$f")"
done
docker exec "$NAME" psql -U postgres -d privacy -v ON_ERROR_STOP=1 -q -f /tmp/000_bootstrap_roles.sql
for f in 001_schema_v1.sql 002_catalogs.sql 003_rules_and_grants.sql 004_disposal_ledger.sql; do
  echo "== migrazione $f"; docker exec "$NAME" psql -U privacy_migrator -d privacy -v ON_ERROR_STOP=1 -q -f "/tmp/$f"
done
echo "== test"
docker exec "$NAME" psql -U postgres -d privacy -v ON_ERROR_STOP=1 -q -f /tmp/test_v1.sql >/dev/null
