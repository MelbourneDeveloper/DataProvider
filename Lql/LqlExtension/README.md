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

The LSP has a pluggable AI completion integration with a **built-in Ollama provider** for local models and a trait for custom providers.

### Built-in: Ollama Provider

The LSP ships with a real Ollama-backed AI provider. Set `provider: "ollama"` and it calls your local Ollama instance with full context:

- **LQL language reference** — compiled into the binary, sent as system context
- **Full database schema** — table names, column names, types, PK/nullability constraints
- **Current file content** — the full document being edited
- **Cursor context** — line, column, prefix being typed

```json
{
  "initializationOptions": {
    "connectionString": "host=localhost dbname=mydb user=postgres password=secret",
    "aiProvider": {
      "provider": "ollama",
      "endpoint": "http://localhost:11434/api/generate",
      "model": "qwen2.5-coder:1.5b",
      "timeoutMs": 3000,
      "enabled": true
    }
  }
}
```

#### Quick Setup

```bash
cd Lql/lql-lsp-rust
./setup-ai.sh                      # Default: qwen2.5-coder:1.5b
./setup-ai.sh codellama:7b         # Or pick your model
./setup-ai.sh deepseek-coder:1.3b  # Lightweight alternative
```

The setup script: installs Ollama, pulls the model, smoke-tests it, builds the LSP, prints VS Code config.

#### Schema Context Sent to Model

The AI receives the full schema in compact form:

```
customers(id uuid PK NOT NULL, name text NOT NULL, email text NOT NULL, created_at timestamp NOT NULL)
orders(id uuid PK NOT NULL, customer_id uuid NOT NULL, total numeric NOT NULL, status text)
order_items(id uuid PK NOT NULL, order_id uuid NOT NULL, product_id uuid NOT NULL, quantity integer NOT NULL)
```

This means the model can suggest completions that reference real column names and types.

#### On/Off Toggle

Set `"enabled": false` to disable AI completions entirely. The LSP still provides full schema + keyword completions.

### Custom Provider Trait

For providers beyond Ollama, implement the trait:

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
- `schema_description` — full schema with column types, PK, nullability (e.g., `customers(id uuid PK NOT NULL, name text NOT NULL)`)

### LQL Reference Doc

The file `crates/lql-reference.md` is compiled into the binary via `include_str!` and sent as system context to the Ollama provider. It contains the complete LQL grammar, all operations, functions, operators, and annotated examples — optimized for tight LLM context windows.

### Built-in Test Providers

For E2E testing, the LSP includes two built-in providers:

- `provider: "test"` — Returns deterministic AI completions (`ai_suggest_filter`, `ai_suggest_join`, `ai_suggest_aggregate`) plus table-specific suggestions based on `available_tables`. Also emits `ai_schema_context` with the full schema description to prove schema flows through
- `provider: "test_slow"` — Sleeps longer than the configured timeout to prove timeout enforcement works

### Recommended Models

| Model | Size | Speed | Quality | Use Case |
|-------|------|-------|---------|----------|
| `qwen2.5-coder:1.5b` | 1.5B | Fast | Good | Default — best speed/quality tradeoff |
| `deepseek-coder:1.3b` | 1.3B | Fast | Good | Alternative lightweight model |
| `codellama:7b` | 7B | Medium | Better | When you have GPU and want higher quality |
| `qwen2.5-coder:7b` | 7B | Medium | Better | Larger Qwen for better understanding |

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

76+ E2E tests verify the LSP via real stdio JSON-RPC protocol — zero mocks.

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
| AI Pipeline | 13 | Full pipeline: provider activation, AI items in results, snippet kinds, prefix filtering, timeout enforcement, schema+AI merge, schema context proof, consistency |
| **Real AI Model** | **12** | **Real Ollama + real PostgreSQL + real LSP: completions, schema-aware AI, join queries, syntax errors, valid queries, hover, full pipeline merge, on/off toggle, live editing** |

### Running Schema Tests

Schema tests require a local PostgreSQL instance with the `lql_test` database:

```bash
# Start PostgreSQL
pg_ctlcluster 16 main start

# Tests auto-detect via LQL_CONNECTION_STRING or DATABASE_URL
# The test suite passes connection strings via initializationOptions
```

### Running Real AI Model Tests

These tests require Ollama running with a model pulled:

```bash
# Set up Ollama (one time)
cd Lql/lql-lsp-rust
./setup-ai.sh

# Run real AI tests
cd Lql/LqlExtension
LQL_TEST_REAL_AI=1 npx mocha --timeout 60000 out/test/suite/lsp-protocol.test.js

# Or with a specific model
LQL_TEST_REAL_AI=1 OLLAMA_MODEL=codellama:7b npx mocha --timeout 60000 out/test/suite/lsp-protocol.test.js
```

The real AI tests prove:
- Real Ollama model returns LQL-aware completions
- Schema columns + AI suggestions coexist in results
- Multi-line join queries get real completions
- Syntax errors produce REAL error diagnostics with line/column
- Valid LQL produces zero diagnostics
- Hover on tables/columns returns real DB types
- Full pipeline: real DB schema + real AI model + real LSP merged
- `enabled: false` completely disables model calls
- Live editing triggers updated diagnostics in real time

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
