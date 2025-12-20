#!/bin/bash
# Healthcare Samples - Startup Script
# Runs Clinical API, Scheduling API, and optionally sync workers

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m'

log() { echo -e "${BLUE}[samples]${NC} $1"; }
success() { echo -e "${GREEN}[samples]${NC} $1"; }
error() { echo -e "${RED}[samples]${NC} $1"; }

cleanup() {
    log "Shutting down services..."
    kill $CLINICAL_PID $SCHEDULING_PID $CLINICAL_SYNC_PID $SCHEDULING_SYNC_PID 2>/dev/null || true
    exit 0
}
trap cleanup SIGINT SIGTERM

usage() {
    echo "Usage: $0 [options]"
    echo ""
    echo "Options:"
    echo "  --sync     Also start sync workers"
    echo "  --help     Show this help"
    echo ""
    echo "Services:"
    echo "  Clinical API:    http://localhost:5080"
    echo "  Scheduling API:  http://localhost:5001"
}

RUN_SYNC=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --sync) RUN_SYNC=true; shift ;;
        --help) usage; exit 0 ;;
        *) error "Unknown option: $1"; usage; exit 1 ;;
    esac
done

# Start Clinical API
log "Starting Clinical API on :5080..."
cd "$SCRIPT_DIR/Clinical/Clinical.Api"
dotnet run --urls "http://localhost:5080" &
CLINICAL_PID=$!

# Start Scheduling API
log "Starting Scheduling API on :5001..."
cd "$SCRIPT_DIR/Scheduling/Scheduling.Api"
dotnet run --urls "http://localhost:5001" &
SCHEDULING_PID=$!

# Wait for APIs to be ready
sleep 3

if $RUN_SYNC; then
    log "Starting Clinical Sync worker..."
    cd "$SCRIPT_DIR/Clinical/Clinical.Sync"
    dotnet run &
    CLINICAL_SYNC_PID=$!

    log "Starting Scheduling Sync worker..."
    cd "$SCRIPT_DIR/Scheduling/Scheduling.Sync"
    dotnet run &
    SCHEDULING_SYNC_PID=$!
fi

success "All services started!"
echo ""
echo "  Clinical API:    http://localhost:5080"
echo "  Scheduling API:  http://localhost:5001"
if $RUN_SYNC; then
    echo "  Sync workers:    Running"
fi
echo ""
log "Press Ctrl+C to stop all services"

wait
