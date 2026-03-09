#!/bin/bash
set -e

echo "Starting all services..."

# Start embedding service first (needed by ICD10 API for AI search)
cd /app/embedding-service
python3 -m uvicorn main:app --host 0.0.0.0 --port 8000 &
echo "Started embedding service on :8000"

# Wait for embedding service to be ready (model loading takes time)
echo "Waiting for embedding service to load model..."
for i in {1..60}; do
    if curl -s http://localhost:8000/health | grep -q "healthy"; then
        echo "Embedding service ready!"
        break
    fi
    echo "  Waiting for embedding model... ($i/60)"
    sleep 5
done

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

# Check for embeddings via direct database query
EMBEDDING_COUNT=$(PGPASSWORD=changeme psql -h db -U postgres -d icd10 -t -c "SELECT COUNT(*) FROM icd10_code_embedding;" 2>/dev/null | tr -d ' ' || echo "0")
CODE_COUNT=$(PGPASSWORD=changeme psql -h db -U postgres -d icd10 -t -c "SELECT COUNT(*) FROM icd10_code;" 2>/dev/null | tr -d ' ' || echo "0")

echo "  Codes loaded: $CODE_COUNT"
echo "  Embeddings: $EMBEDDING_COUNT"

NEED_IMPORT=false

if echo "$ICD10_HEALTH" | grep -q "unhealthy"; then
    echo "ICD-10 codes not loaded - need full import"
    NEED_IMPORT=true
elif [ "$EMBEDDING_COUNT" = "0" ] && [ "$CODE_COUNT" != "0" ]; then
    echo "ICD-10 codes loaded but no embeddings - need to generate embeddings"
    NEED_IMPORT=true
fi

if [ "$NEED_IMPORT" = "true" ]; then
    echo "Starting ICD-10 import from CDC..."
    echo "This will take several minutes (downloading + generating embeddings)..."
    cd /app
    EMBEDDING_SERVICE_URL="http://localhost:8000" \
        python3 import_icd10.py --connection-string "$ConnectionStrings__Postgres_ICD10" \
        || echo "ICD-10 import failed"
    echo "ICD-10 import complete with embeddings"
else
    echo "ICD-10 data and embeddings already loaded"
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
