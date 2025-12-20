#!/bin/bash
# Healthcare Samples - Startup Script

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

cleanup() {
    echo "Shutting down..."
    kill $(jobs -p) 2>/dev/null || true
    exit 0
}
trap cleanup SIGINT SIGTERM

# Kill processes on ports we need
kill_port() {
    lsof -ti:"$1" | xargs kill -9 2>/dev/null || true
}

echo "Killing existing processes..."
kill_port 5080
kill_port 5001
kill_port 5173

echo "Starting services..."

# Start Clinical API
cd "$SCRIPT_DIR/Clinical/Clinical.Api"
dotnet run --urls "http://localhost:5080" &

# Start Scheduling API
cd "$SCRIPT_DIR/Scheduling/Scheduling.Api"
dotnet run --urls "http://localhost:5001" &

# Build and serve Dashboard
cd "$SCRIPT_DIR/Dashboard/Dashboard.Web"
echo "Building Dashboard..."
dotnet build --configuration Release

# Serve the static files using Python's HTTP server
cd "$SCRIPT_DIR/Dashboard/Dashboard.Web/bin/Release/netstandard2.0/wwwroot"
python3 -m http.server 5173 &

sleep 3

echo ""
echo "Services running:"
echo "  Clinical API:  http://localhost:5080"
echo "  Scheduling API: http://localhost:5001"
echo "  Dashboard:     http://localhost:5173"
echo ""
echo "Press Ctrl+C to stop all services"

wait
