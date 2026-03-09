# Single container for ALL .NET services + embedding service
# Runs: Gatekeeper API, Clinical API, Scheduling API, ICD10 API, Clinical Sync, Scheduling Sync, Embedding Service

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet publish Gatekeeper/Gatekeeper.Api -c Release -o /app/gatekeeper
RUN dotnet publish Samples/Clinical/Clinical.Api -c Release -o /app/clinical-api
RUN dotnet publish Samples/Scheduling/Scheduling.Api -c Release -o /app/scheduling-api
RUN dotnet publish Samples/ICD10/ICD10.Api -c Release -o /app/icd10-api
RUN dotnet publish Samples/Clinical/Clinical.Sync -c Release -o /app/clinical-sync
RUN dotnet publish Samples/Scheduling/Scheduling.Sync -c Release -o /app/scheduling-sync

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install Python for embedding service and ICD-10 import
RUN apt-get update && apt-get install -y \
    curl \
    python3 \
    python3-pip \
    python3-venv \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
RUN mkdir -p /app/logs

# Copy all published apps
COPY --from=build /app/gatekeeper ./gatekeeper
COPY --from=build /app/clinical-api ./clinical-api
COPY --from=build /app/scheduling-api ./scheduling-api
COPY --from=build /app/icd10-api ./icd10-api
COPY --from=build /app/clinical-sync ./clinical-sync
COPY --from=build /app/scheduling-sync ./scheduling-sync

# Copy embedding service
COPY Samples/ICD10/embedding-service/main.py ./embedding-service/main.py
COPY Samples/ICD10/embedding-service/requirements.txt ./embedding-service/requirements.txt

# Copy ICD-10 import script
COPY Samples/ICD10/scripts/CreateDb/import_postgres.py ./import_icd10.py

# Install Python dependencies (embedding service + import script)
RUN pip install --break-system-packages --no-cache-dir \
    psycopg2-binary requests click \
    fastapi uvicorn sentence-transformers torch pydantic numpy

# Pre-download the model so startup is fast
RUN python3 -c "from sentence_transformers import SentenceTransformer; SentenceTransformer('abhinand/MedEmbed-small-v0.1')"

# Copy entrypoint script
COPY Samples/docker/start-services.sh ./start-services.sh
RUN chmod +x ./start-services.sh

EXPOSE 5002 5080 5001 5090 8000

ENTRYPOINT ["./start-services.sh"]
