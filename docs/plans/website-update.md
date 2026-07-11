# Website Audit & Fix — Comprehensive Update

## Context

DataProvider has been through a major overhaul. All packages are now published to NuGet under two naming conventions:

- **Top-level CLI tools / flagship packages**: `DataProvider` (v0.9.6-beta), `DataProviderMigrate` (v0.9.6-beta), `Lql` (v0.9.6-beta)
- **Library packages**: `Nimblesite.DataProvider.*`, `Nimblesite.Lql.*`, `Nimblesite.Sync.*`, `Nimblesite.Reporting.Engine`, `Nimblesite.Sql.Model` — all at v0.9.6-beta

The current website (`Website/src/docs/`) still advertises stale package names:
- `DataProvider.MySql` / `DataProvider.Sqlite` / `DataProvider.SqlServer` / `DataProvider.Postgres` (do not exist; real names are `Nimblesite.DataProvider.*`)
- `Lql.SQLite` / `Lql.Postgres` / `Lql.SqlServer` (real names are `Nimblesite.Lql.*`)
- `Lql.TypeProvider.FSharp` (does not exist on NuGet)
- `MelbourneDev.*` prefixes in `samples.md` (do not exist)
- `Dapper`-style `connection.Query<T>(...)` / `connection.Execute(...)` examples in `quick-start.md` — these are NOT the DataProvider API
- `Samples/` content references a repo (`HealthcareSamples`) that no longer exists

The samples repo was removed. The reference implementation now lives at `/Users/christianfindlay/Documents/Code/ClinicalCoding` — the **Nimblesite Clinical Coding Platform** (MIT, 2026 Nimblesite, public on GitHub at `MelbourneDeveloper/ClinicalCoding`). The website must advertise this platform in full.

### Single source of truth: NO CONTENT DUPLICATION

**CRITICAL**: Component documentation must never be duplicated between component READMEs and the website. The READMEs in each component folder (`DataProvider/README.md`, `Lql/README.md`, `Sync/README.md`, `Gatekeeper/README.md`, `Migration/README.md`) are the canonical source of truth for component-level docs. The website build — specifically [Website/scripts/copy-readmes.cjs](Website/scripts/copy-readmes.cjs) — copies those READMEs into `Website/src/docs/{component}.md` at build time with Eleventy frontmatter prepended. The result is that `/docs/dataprovider/`, `/docs/lql/`, `/docs/sync/`, `/docs/gatekeeper/`, `/docs/migrations/` on the website show the exact same content as the component READMEs.

Rules:

- **Never hand-write** `Website/src/docs/dataprovider.md`, `lql.md`, `sync.md`, `gatekeeper.md`, or `migrations.md`. They are generated from READMEs — edit the README instead.
- **Top-level docs** (`installation.md`, `getting-started.md`, `quick-start.md`, `samples.md`) ARE hand-written website content and must not duplicate material that already lives in a component README. They should **link to** `/docs/{component}/` rather than copy-paste.
- If a component README is missing install/usage information that the website needs, fix the README and let `copy-readmes.cjs` publish it — do not add a parallel copy on the website.
- The Playwright stale-package sweep must run against pages generated from READMEs too; if it fails on `/docs/dataprovider/`, the fix goes in `DataProvider/README.md`, not the website.

The repo is missing a root `LICENSE` file. It must be added as MIT belonging to **Nimblesite Pty Ltd, 2026**.

No Playwright tests exist for the Website yet. The user wants automated Playwright tests for the updated documentation pages.

**Goal**: Rewrite all installation/usage docs to match the real shipped packages and CLI tools, add a dedicated Clinical Coding Platform reference-implementation page, add a root MIT license, and add Playwright smoke/verification tests that run against the built site.

---

## Canonical Package & Tool Reference (single source of truth)

### .NET Global/Local Tools (installed via `dotnet tool install`)

| Tool package | Command | Version | Purpose |
|---|---|---|---|
| `DataProvider` | `DataProvider` | `0.9.6-beta` | Source-generation CLI — reads `DataProvider.json` + `.sql`/`.lql` files, emits C# extension methods. Subcommands: `sqlite`, `postgres` |
| `DataProviderMigrate` | `DataProviderMigrate` | `0.9.6-beta` | YAML-schema migration CLI. Subcommands: `migrate`, `export` |
| `Lql` | `Lql` | `0.9.6-beta` | LQL → SQL transpiler CLI. Subcommands: `sqlite`, `postgres` |

`.config/dotnet-tools.json` example (this is how ClinicalCoding consumes them):

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dataprovider":        { "version": "0.9.6-beta",     "commands": ["DataProvider"] },
    "dataprovidermigrate": { "version": "0.9.6-beta","commands": ["DataProviderMigrate"] },
    "lql":                 { "version": "0.9.6-beta","commands": ["Lql"] }
  }
}
```

### Library Packages (installed via `dotnet add package`, all v0.9.6-beta)

| Package | Purpose |
|---|---|
| `Nimblesite.DataProvider.Core` | Shared runtime types for generated code |
| `Nimblesite.DataProvider.SQLite` | SQLite runtime |
| `Nimblesite.DataProvider.Postgres` | Postgres runtime |
| `Nimblesite.DataProvider.SqlServer` | SQL Server runtime |
| `Nimblesite.DataProvider.Migration.Core` | Migration engine core |
| `Nimblesite.DataProvider.Migration.SQLite` | SQLite DDL emitter |
| `Nimblesite.Sql.Model` | SQL AST model used by Lql/DataProvider |
| `Nimblesite.Lql.Core` | LQL parser + analyzer |
| `Nimblesite.Lql.SQLite` | `ToSqlite()` extension |
| `Nimblesite.Lql.Postgres` | `ToPostgreSql()` extension |
| `Nimblesite.Lql.SqlServer` | `ToSqlServer()` extension |
| `Nimblesite.Sync.Core` | Offline-first sync core |
| `Nimblesite.Sync.Http` | HTTP sync transport |
| `Nimblesite.Sync.Postgres` | Postgres sync provider |
| `Nimblesite.Sync.SQLite` | SQLite sync provider |
| `Nimblesite.Reporting.Engine` | Embeddable reporting platform |

**Packages the docs currently advertise but must be REMOVED** (do not exist on NuGet):
- `DataProvider.MySql`, `DataProvider.Sqlite`, `DataProvider.SqlServer`, `DataProvider.Postgres`
- `Lql.SQLite`, `Lql.Postgres`, `Lql.SqlServer`, `Lql.TypeProvider.FSharp`
- `MelbourneDev.DataProvider`, `MelbourneDev.Lql.Postgres`, `MelbourneDev.Sync`, `MelbourneDev.Sync.Postgres`, `MelbourneDev.Migration`, `MelbourneDev.Migration.Postgres`, `MelbourneDev.Selecta`

---

## Files to modify

### 1. [Website/src/docs/installation.md](Website/src/docs/installation.md) — full rewrite

Restructure into three clear sections matching real NuGet reality:

1. **Install the CLI tools** (as a local tool manifest):
   ```bash
   dotnet new tool-manifest
   dotnet tool install DataProvider --version 0.9.6-beta
   dotnet tool install DataProviderMigrate --version 0.9.6-beta
   dotnet tool install Lql --version 0.9.6-beta
   ```
2. **Add runtime library packages** for your database (table with `Nimblesite.DataProvider.SQLite|Postgres|SqlServer` + optional `Nimblesite.Lql.*`, `Nimblesite.Sync.*`, `Nimblesite.Reporting.Engine`)
3. **Requirements**: .NET 10 SDK, C# latest, `<Nullable>enable</Nullable>`
4. Link to Getting Started, Quick Start, Clinical Coding Platform

Remove MySQL. Remove `Lql.TypeProvider.FSharp`.

### 2. [Website/src/docs/getting-started.md](Website/src/docs/getting-started.md) — full rewrite

Replace with the **real end-to-end flow** used by `Nimblesite.DataProvider.Example` and `ClinicalCoding`:

1. Create local tool manifest + install the 3 tools
2. Add `Nimblesite.DataProvider.SQLite` PackageReference
3. Create `example-schema.yaml` → run `dotnet DataProviderMigrate migrate --schema example-schema.yaml --output invoices.db --provider sqlite`
4. Write `GetCustomers.lql` → run `dotnet Lql sqlite --input GetCustomers.lql --output GetCustomers.generated.sql`
5. Write `DataProvider.json` → run `dotnet DataProvider sqlite --project-dir . --config DataProvider.json --out ./Generated`
6. Consume generated `GetCustomersAsync` from C# with `Result<T, SqlError>` pattern match
7. MSBuild target example (cribbed from `DataProvider/Nimblesite.DataProvider.Example/Nimblesite.DataProvider.Example.csproj` lines 49-93) showing all three tools wired into pre-compile targets

Update .NET version to **10.0**. Remove Dapper-esque API.

### 3. [Website/src/docs/quick-start.md](Website/src/docs/quick-start.md) — full rewrite

Current content is Dapper, not DataProvider. Replace with a "5-minute query" walkthrough driven entirely by generated extension methods and `Result<T, SqlError>` pattern-matching. Include:

- A short `.lql` sample
- The resulting generated method signature
- Consuming code showing `switch` on `Result<T, SqlError>.Ok` / `.Error`
- A Postgres-targeted second snippet using `Nimblesite.Lql.Postgres.SqlStatementExtensionsPostgreSQL.ToPostgreSql()`

No raw SQL insert/update/delete snippets (violates project's "no raw SQL" rule).

### 4. [Website/src/docs/samples.md](Website/src/docs/samples.md) — full rewrite → rename conceptually to "Clinical Coding Platform"

Keep filename `samples.md` (so existing nav link works), but retitle to "Nimblesite Clinical Coding Platform — Reference Implementation".

Content sourced from `/Users/christianfindlay/Documents/Code/ClinicalCoding`:

- Overview: agentic ICD coding from patient encounters + clinical notes using pgvector semantic search
- **Disclaimer**: technology demonstration only, not for production
- Architecture diagram (4 microservices: Clinical, Scheduling, ICD10, Gatekeeper + Dashboard)
- Tech stack table (.NET 10, ASP.NET Core Minimal API, Postgres + pgvector, DataProvider, Lql, Sync, H5 + React 18, MedEmbed, Docker Compose)
- **NuGet packages consumed** table (corrected to real `Nimblesite.*` names, matching what ClinicalCoding's csproj files actually reference)
- **dotnet tools consumed** (`.config/dotnet-tools.json` snippet: DataProvider, DataProviderMigrate, Lql)
- FHIR R5 resources list (Patient, Encounter, Condition, MedicationRequest, Practitioner, Appointment)
- ICD-10 country-agnostic variant support table (CM/AM/GM/CA)
- RAG search `POST /api/search` request/response
- Service endpoint table (Clinical `:5080`, Scheduling `:5001`, ICD10 `:5090`, Dashboard `:8080`)
- Data ownership + sync flow table
- **Getting it running**: `make start-docker` / `make start-local` / `make db-migrate` / `make test`
- Links: `https://github.com/MelbourneDeveloper/ClinicalCoding`, MIT license
- Screenshot: copy `/Users/christianfindlay/Documents/Code/ClinicalCoding/login-after.png` into `Website/src/assets/images/clinical-coding/login.png` and reference it

### 5. [Website/src/_data/navigation.json](Website/src/_data/navigation.json) — targeted edits

- Change "Healthcare Samples" link text → "Clinical Coding Platform" (URL stays `/docs/samples/`)
- Remove "F# Type Provider" item (package doesn't exist)
- Keep existing DataProvider/LQL/Sync/Migrations/Gatekeeper component links (the `copy-readmes.cjs` script generates those pages from each component's README — they stay auto-generated; we only need to confirm the READMEs themselves have accurate install blocks — out of scope for this plan but flag it)

### 6. [Website/src/docs/blog](Website/src/blog) — targeted edits

Three blog posts reference stale package names. Replace with real package names:

- [Website/src/blog/getting-started-dataprovider.md:21](Website/src/blog/getting-started-dataprovider.md#L21)
- [Website/src/blog/connecting-sql-server.md:22](Website/src/blog/connecting-sql-server.md#L22)
- [Website/src/blog/lql-simplifies-development.md:71](Website/src/blog/lql-simplifies-development.md#L71)

### 7. [Website/src/index.njk](Website/src/index.njk) — homepage tweaks

- Update the hero's "Source-Generated SQL" card to mention the **CLI-driven build-time code generation** model (not a Roslyn analyzer — the current wording implies Roslyn)
- Add a "Reference Implementation: Clinical Coding Platform" card in the features grid linking to `/docs/samples/`
- Verify any package-name mentions on the page are correct

### 8. Root `LICENSE` file — new file (MIT 2026 Nimblesite Pty Ltd)

Create `/Users/christianfindlay/Documents/Code/DataProvider/LICENSE` with the standard MIT license text, copyright line:

```
Copyright (c) 2026 Nimblesite Pty Ltd
```

Also add a short "License" section at the bottom of the homepage README referencing it (if repo README lacks one — to be verified during execution).

### 9. [Website/scripts/copy-readmes.cjs](Website/scripts/copy-readmes.cjs) — no change expected

This script copies component READMEs (DataProvider, Lql, Sync, Gatekeeper, Migration) into `src/docs/` at build time. The READMEs themselves may still have stale package names — spot-check during execution and fix where needed. **Do not** modify this script; fix the underlying READMEs.

### 10. [Website/package.json](Website/package.json) — add Playwright

Add dev dependency `@playwright/test` and scripts:

```json
"test:e2e": "playwright test",
"test:e2e:install": "playwright install chromium"
```

### 11. New file: `Website/playwright.config.ts`

- `webServer`: runs `npm run build && npx http-server _site -p 8123` (or uses eleventy `--serve`)
- Single project: chromium, headless, baseURL `http://localhost:8123`
- Retries 0, workers 1 — deterministic

### 12. New file: `Website/tests/e2e/docs.spec.ts`

Playwright test suite that:

1. **Homepage smoke**: loads `/`, asserts hero heading visible, asserts at least one link to `/docs/samples/`, asserts NO occurrence of `MelbourneDev.`, `DataProvider.MySql`, or `Lql.TypeProvider.FSharp` anywhere in the DOM
2. **Installation page**: loads `/docs/installation/`, asserts presence of the three real tool names (`DataProvider`, `DataProviderMigrate`, `Lql`) in code blocks, asserts presence of `Nimblesite.DataProvider.SQLite`, asserts absence of stale names
3. **Getting Started page**: asserts presence of `dotnet tool install DataProvider`, `DataProviderMigrate migrate`, `Lql sqlite`, and `Result<` (Result type usage)
4. **Quick Start page**: asserts NO `connection.Query<` or `connection.Execute(` (Dapper API removed), asserts presence of `Result<` pattern matching
5. **Clinical Coding Platform page** (`/docs/samples/`): asserts page title contains "Clinical Coding", asserts link to `github.com/MelbourneDeveloper/ClinicalCoding`, asserts FHIR + ICD-10 + pgvector mentioned, asserts login screenshot `<img>` loads (response 200)
6. **Navigation page**: asserts nav contains "Clinical Coding Platform" text, does NOT contain "F# Type Provider"
7. **Broken-link sweep**: crawls all internal `/docs/*` links from the nav and asserts every response is 200
8. **Stale-package global sweep**: fetches every `/docs/*` and `/blog/*` page and asserts none contain the forbidden strings list

### 13. CI wiring (out of scope note)

The Website currently builds via `make` but has no `test:e2e` step in CI. Flag this for a follow-up; do not touch CI workflows in this pass unless explicitly asked.

---

## Existing code/utilities reused

- [Website/scripts/copy-readmes.cjs](Website/scripts/copy-readmes.cjs) — already generates `dataprovider.md`, `lql.md`, `sync.md`, `gatekeeper.md`, `migrations.md` from component READMEs. Don't duplicate.
- [Website/src/_includes/layouts/docs.njk](Website/src/_includes/layouts/docs.njk) — existing docs layout, reuse
- `Nimblesite.DataProvider.Example.csproj` lines 49-93 — real MSBuild target wiring for all three tools, copy verbatim into getting-started.md
- `Migration/DataProviderMigrate/Program.cs` lines 286-333 — real CLI help text for copy-paste into the migrations section
- `Lql/Lql/SqliteCli.cs` lines 20-43 — real Lql CLI flags
- `/Users/christianfindlay/Documents/Code/ClinicalCoding/.config/dotnet-tools.json` — real tool-manifest example (note: bump version from 0.9.5-beta to 0.9.6-beta for DataProviderMigrate/Lql when documenting)
- `/Users/christianfindlay/Documents/Code/ClinicalCoding/Makefile` — real `make` targets for the platform

---

## Verification

1. **Build**: `cd Website && npm run build` — eleventy build succeeds, no broken template references
2. **Visual smoke**: `cd Website && npm run dev` and manually open `/`, `/docs/installation/`, `/docs/getting-started/`, `/docs/quick-start/`, `/docs/samples/` in a browser — spot-check that the Clinical Coding screenshot renders, code blocks are syntax-highlighted, and nav shows "Clinical Coding Platform"
3. **Playwright**:
   ```bash
   cd Website
   npm run test:e2e:install
   npm run test:e2e
   ```
   All tests (homepage smoke, installation, getting-started, quick-start, clinical coding page, nav, broken-link sweep, stale-package sweep) must pass
4. **Stale-string sanity**: `grep -rE "(MelbourneDev\.|DataProvider\.MySql|DataProvider\.Sqlite[^.]|Lql\.TypeProvider|HealthcareSamples)" Website/src` — must return zero matches
5. **License**: `cat LICENSE | head -3` — must show `MIT License` and `Copyright (c) 2026 Nimblesite Pty Ltd`
6. **Clinical Coding asset**: `ls Website/src/assets/images/clinical-coding/login.png` exists and is non-empty

---

## Out of scope (flag but don't fix in this pass)

- Component README files (`DataProvider/README.md`, `Lql/README.md`, etc.) — may contain stale package names. Will need a separate pass once the top-level docs are fixed, since `copy-readmes.cjs` pipes them into `/docs/*` at build time. If Playwright stale-package sweep fails on an auto-generated page, fix the underlying README as part of this task.
- DocFX API reference pages (`Website/docfx/`) — these regenerate from XML doc comments in csproj; unchanged by this task.
- CI workflow (`.github/workflows/*`) — do not touch unless user asks.
