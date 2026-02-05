#!/bin/bash
# Healthcare Samples - Docker Compose wrapper
# Usage: ./start.sh [--fresh] [--build]
#   --fresh: Drop volumes and start clean
#   --build: Force rebuild containers

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/docker"

FRESH=false
BUILD=""

for arg in "$@"; do
    case $arg in
        --fresh) FRESH=true ;;
        --build) BUILD="--build" ;;
    esac
done

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
