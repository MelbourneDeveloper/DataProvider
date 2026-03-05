# Lambda Query Language (LQL) VS Code Extension

A VS Code extension providing full language support for Lambda Query Language (LQL) — powered by a Rust LSP server with ANTLR-generated parser, real database schema IntelliSense, and optional AI-powered completions.

## Architecture

```
VS Code Extension (TypeScript)
    │
    └── stdio JSON-RPC ──▶ lql-lsp (Rust binary)
                                │
                                ├── lql-parser (ANTLR4 grammar → Rust)
                                ├── lql-analyzer (completions, hover, diagnostics, schema)
                                └── tokio-postgres (live database schema introspection)
```

The LSP server is a native Rust binary communicating over stdio using the Language Server Protocol. The parser is generated from `Lql.g4` using ANTLR4 targeting Rust via the `antlr-rust` crate.

## Features

### IntelliSense — Schema-Aware Completions

Completions are context-aware and sourced from three layers:

| Layer | Priority | What it provides |
|-------|----------|-----------------|
| **Schema (database)** | Columns: 0, Tables: 4 | Real table and column names from your database |
| **Language** | Pipeline: 1, Functions: 2, Keywords: 3 | `select`, `filter`, `join`, `count`, `sum`, etc. |
| **AI (optional)** | 6 | Custom model-generated suggestions |

Trigger characters: `.` `|` `>` `(` `space`

### IntelliPrompt — Dot-Triggered Column Completions

Type a table name followed by `.` and the LSP returns real columns from the database schema:

```
customers.  →  id (uuid, PK, NOT NULL)
               name (text, NOT NULL)
               email (text, NOT NULL)
               created_at (timestamp, NOT NULL)
```

Column completions include SQL type, nullability, and primary key indicators. Prefix filtering works — typing `customers.na` narrows to just `name`.

### Hover

- **Pipeline operations**: Rich Markdown with signature, description, and example
- **Aggregate/string/math functions**: Full documentation
- **Table names**: All columns with types displayed
- **Qualified names** (`Table.Column`): Column type, nullability, PK status from live schema
- **Unknown columns**: Shows available columns on the table

### Diagnostics

- Real-time syntax error detection from ANTLR parse
- Semantic analysis (unknown functions, pipeline validation)
- Errors include line/column ranges for inline squiggles

### Document Symbols

- Extracts `let` bindings as document symbols with correct source locations

### Formatting

- Automatic indentation of pipeline operators
- Bracket-aware indent/dedent

## Database Connection

The LSP connects to a real database to power schema-aware features. Connection is resolved in order:

1. `initializationOptions.connectionString` (from VS Code settings)
2. `LQL_CONNECTION_STRING` environment variable
3. `DATABASE_URL` environment variable

### Supported Connection String Formats

**libpq** (native):
```
host=localhost dbname=mydb user=postgres password=secret
```

**Npgsql** (.NET style — auto-converted):
```
Host=localhost;Database=mydb;Username=postgres;Password=secret
```

**URI**:
```
postgres://postgres:secret@localhost/mydb
```

### Schema Introspection

On startup, the LSP queries `information_schema.columns` joined with primary key constraints to discover:
- All tables in the `public` schema
- Column names, SQL types, nullability
- Primary key membership

Timeouts: 10s connection, 30s query. Schema is cached in memory using `Arc` for lock-free concurrent reads.

### Graceful Degradation

When no database is available, the LSP still provides full keyword, function, and pipeline completions. Schema-dependent features (table/column completions, qualified hover) are simply omitted.

## AI Completion Provider

The LSP has a pluggable AI completion integration. It does **not** ship with or call any specific AI model. Instead, it defines a trait that external code implements:

```rust
#[tower_lsp::async_trait]
pub trait AiCompletionProvider: Send + Sync {
    async fn complete(&self, context: &AiCompletionContext) -> Vec<CompletionItem>;
}
```

### How It Works

1. Configure via `initializationOptions.aiProvider`
2. The LSP calls `provider.complete()` on every completion request
3. AI results are **merged** with schema and keyword completions
4. A timeout (default 2000ms) ensures slow AI never blocks the editor
5. AI completions appear at priority 6 (after all schema/keyword items)

### Configuration

```json
{
  "initializationOptions": {
    "connectionString": "host=localhost dbname=mydb user=postgres password=secret",
    "aiProvider": {
      "provider": "openai",
      "endpoint": "https://api.openai.com/v1/completions",
      "model": "gpt-4",
      "apiKey": "sk-...",
      "timeoutMs": 2000,
      "enabled": true
    }
  }
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `provider` | Yes | Provider identifier (`"openai"`, `"anthropic"`, `"ollama"`, `"custom"`, `"test"`) |
| `endpoint` | Yes | API endpoint URL |
| `model` | No | Model identifier (default: `"default"`) |
| `apiKey` | No | API key for authentication |
| `timeoutMs` | No | Max wait for AI response in ms (default: `2000`) |
| `enabled` | No | Enable/disable AI completions (default: `true`) |

### Context Passed to AI

The `AiCompletionContext` includes:
- `document_text` — full file content
- `line`, `column` — cursor position
- `line_prefix` — text before cursor on current line
- `word_prefix` — the word currently being typed
- `file_uri` — URI of the file
- `available_tables` — table names from the database schema (if loaded)

### Built-in Test Providers

For E2E testing, the LSP includes two built-in providers:

- `provider: "test"` — Returns deterministic AI completions (`ai_suggest_filter`, `ai_suggest_join`, `ai_suggest_aggregate`) plus table-specific suggestions based on `available_tables`
- `provider: "test_slow"` — Sleeps longer than the configured timeout to prove timeout enforcement works

### What "Model" Does It Use?

**None.** The LSP itself is model-agnostic. The `model` field in `AiConfig` is passed through to your `AiCompletionProvider` implementation — you decide what to call. The trait is the contract; the LSP handles merging, timeout, and priority. You bring the model.

## Building

### Language Server (Rust)

```bash
cd Lql/lql-lsp-rust
cargo build --release
# Binary: target/release/lql-lsp
```

### VS Code Extension (TypeScript)

```bash
cd Lql/LqlExtension
npm install
npx tsc --project tsconfig.json
```

## Testing

63 E2E tests verify the LSP via real stdio JSON-RPC protocol — zero mocks.

```bash
cd Lql/LqlExtension
npx tsc --project tsconfig.json
npx mocha --timeout 30000 out/test/suite/lsp-protocol.test.js
```

### Test Breakdown

| Suite | Count | What it proves |
|-------|-------|---------------|
| Core LSP | 37 | Completions, hover, diagnostics, symbols, formatting, shutdown |
| Schema-Aware | 10 | Real PostgreSQL: table/column completions, qualified hover, graceful degradation |
| AI Config | 4 | Config parsing, enabled/disabled logging, coexistence with keywords |
| AI Pipeline | 12 | Full pipeline: provider activation, AI items in results, snippet kinds, prefix filtering, timeout enforcement, schema+AI merge, consistency |

### Running Schema Tests

Schema tests require a local PostgreSQL instance with the `lql_test` database:

```bash
# Start PostgreSQL
pg_ctlcluster 16 main start

# Tests auto-detect via LQL_CONNECTION_STRING or DATABASE_URL
# The test suite passes connection strings via initializationOptions
```

## Completion Priority Tiers

| Priority | Kind | Source |
|----------|------|--------|
| 0 | Column | Database schema |
| 1 | Pipeline | `select`, `filter`, `join`, etc. |
| 2 | Function | `count`, `sum`, `avg`, `concat`, etc. |
| 3 | Keyword | `let`, `fn`, `as`, `distinct`, etc. |
| 4 | Table | Database schema |
| 5 | Variable | `let` bindings from scope |
| 6 | AI Snippet | AI provider suggestions |

Completions are deduplicated by label and sorted by priority, then alphabetically within each tier.

## File Extensions

- `.lql` — Lambda Query Language files

## License

MIT License - see LICENSE file for details.
