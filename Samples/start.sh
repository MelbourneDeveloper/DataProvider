#!/bin/bash
# Healthcare Samples - Docker Compose wrapper
# Usage: ./start.sh [--fresh] [--build]
#   --fresh: Drop volumes and start clean
#   --build: Force rebuild containers

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

FRESH=false
BUILD=""

for arg in "$@"; do
    case $arg in
        --fresh) FRESH=true ;;
        --build) BUILD="--build" ;;
    esac
done

# Kill anything on our ports before Docker binds them
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
kill_port 5432
kill_port 5002
kill_port 5080
kill_port 5001
kill_port 5090
kill_port 5173

# Build Dashboard locally (H5 transpiler doesn't work in Docker Linux)
echo "Building Dashboard locally (H5 requires native build)..."
cd "$SCRIPT_DIR/Dashboard/Dashboard.Web"
dotnet publish -c Release -o "$SCRIPT_DIR/docker/dashboard-build" --nologo -v q
echo "Dashboard built successfully"

cd "$SCRIPT_DIR/docker"

if [ "$FRESH" = true ]; then
    echo "Fresh start - removing volumes..."
    docker compose down -v
fi

echo "Starting services..."
docker compose up $BUILD

# 3 containers:
#   db:        Postgres with all databases (localhost:5432)
#   app:       All .NET APIs + sync workers
#              - Gatekeeper:  localhost:5002
#              - Clinical:    localhost:5080
#              - Scheduling:  localhost:5001
#              - ICD10:       localhost:5090
#   dashboard: Static files (localhost:5173)
