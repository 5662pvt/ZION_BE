#!/usr/bin/env bash
# Run one k6 load script against the API via Docker. Usage:
#   ./run.sh <script> <label> [extra k6 args]
#   <script>: search | product-detail | place-order
#   <label>:  tag used in the result filename and the k6 LABEL env var (e.g. cache-on, cache-off)
#
# Example:
#   BASE_URL=http://host.docker.internal:8080 RATE=50 DURATION=60s ./run.sh search cache-on
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

SCRIPT_NAME="${1:-}"
LABEL="${2:-run}"

if [ -z "$SCRIPT_NAME" ]; then
  echo "Usage: ./run.sh <script> <label> [extra k6 args]" >&2
  echo "  <script>: search | product-detail | place-order" >&2
  exit 1
fi

if [ "$#" -ge 2 ]; then
  shift 2
elif [ "$#" -eq 1 ]; then
  shift 1
fi

mkdir -p "$SCRIPT_DIR/results"

HOST_DIR="$(pwd -W 2>/dev/null || pwd)"

MSYS_NO_PATHCONV=1 docker run --rm -i \
  --add-host=host.docker.internal:host-gateway \
  -e BASE_URL \
  -e RATE \
  -e DURATION \
  -e USERS \
  -e PREALLOC_VUS \
  -e MAX_VUS \
  -e PRODUCT_COUNT \
  -e ADMIN_EMAIL \
  -e ADMIN_PASSWORD \
  -e LOAD_USER_PASSWORD \
  -e LABEL="$LABEL" \
  -v "${HOST_DIR}/k6:/scripts" \
  -v "${HOST_DIR}/results:/results" \
  grafana/k6:0.54.0 run "$@" "/scripts/${SCRIPT_NAME}.js"
