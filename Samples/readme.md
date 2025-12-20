# Healthcare Samples

Two decoupled microservices demonstrating DataProvider, LQL, and domain-specific sync.

## Architecture

```mermaid
graph TB
    subgraph Clinical["Clinical Domain (SQLite)"]
        CA[Clinical.Api<br/>fhir_Patient, fhir_Encounter]
        CS[Clinical.Sync<br/>sync_Provider]
    end

    subgraph Scheduling["Scheduling Domain (PostgreSQL)"]
        SA[Scheduling.Api<br/>fhir_Practitioner, fhir_Appointment]
        SS[Scheduling.Sync<br/>sync_ScheduledPatient]
    end

    CA -->|Patient data| SS
    SA -->|Practitioner data| CS

    style Clinical fill:#e1f5fe
    style Scheduling fill:#fff3e0
```

## Data Ownership & Sync Mapping

Each domain owns its FHIR resources (prefixed `fhir_`) and receives **mapped** copies from other domains (prefixed `sync_`).

| Domain | Owns (fhir_*) | Receives via Sync (sync_*) |
|--------|---------------|----------------------------|
| **Clinical** | fhir_Patient, fhir_Encounter, fhir_Condition, fhir_AllergyIntolerance, fhir_MedicationStatement, fhir_Observation | sync_Provider (mapped from Scheduling's fhir_Practitioner) |
| **Scheduling** | fhir_Practitioner, fhir_Appointment, fhir_Schedule, fhir_Slot | sync_ScheduledPatient (mapped from Clinical's fhir_Patient) |

### Sync Mapping Examples

**Practitioner → Provider** (Scheduling → Clinical):
```
fhir_Practitioner.Id          → sync_Provider.ProviderId
fhir_Practitioner.NameGiven   → sync_Provider.FirstName
fhir_Practitioner.NameFamily  → sync_Provider.LastName
fhir_Practitioner.Specialty   → sync_Provider.Specialty
```

**Patient → ScheduledPatient** (Clinical → Scheduling):
```
fhir_Patient.Id                              → sync_ScheduledPatient.PatientId
concat(GivenName, ' ', FamilyName)           → sync_ScheduledPatient.DisplayName
fhir_Patient.Phone                           → sync_ScheduledPatient.ContactPhone
fhir_Patient.Email                           → sync_ScheduledPatient.ContactEmail
```

## Project Structure

```
Samples/
├── readme.md
├── Clinical/
│   ├── Clinical.Api/          # ASP.NET Core Minimal API (SQLite)
│   │   ├── Program.cs
│   │   ├── Models.cs          # FHIR R4 records
│   │   ├── Queries/*.sql      # External SQL queries
│   │   └── schema.sql         # fhir_* and sync_* tables
│   └── Clinical.Sync/         # Incoming sync from Scheduling
│       ├── Program.cs
│       └── SyncMappings.json  # Practitioner → Provider mapping
├── Scheduling/
│   ├── Scheduling.Api/        # ASP.NET Core Minimal API (PostgreSQL)
│   │   ├── Program.cs
│   │   ├── Models.cs          # FHIR R4 records
│   │   ├── Queries/*.sql      # External SQL queries
│   │   ├── Queries/*.lql      # External LQL queries
│   │   └── schema.sql         # fhir_* and sync_* tables
│   └── Scheduling.Sync/       # Incoming sync from Clinical
│       ├── Program.cs
│       └── SyncMappings.json  # Patient → ScheduledPatient mapping
└── Healthcare.Sync.http       # HTTP test collection
```

## Running the Services

### Prerequisites

- .NET 9.0 SDK
- PostgreSQL (for Scheduling domain) or Docker

### Clinical API (SQLite)

No database setup required - SQLite creates the database automatically.

```bash
cd Samples/Clinical/Clinical.Api
dotnet run
```

Endpoints:
- `GET /fhir/Patient` - List patients
- `GET /fhir/Patient/{id}` - Get patient by ID
- `POST /fhir/Patient` - Create patient
- `GET /fhir/Patient/_search?q=smith` - Search patients
- `GET /sync/changes?fromVersion=0` - Get changes for sync
- `GET /sync/origin` - Get origin ID

### Scheduling API (PostgreSQL)

Requires PostgreSQL. Use Docker:

```bash
docker run -d --name clinic-postgres \
  -e POSTGRES_USER=clinic \
  -e POSTGRES_PASSWORD=clinic123 \
  -e POSTGRES_DB=clinic_scheduling \
  -p 5432:5432 \
  postgres:16

cd Samples/Scheduling/Scheduling.Api
dotnet run
```

Or set `POSTGRES_CONNECTION` environment variable.

Endpoints:
- `GET /Practitioner` - List practitioners
- `GET /Practitioner/{id}` - Get practitioner by ID
- `POST /Practitioner` - Create practitioner
- `GET /Appointment` - List upcoming appointments
- `POST /Appointment` - Create appointment
- `GET /sync/changes?fromVersion=0` - Get changes for sync
- `GET /sync/origin` - Get origin ID

### Running Sync Workers

Start the sync workers to pull data between domains:

```bash
# Pull Practitioner data into Clinical domain
cd Samples/Clinical/Clinical.Sync
dotnet run

# Pull Patient data into Scheduling domain
cd Samples/Scheduling/Scheduling.Sync
dotnet run
```

## FHIR Compliance

All entities follow [FHIR R4](https://build.fhir.org/resourcelist.html) resource definitions:

| Resource | Domain | Description |
|----------|--------|-------------|
| Patient | Clinical | Demographics, contact info, address |
| Encounter | Clinical | Patient visits/admissions |
| Condition | Clinical | Diagnoses (ICD-10 codes) |
| AllergyIntolerance | Clinical | Allergies and intolerances |
| MedicationStatement | Clinical | Current medications (RxNorm) |
| Observation | Clinical | Lab results (LOINC codes) |
| Practitioner | Scheduling | Healthcare providers (NPI) |
| Appointment | Scheduling | Scheduled encounters |
| Schedule | Scheduling | Provider availability |
| Slot | Scheduling | Bookable time slots |

## Query Architecture

All queries are externalized to files (no embedded SQL/LQL strings):

- **`.sql` files** - Raw SQL queries
- **`.lql` files** - LQL (Lambda Query Language) that transpiles to SQL

Example LQL query (`GetUpcomingAppointments.lql`):
```
fhir_Appointment
|> filter status = 'booked'
|> orderBy start_time
|> limit 50
```

## Technology Stack

| Component | Clinical | Scheduling |
|-----------|----------|------------|
| Database | SQLite | PostgreSQL |
| Query Language | SQL | SQL + LQL |
| Sync Library | Sync.SQLite | Sync.Postgres |
| Framework | ASP.NET Core Minimal API | ASP.NET Core Minimal API |

## Web Dashboard

- React app written in C# with Bridge.Net to transpile from C# to JavaScript
- Design should follow standard medical dashboards
- Talks to the Microservices