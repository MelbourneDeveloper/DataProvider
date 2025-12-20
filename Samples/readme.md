# Healthcare Samples

Two FHIR-compliant microservices with bidirectional sync and a React dashboard.

## Quick Start

```bash
# Run both APIs
./start.sh

# Run APIs + sync workers
./start.sh --sync
```

| Service | URL |
|---------|-----|
| Clinical API | http://localhost:5080 |
| Scheduling API | http://localhost:5001 |
| Dashboard | http://localhost:8080 |

## Architecture

```
Dashboard.Web (React/H5)
       │
       ├──► Clinical.Api ◄──── Clinical.Sync ◄─┐
       │    (SQLite)                           │
       │    fhir_Patient, fhir_Encounter       │ Practitioner→Provider
       │                                       │
       └──► Scheduling.Api ◄── Scheduling.Sync ◄┘
            (SQLite)           Patient→ScheduledPatient
            fhir_Practitioner, fhir_Appointment
```

## Data Ownership

| Domain | Owns | Receives via Sync |
|--------|------|-------------------|
| Clinical | fhir_Patient, fhir_Encounter, fhir_Condition, fhir_MedicationRequest | sync_Provider |
| Scheduling | fhir_Practitioner, fhir_Appointment, fhir_Schedule, fhir_Slot | sync_ScheduledPatient |

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

## Dashboard

Serve static files and open http://localhost:8080:

```bash
cd Dashboard/Dashboard.Web/wwwroot
python3 -m http.server 8080
```

Built with H5 transpiler (C#→JavaScript) + React 18.

## Project Structure

```
Samples/
├── start.sh                    # Startup script
├── Clinical/
│   ├── Clinical.Api/           # REST API (SQLite)
│   └── Clinical.Sync/          # Pulls from Scheduling
├── Scheduling/
│   ├── Scheduling.Api/         # REST API (SQLite)
│   └── Scheduling.Sync/        # Pulls from Clinical
└── Dashboard/
    └── Dashboard.Web/          # React UI (H5)
```

## Tech Stack

- .NET 9, ASP.NET Core Minimal API
- SQLite (both domains)
- DataProvider (SQL→extension methods)
- LQL (Lambda Query Language)
- H5 transpiler + React 18
