#!/usr/bin/env bash
# Builds the image, starts Postgres + API with docker compose (migrations applied on startup), waits for
# /health/ready, then runs the end-to-end smoke flow. Pass --down to remove the stack and its volumes afterwards.
set -euo pipefail
cd "$(dirname "$0")/.."

docker compose up -d --build --wait
status=0
node scripts/smoke.mjs || status=$?

if [[ "${1:-}" == "--down" ]]; then
  docker compose down -v
fi
exit "$status"
