#!/usr/bin/env bash
# Ricrea il database dimostrativo VUOTO («privacy_demo») nel contenitore di sviluppo «privacy-test», con schema e cataloghi.
# Solo sviluppo: i dati dimostrativi si caricano poi con «Privacy.App.exe --seed-demo».
set -euo pipefail
cd "$(dirname "$0")"
N=privacy-test
docker exec "$N" psql -U postgres -qc "DROP DATABASE IF EXISTS privacy_demo WITH (FORCE)"
docker exec "$N" psql -U postgres -qc "CREATE DATABASE privacy_demo"
docker exec -i "$N" psql -U postgres -d privacy_demo -q -v ON_ERROR_STOP=1 < 000_bootstrap_roles.sql
docker exec "$N" psql -U postgres -qc "ALTER ROLE privacy_app PASSWORD 'app'"
for f in 001_schema_v1 002_catalogs 003_rules_and_grants 004_disposal_ledger; do
  docker exec -i "$N" psql -U privacy_migrator -d privacy_demo -q -v ON_ERROR_STOP=1 < "$f.sql"
done
echo "privacy_demo pronto (vuoto)"
