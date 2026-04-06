---
layout: layouts/docs.njk
title: Healthcare Samples - FHIR Microservices with ICD-10 RAG Search
description: Three FHIR-compliant .NET microservices with bidirectional sync, ICD-10 semantic search using MedEmbed embeddings and pgvector, and a React dashboard.
---

A complete demonstration of the DataProvider suite: three FHIR-compliant microservices with bidirectional sync, semantic search, and a React dashboard.

The Healthcare Samples live in their own repository: [MelbourneDeveloper/HealthcareSamples](https://github.com/MelbourneDeveloper/HealthcareSamples).

## What It Demonstrates

- **DataProvider** - Compile-time safe SQL queries for all database operations
- **Sync Framework** - Bidirectional data synchronization between Clinical and Scheduling domains
- **LQL** - Lambda Query Language for complex queries
- **RAG Search** - Semantic medical code search with pgvector embeddings
- **FHIR Compliance** - All medical data follows [FHIR R5 spec](https://build.fhir.org/resourcelist.html)

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

## Services

| Service | URL | Purpose |
|---------|-----|---------|
| Clinical API | http://localhost:5080 | Patient, Encounter, Condition management |
| Scheduling API | http://localhost:5001 | Practitioner, Appointment management |
| ICD10 API | http://localhost:5090 | Medical code search with RAG |
| Dashboard | http://localhost:8080 | React UI (H5 transpiler) |

## NuGet Packages Used

The Healthcare Samples consume DataProvider toolkit packages from NuGet:

| Package | Purpose |
|---------|---------|
| `MelbourneDev.DataProvider` | Source-generated SQL extension methods |
| `MelbourneDev.Lql.Postgres` | LQL transpilation to PostgreSQL |
| `MelbourneDev.Sync` | Core sync framework |
| `MelbourneDev.Sync.Postgres` | PostgreSQL sync provider |
| `MelbourneDev.Migration` | YAML schema migrations |
| `MelbourneDev.Migration.Postgres` | PostgreSQL DDL generation |
| `MelbourneDev.Selecta` | SQL result formatting |

## ICD-10 Microservice

The ICD-10 microservice provides clinical coders with RAG (Retrieval-Augmented Generation) search capabilities and standard lookup functionality for ICD-10 diagnosis codes.

### Country-Agnostic Design

The service uses a unified schema that supports any ICD-10 variant:

| Variant | Country | Data Source |
|---------|---------|-------------|
| **ICD-10-CM** | USA | CMS.gov (FREE) |
| **ICD-10-AM** | Australia | IHACPA (Licensed) |
| **ICD-10-GM** | Germany | BfArM |
| **ICD-10-CA** | Canada | CIHI |

### RAG Search

Semantic search using MedEmbed medical embeddings. A clinical coder enters natural language like "chest pain with shortness of breath" and receives ranked ICD-10 code suggestions with confidence scores.

```json
POST /api/search
{
  "query": "chest pain with shortness of breath",
  "limit": 10,
  "format": "json"
}
```

Response:
```json
{
  "results": [
    {
      "code": "R07.4",
      "description": "Chest pain, unspecified",
      "confidence": 0.92
    },
    {
      "code": "R06.0",
      "description": "Dyspnoea",
      "confidence": 0.87
    }
  ],
  "model": "MedEmbed-Large-v1"
}
```

### ICD-10 API Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /api/icd10/chapters` | ICD-10 chapters |
| `GET /api/icd10/chapters/{id}/blocks` | Blocks within chapter |
| `GET /api/icd10/codes/{code}` | Direct code lookup (supports `?format=fhir`) |
| `GET /api/icd10/codes?q={query}` | Text search |
| `POST /api/search` | RAG semantic search |
| `GET /api/achi/codes/{code}` | ACHI procedure code lookup |

## Data Ownership

| Domain | Owns | Receives via Sync |
|--------|------|-------------------|
| Clinical | Patient, Encounter, Condition, MedicationRequest | Provider |
| Scheduling | Practitioner, Appointment, Schedule, Slot | ScheduledPatient |
| ICD10 | Chapters, Blocks, Categories, Codes | N/A (read-only reference) |

## Quick Start

See the [HealthcareSamples repository](https://github.com/MelbourneDeveloper/HealthcareSamples) for full setup instructions.

## Tech Stack

- .NET 10, ASP.NET Core Minimal API
- PostgreSQL with pgvector (semantic search)
- DataProvider (SQL to extension methods)
- Sync Framework (bidirectional sync)
- LQL (Lambda Query Language)
- MedEmbed (medical text embeddings)
- H5 transpiler + React 18

## Next Steps

- [DataProvider](/docs/dataprovider/) - Source-generated SQL
- [Sync](/docs/sync/) - Offline-first synchronization
- [LQL](/docs/lql/) - Cross-database query language
- [Gatekeeper](/docs/gatekeeper/) - Authentication and authorization
