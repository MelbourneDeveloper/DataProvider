# Healthcare Samples

A complete demonstration of the DataProvider suite: three FHIR-compliant microservices with bidirectional sync, semantic search, and a React dashboard.

This sample showcases:
- **DataProvider** - Compile-time safe SQL queries for all database operations
- **Sync Framework** - Bidirectional data synchronization between Clinical and Scheduling domains
- **LQL** - Lambda Query Language for complex queries
- **RAG Search** - Semantic medical code search with pgvector embeddings
- **FHIR Compliance**
   - All medical data follows [FHIR R5 spec](https://build.fhir.org/resourcelist.html)
   - Follows the FHIR [access control rules](https://build.fhir.org/security.html).

## Quick Start

```bash
# Run all APIs locally against Docker Postgres
./scripts/start-local.sh

# Run everything in Docker containers
./scripts/start.sh

# Run APIs + sync workers
./scripts/start.sh --sync
```

| Service | URL |
|---------|-----|
| Clinical API | http://localhost:5080 |
| Scheduling API | http://localhost:5001 |
| ICD10 API | http://localhost:5090 |
| Dashboard | http://localhost:8080 |

## Architecture

```
Dashboard.Web (React/H5)
       |
       +--> Clinical.Api <---- Clinical.Sync <-+
       |    (PostgreSQL)                       |
       |    fhir_Patient, fhir_Encounter       | Practitioner->Provider
       |                                       |
       +--> Scheduling.Api <-- Scheduling.Sync <+
       |    (PostgreSQL)       Patient->ScheduledPatient
       |    fhir_Practitioner, fhir_Appointment
       |
       +--> ICD10.Api
            (PostgreSQL + pgvector)
            icd10_code, achi_code, embeddings
```

## Data Ownership

| Domain | Owns | Receives via Sync |
|--------|------|-------------------|
| Clinical | fhir_Patient, fhir_Encounter, fhir_Condition, fhir_MedicationRequest | sync_Provider |
| Scheduling | fhir_Practitioner, fhir_Appointment, fhir_Schedule, fhir_Slot | sync_ScheduledPatient |
| ICD10 | icd10_chapter, icd10_block, icd10_category, icd10_code, achi_block, achi_code | N/A (read-only reference) |

## API Endpoints

### Clinical (`:5080`)
- `GET/POST /fhir/Patient` - Patients
- `GET /fhir/Patient/_search?q=smith` - Search
- `GET/POST /fhir/Patient/{id}/Encounter` - Encounters
- `GET/POST /fhir/Patient/{id}/Condition` - Conditions
- `GET/POST /fhir/Patient/{id}/MedicationRequest` - Medications
- `GET /sync/changes?fromVersion=0` - Sync feed

### Scheduling (`:5001`)
- `GET/POST /Practitioner` - Practitioners
- `GET /Practitioner/_search?specialty=cardiology` - Search
- `GET/POST /Appointment` - Appointments
- `PATCH /Appointment/{id}/status` - Update status
- `GET /sync/changes?fromVersion=0` - Sync feed

### ICD10 (`:5090`)
- `GET /api/icd10/chapters` - ICD-10 chapters
- `GET /api/icd10/chapters/{id}/blocks` - Blocks within chapter
- `GET /api/icd10/blocks/{id}/categories` - Categories within block
- `GET /api/icd10/categories/{id}/codes` - Codes within category
- `GET /api/icd10/codes/{code}` - Direct code lookup (supports `?format=fhir`)
- `GET /api/icd10/codes?q={query}&limit=20` - Text search
- `GET /api/achi/blocks` - ACHI procedure blocks
- `GET /api/achi/codes/{code}` - ACHI code lookup
- `GET /api/achi/codes?q={query}&limit=20` - ACHI text search
- `POST /api/search` - RAG semantic search (requires embedding service)
- `GET /health` - Health check

## Dashboard

Serve static files and open http://localhost:8080:

```bash
cd Dashboard/Dashboard.Web/wwwroot
python3 -m http.server 8080
```

Built with H5 transpiler (C#->JavaScript) + React 18.

## Project Structure

```
Samples/
+-- scripts/
|   +-- start.sh                # Docker startup script
|   +-- start-local.sh          # Local dev startup script
|   +-- clean.sh                # Clean Docker environment
|   +-- clean-local.sh          # Clean local environment
+-- Clinical/
|   +-- Clinical.Api/           # REST API (PostgreSQL)
|   +-- Clinical.Api.Tests/     # E2E tests
|   +-- Clinical.Sync/          # Pulls from Scheduling
+-- Scheduling/
|   +-- Scheduling.Api/         # REST API (PostgreSQL)
|   +-- Scheduling.Api.Tests/   # E2E tests
|   +-- Scheduling.Sync/        # Pulls from Clinical
+-- ICD10/
|   +-- ICD10.Api/              # REST API (PostgreSQL + pgvector)
|   +-- ICD10.Api.Tests/        # E2E tests
|   +-- ICD10.Cli/              # Interactive TUI client
|   +-- ICD10.Cli.Tests/        # CLI E2E tests
|   +-- embedding-service/      # Python FastAPI embedding service
|   +-- scripts/                # DB import + embedding generation
+-- Dashboard/
    +-- Dashboard.Web/          # React UI (H5)
```

## Tech Stack

- .NET 9, ASP.NET Core Minimal API
- PostgreSQL with pgvector (semantic search)
- DataProvider (SQL->extension methods)
- Sync Framework (bidirectional sync)
- LQL (Lambda Query Language)
- MedEmbed (medical text embeddings)
- H5 transpiler + React 18

## Testing

```bash
# Run all sample tests
dotnet test --filter "FullyQualifiedName~Samples"

# ICD10 RAG search tests (requires embedding service)
cd ICD10/scripts/Dependencies && ./start.sh
dotnet test --filter "FullyQualifiedName~ICD10.Api.Tests"

# Integration tests (requires APIs running)
dotnet test --filter "FullyQualifiedName~Dashboard.Integration.Tests"
```

## Learn More

- [DataProvider Documentation](../DataProvider/README.md)
- [Sync Framework Documentation](../Sync/README.md)
- [LQL Documentation](../Lql/README.md)
