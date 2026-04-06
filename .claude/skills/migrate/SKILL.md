---
name: migrate
description: Run database migrations using the Migration CLI with YAML schemas. Use when asked to create databases, run migrations, or set up schema.
disable-model-invocation: true
allowed-tools: Bash(dotnet run --project *Migration*)
argument-hint: "[schema.yaml] [output.db] [provider]"
---

# Migrate

Run the Migration CLI to create or update databases from YAML schema files.

## Usage

`/migrate` - show help
`/migrate icd10` - create ICD10 SQLite database from its schema

## Shortcuts

| Argument | Schema | Output | Provider |
|----------|--------|--------|----------|
| icd10 | Samples/ICD10/ICD10.Api/icd10-schema.yaml | Samples/ICD10/ICD10.Api/icd10.db | sqlite |

## Manual usage

```bash
dotnet run --project /Users/christianfindlay/Documents/Code/DataProvider/Migration/Migration.Cli -- \
    --schema <path-to-schema.yaml> \
    --output <output-path> \
    --provider <sqlite|postgres>
```

## Notes

- YAML schemas are the ONLY valid way to define database schema (raw SQL DDL is ILLEGAL)
- Schema files live alongside their API projects
- Supported providers: `sqlite`, `postgres`
- The Migration CLI converts YAML to SQL DDL and applies it
