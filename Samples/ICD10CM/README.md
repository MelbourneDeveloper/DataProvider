# ICD-10-CM Microservice

RAG semantic search for 74,260 ICD-10-CM diagnosis codes. Pure C# with ONNX Runtime - no Python at runtime.

## Quick Start

```bash
# First time: create database and import codes
./scripts/CreateDb/run.sh

# Run the service
./scripts/run.sh
```

## Scripts

```
scripts/
├── run.sh                 # Run the API (exports ONNX model if needed)
├── Dependencies/          # Docker services
│   ├── start.sh           # Start embedding service
│   ├── stop.sh            # Stop embedding service
│   └── embedding_service.py
└── CreateDb/              # First-time database setup
    ├── run.sh             # Migrate + import + embeddings
    ├── import_icd10cm.py  # Import codes from CMS.gov
    ├── generate_embeddings.py
    ├── generate_sample_data.py
    └── requirements.txt
```

| Script/Folder | Purpose |
|---------------|---------|
| `run.sh` | Run the API and dependencies |
| `Dependencies/` | Start/stop Docker services (embedding service) |
| `CreateDb/` | One-time setup: migrate schema, import codes, generate embeddings |

## Test It

```bash
# Health check
curl http://localhost:5558/health

# RAG semantic search
curl -X POST http://localhost:5558/api/search \
  -H "Content-Type: application/json" \
  -d '{"Query": "chest pain with shortness of breath", "Limit": 10}'

# Direct code lookup
curl http://localhost:5558/api/codes/R07.4
```

## Run E2E Tests

```bash
cd ICD10AM.Api.Tests
dotnet test
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check |
| POST | `/api/search` | RAG semantic search |
| GET | `/api/codes/{code}` | Direct code lookup |
| GET | `/api/codes` | List codes (paginated) |

## Architecture

```
SETUP (One-Time)                    RUNTIME (C# Only)
┌─────────────────────┐            ┌──────────────────────┐
│ CreateDb/run.sh     │            │ ICD10AM.Api          │
│ ├── migrate schema  │ ────────▶  │ ├── ONNX Runtime     │
│ ├── import codes    │            │ ├── BERTTokenizers   │
│ └── gen embeddings  │            │ └── LQL Data Access  │
└─────────────────────┘            └──────────────────────┘
        ↓                                  ↑
   icd10cm.db                         User Queries
   (74,260 codes + embeddings)
```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DbPath` | Path to SQLite database | `icd10cm.db` |
| `ASPNETCORE_URLS` | API listen URL | `http://localhost:5000` |

## Troubleshooting

### "model.onnx not found"
RunService/run.sh exports it automatically. Or manually:
```bash
pip install optimum[onnxruntime]
optimum-cli export onnx --model abhinand/MedEmbed-small-v0.1 ICD10AM.Api/onnx_model/
```

### "No embeddings found"
Run `CreateDb/run.sh` - RAG search requires pre-computed embeddings.

### Tests fail with "database not found"
Run `CreateDb/run.sh` first.
