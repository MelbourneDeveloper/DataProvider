#!/bin/bash
# Healthcare Samples - Clean Docker environment
# Kills running services and drops all Docker volumes
# Usage: ./clean.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SAMPLES_DIR="$(dirname "$SCRIPT_DIR")"

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

echo "Removing Docker volumes..."
cd "$SAMPLES_DIR/docker"
docker compose down -v

echo "Clean complete."
