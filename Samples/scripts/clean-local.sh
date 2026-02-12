#!/bin/bash
# Healthcare Samples - Clean local development environment
# Kills running services and drops the Postgres database volume
# Usage: ./clean-local.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SAMPLES_DIR="$(dirname "$SCRIPT_DIR")"
REPO_ROOT="$(dirname "$SAMPLES_DIR")"

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

echo "Removing Postgres volume..."
cd "$REPO_ROOT"
docker compose -f docker-compose.postgres.yml down -v 2>/dev/null || true

echo "Clean complete."
