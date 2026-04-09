# Plan: pgvector support in DataProviderMigrate + DataProvider codegen

> Implements spec section [MIG-TYPES-VECTOR] in `docs/specs/migration-spec.md`. Target release: **0.9.0-beta**.

## Why

HealthcareSamples (ICD10 embeddings) + any consumer doing semantic search needs native pgvector storage + indexes in Postgres, native `float[]` codegen, and a graceful SQLite fallback. Storing embeddings as `text` + casting at query time (the current workaround) is unacceptable.

## Scope (in 0.9.0-beta)

| Item | Spec ref | Owner |
|---|---|---|
| `VectorType(int Dimensions)` portable type record | [MIG-TYPES-VECTOR] §5.4 | `lql-cli-merger` |
| YAML `type: Vector(N)` parser + writer | [MIG-TYPES-VECTOR] §5.4.1 | `lql-cli-merger` |
| `PostgresDdlGenerator` → `vector(N)` + `CREATE EXTENSION IF NOT EXISTS vector` prologue | [MIG-TYPES-VECTOR] §5.4.2 | `lql-cli-merger` |
| `SqliteDdlGenerator` → `BLOB` fallback | [MIG-TYPES-VECTOR] §5.4.6 | `lql-cli-merger` |
| `PostgresSchemaInspector` reverse-map via `pg_attribute.atttypmod` | [MIG-TYPES-VECTOR] §5.4.4 | `DataProvider` |
| IVFFlat + HNSW index DDL with `vector_ops` + options | [MIG-TYPES-VECTOR-INDEX] §5.4.3 | `DataProvider` |
| DataProvider codegen emits `float[]` in records, binders, readers | [DP-CODEGEN-VECTOR] §5.4.5 | `DataProvider` |
| Add `Pgvector.Npgsql` to DataProvider tool deps | [DP-CODEGEN-VECTOR] §5.4.5 | `DataProvider` |
| Diagnostics `MIG-E-VECTOR-*` | [MIG-TYPES-VECTOR] §5.4.8 | split per owner |
| YAML round-trip tests | §5.4.1 | `dataprovider-ci-prep` |
| Postgres testcontainer with `pgvector/pgvector:pg16` image | §5.4.2, §5.4.3, §5.4.4 | `dataprovider-ci-prep` |
| SQLite BLOB fallback test | §5.4.6 | `dataprovider-ci-prep` |
| `make ci` green across the full matrix | — | `dataprovider-ci-prep` |

## Out of scope (deferred to 0.10.0-beta)

- LQL `cosine_distance(col, @q)` + `order_by` + `limit(@k)` sugar in Postgres LQL transpile. Tracked as [LQL-COSINE-DISTANCE] in `lql-spec.md` (pending).
- `ReadOnlyMemory<float>` codegen variant (`--vector-repr=readonly-memory`).
- SQL Server native vector type (SQL Server 2025 roadmap).
- Half-precision vectors (`halfvec`), sparse vectors (`sparsevec`), bit vectors (`bit(N)` as vector operand). Follow-up release.
- Vector-aware sync (Sync framework does not currently diff vector columns; vectors ride through as opaque bytes).

## Execution order (strict)

1. **Spec landed** (this file + `migration-spec.md` §5.4) — done by DataProvider.
2. **`lql-cli-merger`**: land `VectorType(int Dimensions)` in `PortableTypes.cs`, YAML r/w in `SchemaYamlSerializer.cs`, PG DDL + `CREATE EXTENSION` in `PostgresDdlGenerator.cs`, SQLite `BLOB` in `SqliteDdlGenerator.cs`. No tests in this step.
3. **`DataProvider`**: inspector reverse-map in `PostgresSchemaInspector.cs`; `CreateIndex` operation extended with `VectorOps` + `IvfflatOptions`/`HnswOptions` discriminated union; codegen emits `float[]`; `Pgvector.Npgsql` added to `DataProvider/DataProvider/DataProvider.csproj`.
4. **`dataprovider-ci-prep`**: failing round-trip tests first (per CLAUDE.md bug-fix / feature process). Use `pgvector/pgvector:pg16` testcontainer image. Then run `make ci`.
5. **Owner**: `git tag v0.9.0-beta` → `release.yml` packs + pushes all packages sync'd to 0.9.0-beta.
6. **HealthcareSamples**: pin to 0.9.0-beta, convert `text` embedding columns to `Vector(384)`, drop the query-time cast.

## File inventory (exact)

### `lql-cli-merger` edits

- `Migration/Nimblesite.DataProvider.Migration.Core/PortableTypes.cs` — add `public sealed record VectorType(int Dimensions) : PortableType;`. Respect the closed hierarchy (`private` base constructor, records only).
- `Migration/Nimblesite.DataProvider.Migration.Core/SchemaYamlSerializer.cs` — extend `PortableTypeYamlConverter.ReadYaml` → `ParseType` switch to recognise `Vector(N)`; extend `WriteYaml` pattern-match. Reject bare `Vector` with `MIG-E-VECTOR-DIMS-MISSING`, out-of-range with `MIG-E-VECTOR-DIMS-RANGE`.
- `Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresDdlGenerator.cs` — add `VectorType(var n) => $"vector({n})"` to `PortableTypeToPostgres`; detect any `VectorType` column in the schema and prepend `CREATE EXTENSION IF NOT EXISTS vector;` to the emitted DDL batch exactly once. Fail with `MIG-E-VECTOR-EXT-PERM` on extension permission error.
- `Migration/Nimblesite.DataProvider.Migration.SQLite/SqliteDdlGenerator.cs` — add `VectorType => "BLOB"` to `PortableTypeToSqlite`. Precedent: `GeometryType`/`GeographyType` at line 185.

### `DataProvider` (me) edits

- `Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresSchemaInspector.cs` — augment `InspectTable` to join `pg_attribute` and decode `atttypmod` for vector columns; add `"VECTOR"` case to `PostgresTypeToPortable` that constructs `VectorType(dims)` from the decoded modifier. `MIG-E-VECTOR-INTROSPECT` on decode failure.
- `Migration/Nimblesite.DataProvider.Migration.Core/SchemaTypes.cs` — extend `IndexDefinition` with optional `IndexType` (enum: `BTree | IvfflatVector | HnswVector`), `VectorOps` (enum: `Cosine | L2 | Ip`), and a discriminated union of index options.
- `Migration/Nimblesite.DataProvider.Migration.Postgres/PostgresDdlGenerator.cs` — (shared file with lql-cli-merger, lock coordination required) extend `GenerateCreateIndex` to emit `USING ivfflat ("col" vector_cosine_ops) WITH (lists = 100)` etc. Validate options per `MIG-E-VECTOR-IDX-OPTIONS` / `MIG-E-VECTOR-IDX-NONVECTOR`.
- `DataProvider/DataProvider/DataProvider.csproj` — add `<PackageReference Include="Pgvector.Npgsql" Version="*" />`.
- `DataProvider/DataProvider/PostgresCli.cs` (codegen) — add `VectorType(var n)` case in C# type mapping → `float[]`; reader path `reader.GetFieldValue<Pgvector.Vector>(i).ToArray()`; writer path builds `new Pgvector.Vector(arr)`. `float[]?` when `IsNullable = true`.

### `dataprovider-ci-prep` edits

- `Migration/Nimblesite.DataProvider.Migration.Tests/SchemaYamlSerializerTests.cs` — add `Vector(384)` round-trip case to `RoundTrip_ComplexSchema_PreservesAllData` + specific `Vector_ParsesDimensions` + `Vector_BareTypeFails` negative test.
- `Migration/Nimblesite.DataProvider.Migration.Tests/PostgresMigrationTests.cs` — switch the Postgres testcontainer image to `pgvector/pgvector:pg16` (retains every existing test + adds vector-specific cases). New tests: `Vector_CreateTable_EmitsExtension`, `Vector_CreateTable_RoundTripsDimensions`, `Vector_IvfflatIndex_BuildsAndQueries`, `Vector_HnswIndex_BuildsAndQueries`, `Vector_InspectTable_ReturnsVectorType`.
- `Migration/Nimblesite.DataProvider.Migration.Tests/SqliteMigrationTests.cs` — `Vector_OnSqlite_FallsBackToBlob` asserting the BLOB lowering.
- `DataProvider.Tests` — `Vector_Codegen_EmitsFloatArray_ForReader` and `_ForWriter`.
- `coverage-thresholds.json` — bump if new code exceeds existing minimums (should not drop any).

## Coordination rules

- **PostgresDdlGenerator.cs is shared** between lql-cli-merger (column type + extension prologue) and DataProvider (vector index DDL). Acquire TMC lock, do the edit, release immediately. Do not hold.
- All three agents use **the same GeneratorTool branch** (shared worktree). Commit messages must reference `[MIG-TYPES-VECTOR]` or `[DP-CODEGEN-VECTOR]` for traceability.
- Version bump to `0.9.0-beta` lands in a single commit at the end by whoever ships last. `dataprovider-ci-prep` coordinates the bump + reruns CI one final time before signalling owner to tag.

## Rollback

If pgvector landing breaks existing tests we cannot unbreak in under 30 minutes:
- `git revert` the vector commits.
- Publish 0.9.0-beta with `Lql` rename only (no vector).
- Re-target vector for 0.10.0-beta.
- HealthcareSamples stays on their text + cast workaround until 0.10.0-beta.
