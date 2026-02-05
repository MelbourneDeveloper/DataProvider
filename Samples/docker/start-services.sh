#!/bin/bash
set -e

echo "Starting all services..."

# Start embedding service first (needed by ICD10 API for AI search)
cd /app/embedding-service
python3 -m uvicorn main:app --host 0.0.0.0 --port 8000 &
echo "Started embedding service on :8000"

# Gatekeeper API
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

# ICD10 API - point to local embedding service
cd /app/icd10-api
ConnectionStrings__Postgres="$ConnectionStrings__Postgres_ICD10" \
EmbeddingService__BaseUrl="http://localhost:8000" \
ASPNETCORE_URLS=http://+:5090 dotnet ICD10.Api.dll &

echo "Waiting for APIs to initialize..."
sleep 10

# Import ICD-10 data if not already imported
echo "Checking ICD-10 data..."
ICD10_HEALTH=$(curl -s http://localhost:5090/health 2>/dev/null || echo '{"Status":"error"}')

if echo "$ICD10_HEALTH" | grep -q "unhealthy"; then
    echo "ICD-10 database is empty - importing data from CDC..."
    cd /app
    # Convert .NET connection string to psycopg2 format
    # From: Host=db;Database=icd10;Username=icd10;Password=changeme
    # To:   host=db dbname=icd10 user=icd10 password=changeme
    PG_CONN=$(echo "$ConnectionStrings__Postgres_ICD10" | \
        sed 's/Host=/host=/g' | \
        sed 's/Database=/dbname=/g' | \
        sed 's/Username=/user=/g' | \
        sed 's/Password=/password=/g' | \
        sed 's/;/ /g')
    python3 import_icd10.py --connection-string "$PG_CONN" || echo "ICD-10 import failed"
    echo "ICD-10 import complete"
else
    echo "ICD-10 data already loaded"
fi

# Sync workers
cd /app/clinical-sync
dotnet Clinical.Sync.dll &

cd /app/scheduling-sync
dotnet Scheduling.Sync.dll &

echo "All services started"
echo "  Embedding: :8000"
echo "  Gatekeeper: :5002"
echo "  Clinical:   :5080"
echo "  Scheduling: :5001"
echo "  ICD10:      :5090"

wait
