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
SAMPLES_DIR="$(dirname "$SCRIPT_DIR")"
REPO_ROOT="$(dirname "$SAMPLES_DIR")"
PIDS=()

for arg in "$@"; do
    case $arg in
        --fresh) "$SCRIPT_DIR/clean-local.sh" ;;
    esac
done

# ── Ensure Postgres is running ──────────────────────────────────────
cd "$REPO_ROOT"

if ! pg_isready -h localhost -p 5432 -q 2>/dev/null; then
    echo "Postgres not running. Starting via docker-compose.postgres.yml..."
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

# ── Set up Python venv (shared by embedding service + import) ─────
VENV_DIR="$SAMPLES_DIR/ICD10/.venv"
EMBED_DIR="$SAMPLES_DIR/ICD10/embedding-service"

echo ""
echo "Setting up Python environment..."
if [ ! -d "$VENV_DIR" ]; then
    python3 -m venv "$VENV_DIR"
fi
"$VENV_DIR/bin/pip" install -q \
    -r "$EMBED_DIR/requirements.txt" \
    psycopg2-binary click requests
echo "Python environment ready."

# ── Start Embedding Service ───────────────────────────────────────
echo "Starting Embedding Service on :8000 (model loading may take a moment)..."
"$VENV_DIR/bin/python" -m uvicorn main:app --host 0.0.0.0 --port 8000 \
    --app-dir "$EMBED_DIR" \
    2>&1 | sed 's/^/  [embedding]  /' &
PIDS+=($!)

# ── ICD10 data population (runs after APIs + embedding are ready) ─
populate_icd10() {
    local CONN_STR="Host=localhost;Database=icd10;Username=icd10;Password=$DB_PASS"
    local SCRIPTS_DIR="$SAMPLES_DIR/ICD10/scripts/CreateDb"

    # Wait for ICD10 API to be ready
    echo "  [icd10-import] Waiting for ICD10 API..."
    for i in {1..60}; do
        if curl -sf http://localhost:5090/health >/dev/null 2>&1; then
            echo "  [icd10-import] ICD10 API is up."
            break
        fi
        sleep 2
    done

    # Wait for embedding service to be ready (needed for AI search)
    echo "  [icd10-import] Waiting for embedding service..."
    for i in {1..120}; do
        if curl -sf http://localhost:8000/health >/dev/null 2>&1; then
            echo "  [icd10-import] Embedding service ready."
            break
        fi
        sleep 2
    done

    # Check if data already exists (query the chapters endpoint)
    local CHAPTERS
    CHAPTERS=$(curl -sf http://localhost:5090/api/icd10/chapters 2>/dev/null || echo "[]")
    if [ "$CHAPTERS" = "[]" ] || [ "$CHAPTERS" = "" ]; then
        echo "  [icd10-import] No ICD10 data found. Running full Postgres import..."
        EMBEDDING_SERVICE_URL="http://localhost:8000" \
            "$VENV_DIR/bin/python" "$SCRIPTS_DIR/import_postgres.py" \
            --connection-string "$CONN_STR" || echo "  [icd10-import] Import encountered errors (check logs above)"
    else
        echo "  [icd10-import] ICD10 codes already populated. Generating missing embeddings..."
        EMBEDDING_SERVICE_URL="http://localhost:8000" \
            "$VENV_DIR/bin/python" "$SCRIPTS_DIR/import_postgres.py" \
            --connection-string "$CONN_STR" --embeddings-only || echo "  [icd10-import] Embedding generation encountered errors"
    fi
}

# ── Build all projects (avoids parallel build contention) ───────────
echo ""
echo "Building all projects..."
dotnet build "$REPO_ROOT/Gatekeeper/Gatekeeper.Api/Gatekeeper.Api.csproj" --nologo -v q
dotnet build "$SAMPLES_DIR/Clinical/Clinical.Api/Clinical.Api.csproj" --nologo -v q
dotnet build "$SAMPLES_DIR/Scheduling/Scheduling.Api/Scheduling.Api.csproj" --nologo -v q
dotnet build "$SAMPLES_DIR/ICD10/ICD10.Api/ICD10.Api.csproj" --nologo -v q
dotnet build "$SAMPLES_DIR/Dashboard/Dashboard.Web/Dashboard.Web.csproj" -c Release --nologo -v q
echo "All projects built."

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

# ── Start APIs (--no-build since we pre-built above) ────────────────
echo ""
DB_PASS="${DB_PASSWORD:-changeme}"

echo "Starting Gatekeeper.Api on :5002..."
ConnectionStrings__Postgres="Host=localhost;Database=gatekeeper;Username=gatekeeper;Password=$DB_PASS" \
    dotnet run --no-build --project "$REPO_ROOT/Gatekeeper/Gatekeeper.Api/Gatekeeper.Api.csproj" --no-launch-profile \
    --urls "http://localhost:5002" \
    2>&1 | sed 's/^/  [gatekeeper] /' &
PIDS+=($!)

echo "Starting Clinical.Api on :5080..."
ConnectionStrings__Postgres="Host=localhost;Database=clinical;Username=clinical;Password=$DB_PASS" \
    dotnet run --no-build --project "$SAMPLES_DIR/Clinical/Clinical.Api/Clinical.Api.csproj" --no-launch-profile \
    --urls "http://localhost:5080" \
    2>&1 | sed 's/^/  [clinical]   /' &
PIDS+=($!)

echo "Starting Scheduling.Api on :5001..."
ConnectionStrings__Postgres="Host=localhost;Database=scheduling;Username=scheduling;Password=$DB_PASS" \
    dotnet run --no-build --project "$SAMPLES_DIR/Scheduling/Scheduling.Api/Scheduling.Api.csproj" --no-launch-profile \
    --urls "http://localhost:5001" \
    2>&1 | sed 's/^/  [scheduling] /' &
PIDS+=($!)

echo "Starting ICD10.Api on :5090..."
ConnectionStrings__Postgres="Host=localhost;Database=icd10;Username=icd10;Password=$DB_PASS" \
    dotnet run --no-build --project "$SAMPLES_DIR/ICD10/ICD10.Api/ICD10.Api.csproj" --no-launch-profile \
    --urls "http://localhost:5090" \
    2>&1 | sed 's/^/  [icd10]      /' &
PIDS+=($!)

echo "Starting Dashboard on :5173..."
python3 -m http.server 5173 --directory "$SAMPLES_DIR/Dashboard/Dashboard.Web/wwwroot" \
    2>&1 | sed 's/^/  [dashboard]  /' &
PIDS+=($!)

# Populate ICD10 data in background (waits for API, then imports if empty)
populate_icd10 &
PIDS+=($!)

echo ""
echo "════════════════════════════════════════"
echo "  Gatekeeper:  http://localhost:5002"
echo "  Clinical:    http://localhost:5080"
echo "  Scheduling:  http://localhost:5001"
echo "  ICD10:       http://localhost:5090"
echo "  Embedding:   http://localhost:8000"
echo "  Dashboard:   http://localhost:5173"
echo "════════════════════════════════════════"
echo "  Press Ctrl+C to stop all services"
echo ""

wait
