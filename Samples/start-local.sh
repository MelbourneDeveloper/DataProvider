#!/bin/bash
# Healthcare Samples - Local Development
# Runs all 4 APIs locally against docker-compose.postgres.yml
#
# Prerequisites:
#   docker compose -f docker-compose.postgres.yml up -d
#
# Usage: ./start-local.sh [--fresh]
#   --fresh: Drop postgres volume and recreate

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
PIDS=()

FRESH=false
for arg in "$@"; do
    case $arg in
        --fresh) FRESH=true ;;
    esac
done

# ── Kill anything on our ports ──────────────────────────────────────
kill_port() {
    local port=$1
    local pids
    pids=$(lsof -ti :"$port" 2>/dev/null || true)
    if [ -n "$pids" ]; then
        echo "Killing processes on port $port: $pids"
        echo "$pids" | xargs kill -9 2>/dev/null || true
        sleep 0.5
    fi
}

echo "Clearing ports..."
kill_port 5002
kill_port 5080
kill_port 5001
kill_port 5090
kill_port 5173

# ── Ensure Postgres is running ──────────────────────────────────────
cd "$REPO_ROOT"

if ! pg_isready -h localhost -p 5432 -q 2>/dev/null; then
    echo "Postgres not running. Starting via docker-compose.postgres.yml..."
    if [ "$FRESH" = true ]; then
        docker compose -f docker-compose.postgres.yml down -v 2>/dev/null || true
    fi
    docker compose -f docker-compose.postgres.yml up -d
    echo "Waiting for Postgres..."
    for i in {1..30}; do
        if pg_isready -h localhost -p 5432 -q 2>/dev/null; then
            echo "Postgres ready!"
            break
        fi
        sleep 1
    done
fi

# ── Cleanup on exit ─────────────────────────────────────────────────
cleanup() {
    echo ""
    echo "Shutting down..."
    for pid in "${PIDS[@]}"; do
        kill "$pid" 2>/dev/null || true
    done
    wait 2>/dev/null || true
    echo "All services stopped."
}
trap cleanup EXIT INT TERM

# ── Start APIs ──────────────────────────────────────────────────────
echo ""
DB_PASS="${DB_PASSWORD:-changeme}"

echo "Starting Gatekeeper.Api on :5002..."
ConnectionStrings__Postgres="Host=localhost;Database=gatekeeper;Username=gatekeeper;Password=$DB_PASS" \
    dotnet run --project "$REPO_ROOT/Gatekeeper/Gatekeeper.Api/Gatekeeper.Api.csproj" --no-launch-profile \
    --urls "http://localhost:5002" \
    2>&1 | sed 's/^/  [gatekeeper] /' &
PIDS+=($!)

echo "Starting Clinical.Api on :5080..."
ConnectionStrings__Postgres="Host=localhost;Database=clinical;Username=clinical;Password=$DB_PASS" \
    dotnet run --project "$SCRIPT_DIR/Clinical/Clinical.Api/Clinical.Api.csproj" --no-launch-profile \
    --urls "http://localhost:5080" \
    2>&1 | sed 's/^/  [clinical]   /' &
PIDS+=($!)

echo "Starting Scheduling.Api on :5001..."
ConnectionStrings__Postgres="Host=localhost;Database=scheduling;Username=scheduling;Password=$DB_PASS" \
    dotnet run --project "$SCRIPT_DIR/Scheduling/Scheduling.Api/Scheduling.Api.csproj" --no-launch-profile \
    --urls "http://localhost:5001" \
    2>&1 | sed 's/^/  [scheduling] /' &
PIDS+=($!)

echo "Starting ICD10.Api on :5090..."
ConnectionStrings__Postgres="Host=localhost;Database=icd10;Username=icd10;Password=$DB_PASS" \
    dotnet run --project "$SCRIPT_DIR/ICD10/ICD10.Api/ICD10.Api.csproj" --no-launch-profile \
    --urls "http://localhost:5090" \
    2>&1 | sed 's/^/  [icd10]      /' &
PIDS+=($!)

echo "Building Dashboard (H5 transpilation)..."
dotnet build "$SCRIPT_DIR/Dashboard/Dashboard.Web/Dashboard.Web.csproj" -c Release --nologo -v q
echo "Dashboard built. Starting on :5173..."
python3 -m http.server 5173 --directory "$SCRIPT_DIR/Dashboard/Dashboard.Web/wwwroot" \
    2>&1 | sed 's/^/  [dashboard]  /' &
PIDS+=($!)

echo ""
echo "════════════════════════════════════════"
echo "  Gatekeeper:  http://localhost:5002"
echo "  Clinical:    http://localhost:5080"
echo "  Scheduling:  http://localhost:5001"
echo "  ICD10:       http://localhost:5090"
echo "  Dashboard:   http://localhost:5173"
echo "════════════════════════════════════════"
echo "  Press Ctrl+C to stop all services"
echo ""

wait
