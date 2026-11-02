#!/usr/bin/env bash
# Smoke test against a running API: waits for /health, then runs one dispatch flow.
# Usage: scripts/smoke-test.sh http://localhost:8080
set -euo pipefail
BASE="${1:-http://localhost:8080}"

for _ in $(seq 1 60); do
  curl -fsS "$BASE/health" >/dev/null 2>&1 && break
  sleep 2
done
curl -fsS "$BASE/health"; echo

post() { curl -fsS -X "$1" "$BASE$2" -H 'Content-Type: application/json' -d "$3"; }

unit_id=$(post POST /api/units "{\"callSign\":\"RZP $RANDOM\",\"type\":\"Ambulance\"}" | jq -r .id)
incident_id=$(post POST /api/incidents '{"title":"Smoke test","address":"Brno","priority":"High"}' | jq -r .id)
status=$(post POST "/api/incidents/$incident_id/assignments" "{\"unitId\":$unit_id}" | jq -r .status)
[ "$status" = "Dispatched" ] || { echo "Expected Dispatched, got $status"; exit 1; }
status=$(post PUT "/api/incidents/$incident_id/status" '{"status":"Closed"}' | jq -r .status)
[ "$status" = "Closed" ] || { echo "Expected Closed, got $status"; exit 1; }

echo "Smoke test passed against $BASE"
