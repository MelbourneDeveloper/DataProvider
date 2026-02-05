# Single container for ALL .NET services
# Runs: Gatekeeper API, Clinical API, Scheduling API, ICD10 API, Clinical Sync, Scheduling Sync

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy everything and build all projects
COPY . .

# Build all APIs and sync workers
RUN dotnet publish Gatekeeper/Gatekeeper.Api -c Release -o /app/gatekeeper
RUN dotnet publish Samples/Clinical/Clinical.Api -c Release -o /app/clinical-api
RUN dotnet publish Samples/Scheduling/Scheduling.Api -c Release -o /app/scheduling-api
RUN dotnet publish Samples/ICD10/ICD10.Api -c Release -o /app/icd10-api
RUN dotnet publish Samples/Clinical/Clinical.Sync -c Release -o /app/clinical-sync
RUN dotnet publish Samples/Scheduling/Scheduling.Sync -c Release -o /app/scheduling-sync

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy all published apps
COPY --from=build /app/gatekeeper ./gatekeeper
COPY --from=build /app/clinical-api ./clinical-api
COPY --from=build /app/scheduling-api ./scheduling-api
COPY --from=build /app/icd10-api ./icd10-api
COPY --from=build /app/clinical-sync ./clinical-sync
COPY --from=build /app/scheduling-sync ./scheduling-sync

# Copy entrypoint script
COPY Samples/docker/start-services.sh ./start-services.sh
RUN chmod +x ./start-services.sh

EXPOSE 5002 5080 5001 5090

ENTRYPOINT ["./start-services.sh"]
