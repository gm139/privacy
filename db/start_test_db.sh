#!/usr/bin/env bash
# Avvia (o ricrea) il PostgreSQL di prova «privacy-test» con schema e cataloghi, in ascolto solo su 127.0.0.1:55432.
# Uso: db/start_test_db.sh [stop]. Stringa di connessione per i test .NET:
#   export PRIVACY_TEST_CONN="Host=127.0.0.1;Port=55432;Database=privacy;Username=privacy_app;Password=app"
set -euo pipefail
cd "$(dirname "$0")"
NAME=privacy-test
docker rm -f "$NAME" >/dev/null 2>&1 || true
[ "${1:-}" = "stop" ] && exit 0
docker run -d --name "$NAME" -p 127.0.0.1:55432:5432 -e POSTGRES_PASSWORD=x postgres:17-alpine >/dev/null
for i in $(seq 1 60); do docker exec "$NAME" psql -U postgres -tAc "select 1" >/dev/null 2>&1 && break; sleep 1; done
sleep 2
docker exec "$NAME" psql -U postgres -qc "CREATE DATABASE privacy"
for f in 000_bootstrap_roles.sql 001_schema_v1.sql 002_catalogs.sql 003_rules_and_grants.sql; do docker cp "$f" "$NAME:/tmp/$f"; done
docker exec "$NAME" psql -U postgres -d privacy -v ON_ERROR_STOP=1 -q -f /tmp/000_bootstrap_roles.sql
docker exec "$NAME" psql -U postgres -qc "ALTER ROLE privacy_app PASSWORD 'app'"
for f in 001_schema_v1.sql 002_catalogs.sql 003_rules_and_grants.sql; do
  docker exec "$NAME" psql -U privacy_migrator -d privacy -v ON_ERROR_STOP=1 -q -f "/tmp/$f"
done
echo "pronto: Host=127.0.0.1;Port=55432;Database=privacy;Username=privacy_app;Password=app"
