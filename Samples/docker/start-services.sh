#!/bin/bash
set -e

echo "Starting all services..."

# Start Gatekeeper API (auth - needed first)
cd /app/gatekeeper
ASPNETCORE_URLS=http://+:5002 dotnet Gatekeeper.Api.dll &
GATEKEEPER_PID=$!

echo "Waiting for Gatekeeper to be ready..."
sleep 5

# Start Clinical API
cd /app/clinical-api
ASPNETCORE_URLS=http://+:5080 dotnet Clinical.Api.dll &

# Start Scheduling API
cd /app/scheduling-api
ASPNETCORE_URLS=http://+:5001 dotnet Scheduling.Api.dll &

# Start ICD10 API
cd /app/icd10-api
ASPNETCORE_URLS=http://+:5090 dotnet ICD10.Api.dll &

echo "Waiting for APIs to be ready..."
sleep 5

# Start Sync workers
cd /app/clinical-sync
dotnet Clinical.Sync.dll &

cd /app/scheduling-sync
dotnet Scheduling.Sync.dll &

echo "All services started!"
echo "  Gatekeeper: http://localhost:5002"
echo "  Clinical:   http://localhost:5080"
echo "  Scheduling: http://localhost:5001"
echo "  ICD10:      http://localhost:5090"

# Wait for any process to exit
wait -n

# Exit with status of process that exited first
exit $?
