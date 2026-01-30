# ICD-10-CM Microservice

RAG semantic search for 74,260 ICD-10-CM diagnosis codes. Pure C# with ONNX Runtime - no Python at runtime.

## Quick Start

### 1. Setup Database & Embeddings (One-Time)

```bash
cd Samples/ICD10CM

# Install Python deps
pip install click requests sentence-transformers torch

# Import codes from CMS.gov (downloads ~5MB, creates 74,260 codes)
python scripts/import_icd10cm.py --db-path icd10cm.db

# Generate embeddings (~30-60 mins for all codes)
python scripts/generate_embeddings.py --db-path icd10cm.db
```

### 2. Export ONNX Model (One-Time)

The ONNX model (127MB) is gitignored. Export it locally:

```bash
cd ICD10AM.Api
pip install optimum[onnxruntime]
optimum-cli export onnx --model abhinand/MedEmbed-small-v0.1 onnx_model/
```

### 3. Run the API

```bash
cd ICD10AM.Api
DbPath="../icd10cm.db" dotnet run --urls "http://localhost:5558"
```

### 4. Test It

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

# Run all tests (requires icd10cm.db with embeddings)
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

## Project Structure

```
Samples/ICD10CM/
├── ICD10AM.Api/           # C# API (ONNX Runtime, no Python)
│   ├── Program.cs         # Endpoints + ONNX embedding
│   ├── onnx_model/        # MedEmbed ONNX model (gitignored)
│   └── Vocabularies/      # BERT tokenizer vocab
├── ICD10AM.Api.Tests/     # E2E integration tests
├── scripts/
│   ├── import_icd10cm.py      # Import codes from CMS.gov
│   └── generate_embeddings.py # Generate vector embeddings
├── icd10cm.db             # SQLite database (gitignored)
└── SPEC.md                # Full specification
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check |
| POST | `/api/search` | RAG semantic search |
| GET | `/api/codes/{code}` | Direct code lookup |
| GET | `/api/codes` | List codes (paginated) |

### RAG Search Request

```json
{
  "Query": "severe abdominal pain",
  "Limit": 10
}
```

### RAG Search Response

```json
{
  "Results": [
    {
      "Code": "R10.9",
      "Description": "Unspecified abdominal pain",
      "Confidence": 0.89
    }
  ],
  "Query": "severe abdominal pain",
  "Model": "MedEmbed-Small-v0.1"
}
```

## Architecture

```
SETUP (Python - One-Time)           RUNTIME (C# Only)
┌─────────────────────┐            ┌──────────────────────┐
│ import_icd10cm.py   │            │ ICD10AM.Api          │
│ generate_embeddings │ ────────▶  │ ├── ONNX Runtime     │
│ export ONNX model   │            │ ├── BERTTokenizers   │
└─────────────────────┘            │ └── LQL Data Access  │
        ↓                          └──────────────────────┘
   icd10cm.db                              ↑
   (74,260 codes)                     User Queries
   (74,260 embeddings)
```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DbPath` | Path to SQLite database | `icd10cm.db` |
| `ASPNETCORE_URLS` | API listen URL | `http://localhost:5000` |

## Troubleshooting

### "model.onnx not found"
Export the ONNX model - see step 2 above.

### "base_uncased.txt not found"
The `Vocabularies/` directory should be in the project. If missing:
```bash
cp ~/.nuget/packages/berttokenizers/1.2.0/contentFiles/any/net6.0/Vocabularies/base_uncased.txt \
   ICD10AM.Api/Vocabularies/
```

### "No embeddings found"
Run `generate_embeddings.py` - RAG search requires pre-computed embeddings.

### Tests fail with "database not found"
Ensure `icd10cm.db` exists with imported codes and embeddings.
