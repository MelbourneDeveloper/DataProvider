# Docker Setup

3 containers. That's it.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                        app                              │
│  Gatekeeper:5002  Clinical:5080  Scheduling:5001       │
│  ICD10:5090       ClinicalSync   SchedulingSync        │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────┼────────────────────────────────┐
│                       db                                │
│  Postgres:5432                                          │
│  ├── gatekeeper                                         │
│  ├── clinical                                           │
│  ├── scheduling                                         │
│  └── icd10                                              │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│                     dashboard                           │
│  nginx:5173 (static H5 files)                          │
└─────────────────────────────────────────────────────────┘
```

## Why This Split?

| Container | Runtime | Why separate |
|-----------|---------|--------------|
| db | Postgres | Stateful. Don't rebuild the database. |
| app | .NET 9 | All APIs tightly coupled. Same codebase, same deploy. |
| dashboard | nginx | Static files. Different runtime. |

## Dashboard Note

H5 transpiler doesn't work in Docker Linux. Build locally first:

```bash
cd Samples/Dashboard/Dashboard.Web
dotnet publish -c Release
```

Then serve the static files however you want (nginx, python, etc).

## Usage

```bash
# Start everything
./scripts/start.sh

# Fresh start (wipe databases)
./scripts/start.sh --fresh

# Rebuild containers
./scripts/start.sh --build
```

## Ports

| Service | Port |
|---------|------|
| Postgres | 5432 |
| Gatekeeper API | 5002 |
| Clinical API | 5080 |
| Scheduling API | 5001 |
| ICD10 API | 5090 |
| Dashboard | 5173 |

## Files

```
docker/
├── docker-compose.yml    # 3 services
├── Dockerfile.app        # All .NET services
├── Dockerfile.dashboard  # nginx + static files
├── start-services.sh     # Entrypoint for app container
├── init-db/
│   └── init.sql          # Creates all 4 databases
└── nginx.conf            # Dashboard config
```
