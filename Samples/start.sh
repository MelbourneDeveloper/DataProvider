#!/bin/bash
# Healthcare Samples - Startup Script
# Usage: ./start.sh [--fresh]
#   --fresh: Delete databases and start with clean test data

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

FRESH=false
if [ "$1" = "--fresh" ]; then
    FRESH=true
fi

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
kill_port 5002
kill_port 5173

# Delete databases if --fresh flag is set
if [ "$FRESH" = true ]; then
    echo "Clearing databases (fresh start)..."
    rm -f "$SCRIPT_DIR/Clinical/Clinical.Api/bin/Debug/net9.0/clinical.db" 2>/dev/null || true
    rm -f "$SCRIPT_DIR/Clinical/Clinical.Api/bin/Release/net9.0/clinical.db" 2>/dev/null || true
    rm -f "$SCRIPT_DIR/Scheduling/Scheduling.Api/bin/Debug/net9.0/scheduling_*.db" 2>/dev/null || true
    rm -f "$SCRIPT_DIR/Scheduling/Scheduling.Api/bin/Release/net9.0/scheduling_*.db" 2>/dev/null || true
    rm -f "$SCRIPT_DIR/../Gatekeeper/Gatekeeper.Api/bin/Debug/net9.0/gatekeeper.db" 2>/dev/null || true
    rm -f "$SCRIPT_DIR/../Gatekeeper/Gatekeeper.Api/bin/Release/net9.0/gatekeeper.db" 2>/dev/null || true
    echo "Databases cleared."
else
    echo "Keeping existing databases (use --fresh to clear)."
fi

echo "Starting services..."

# Start Clinical API
cd "$SCRIPT_DIR/Clinical/Clinical.Api"
dotnet run --urls "http://localhost:5080" &

# Start Scheduling API
cd "$SCRIPT_DIR/Scheduling/Scheduling.Api"
dotnet run --urls "http://localhost:5001" &

# Start Gatekeeper API (auth/access control)
cd "$SCRIPT_DIR/../Gatekeeper/Gatekeeper.Api"
dotnet run --urls "http://localhost:5002" &

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
echo "  Clinical API:   http://localhost:5080"
echo "  Scheduling API: http://localhost:5001"
echo "  Gatekeeper API: http://localhost:5002"
echo "  Dashboard:      http://localhost:5173"
echo ""
echo "Press Ctrl+C to stop all services"

wait
