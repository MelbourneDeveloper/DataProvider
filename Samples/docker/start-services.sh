#!/bin/bash
set -e

echo "Starting all services..."

# Gatekeeper API (auth - needed first)
# Uses ConnectionStrings__Postgres from environment
cd /app/gatekeeper
ASPNETCORE_URLS=http://+:5002 dotnet Gatekeeper.Api.dll &

echo "Waiting for Gatekeeper..."
sleep 5

# Clinical API
cd /app/clinical-api
ConnectionStrings__Postgres="$ConnectionStrings__Postgres_Clinical" \
ASPNETCORE_URLS=http://+:5080 dotnet Clinical.Api.dll &

# Scheduling API
cd /app/scheduling-api
ConnectionStrings__Postgres="$ConnectionStrings__Postgres_Scheduling" \
ASPNETCORE_URLS=http://+:5001 dotnet Scheduling.Api.dll &

# ICD10 API
cd /app/icd10-api
ConnectionStrings__Postgres="$ConnectionStrings__Postgres_ICD10" \
ASPNETCORE_URLS=http://+:5090 dotnet ICD10.Api.dll &

echo "Waiting for APIs..."
sleep 5

# Sync workers
cd /app/clinical-sync
dotnet Clinical.Sync.dll &

cd /app/scheduling-sync
dotnet Scheduling.Sync.dll &

echo "All services started"
echo "  Gatekeeper: :5002"
echo "  Clinical:   :5080"
echo "  Scheduling: :5001"
echo "  ICD10:      :5090"

# Keep container running - wait for all background jobs
wait
