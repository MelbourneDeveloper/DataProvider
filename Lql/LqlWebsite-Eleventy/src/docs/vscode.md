---
layout: layouts/docs.njk
title: VS Code Extension
description: LQL VS Code extension with syntax highlighting, IntelliSense, diagnostics, and AI completions.
---

The LQL VS Code extension provides a rich editing experience for `.lql` files, powered by a native Rust Language Server.

## Installation

Search for **LQL** in the VS Code Extensions marketplace and click Install. The extension automatically downloads the correct LSP binary for your platform on first activation.

Supported platforms:
- Linux x64
- macOS x64 (Intel)
- macOS ARM64 (Apple Silicon)
- Windows x64

## Features

### Syntax Highlighting

Full TextMate grammar with semantic colorization for all LQL constructs:

| Token | Color | Example |
|-------|-------|---------|
| Keywords | Orange-red | `let`, `fn`, `as`, `asc`, `desc` |
| Pipeline operator | Forest green | `\|>` |
| Lambda operator | Violet | `=>` |
| Query functions | Forest green | `select`, `filter`, `join` |
| Aggregate functions | Violet | `count`, `sum`, `avg` |
| String literals | Lime green | `'completed'` |
| Comments | Dark slate | `-- comment` |
| Table/column names | White/green | `users.id` |

The extension includes a dedicated **LQL Dark** color theme optimized for LQL syntax.

### IntelliSense Completions

Context-aware completions triggered automatically as you type. Completions are organized by priority:

**1. Column completions** - Type `table.` to see all columns from that table (requires [database connection](/docs/database-config/)):
- Shows column type, primary key indicator, and nullability
- Example: `users.` suggests `id (uuid PK NOT NULL)`, `name (text)`, `email (text)`

**2. Pipeline operations** - Suggested after `|>`:
- `select`, `filter`, `join`, `left_join`, `right_join`, `cross_join`
- `group_by`, `order_by`, `having`, `limit`, `offset`
- `union`, `union_all`, `insert`, `distinct`

**3. Functions** - Suggested in expression contexts:
- **Aggregate**: `count`, `sum`, `avg`, `min`, `max`, `first`, `last`, `row_number`, `rank`
- **String**: `concat`, `substring`, `length`, `trim`, `upper`, `lower`, `replace`
- **Math**: `round`, `floor`, `ceil`, `abs`, `sqrt`, `power`, `mod`
- **Date/Time**: `now`, `today`, `year`, `month`, `day`, `extract`, `date_trunc`
- **Conditional**: `coalesce`, `nullif`, `isnull`, `isnotnull`

**4. Keywords** - `let`, `fn`, `as`, `and`, `or`, `not`, `distinct`, `null`, `case`, `when`, etc.

**5. Table names** - From your database schema, showing column count and first 5 columns

**6. Variable bindings** - `let` bindings from the current document

**7. AI completions** - Optional intelligent suggestions from an AI model (see [AI Configuration](#ai-configuration))

### Snippets

23 built-in snippets with tab stops for fast query authoring:

| Prefix | Description |
|--------|-------------|
| `select` | Basic select all |
| `selectc` | Select specific columns |
| `selectf` | Select with filter |
| `filterand` | Filter with AND |
| `filteror` | Filter with OR |
| `join` | Inner join |
| `leftjoin` | Left join |
| `groupby` | Group by with count |
| `groupbyhaving` | Group by with having |
| `orderby` | Order by ascending |
| `limit` | Limit results |
| `limitoffset` | Pagination (limit + offset) |
| `distinct` | Select distinct |
| `union` | Union queries |
| `let` | Let binding |
| `case` | Case expression |
| `fn` | Lambda function |
| `pipeline` | Full pipeline example |

### Real-Time Diagnostics

Errors and warnings appear as you type with squiggly underlines:

**Errors** (red):
- ANTLR parse errors with line/column position
- Unmatched closing parenthesis
- Unclosed parenthesis at end of file

**Warnings** (yellow):
- Pipe operator `|>` not surrounded by spaces

**Information** (blue):
- Unknown function names (not in the built-in function list)

### Hover Documentation

Hover over any LQL keyword, function, or operator to see:
- Description and usage
- Syntax signature

With a [database connection](/docs/database-config/), hover also shows:
- **Table hover**: All columns with types, PK/nullable indicators
- **Column hover** (e.g., `users.email`): Column type, nullability, primary key status

### Document Symbols

The outline view shows all `let` bindings in your file, enabling quick navigation with `Ctrl+Shift+O`.

### Document Formatting

Format your entire document with `Shift+Alt+F` or right-click and select **Format LQL Document**:
- Consistent 4-space indentation for pipeline continuations
- Proper indentation inside parentheses
- Trimmed whitespace
- Preserved comments and blank lines

### Commands

| Command | Description |
|---------|-------------|
| **Format LQL Document** | Format the current `.lql` file |
| **Validate LQL Document** | Trigger validation diagnostics |
| **Show Compiled SQL** | Show the transpiled SQL output |

Commands are available in the command palette (`Ctrl+Shift+P`) and the editor context menu when editing `.lql` files.

## Extension Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `lql.languageServer.enabled` | boolean | `true` | Enable/disable the language server |
| `lql.languageServer.trace` | enum | `off` | LSP trace level: `off`, `messages`, `verbose` |
| `lql.validation.enabled` | boolean | `true` | Enable/disable real-time validation |
| `lql.formatting.enabled` | boolean | `true` | Enable/disable document formatting |

## AI Configuration

The extension supports optional AI-powered completions via local or remote models. AI completions are merged with schema and keyword completions - they supplement, never replace.

### Setup with Ollama (recommended)

1. Install [Ollama](https://ollama.com)
2. Pull a code model:
   ```bash
   ollama pull qwen2.5-coder:1.5b
   ```
3. Add to your VS Code `settings.json`:
   ```json
   {
     "lql.aiProvider": {
       "provider": "ollama",
       "endpoint": "http://localhost:11434",
       "model": "qwen2.5-coder:1.5b",
       "enabled": true
     }
   }
   ```

### Recommended Models

| Model | Size | Speed | Quality |
|-------|------|-------|---------|
| `qwen2.5-coder:1.5b` | 1.5B | Fast | Good |
| `deepseek-coder:1.3b` | 1.3B | Fast | Good |
| `codellama:7b` | 7B | Slower | Better |

### AI Provider Settings

```json
{
  "lql.aiProvider": {
    "provider": "ollama",
    "endpoint": "http://localhost:11434",
    "model": "qwen2.5-coder:1.5b",
    "apiKey": "",
    "timeoutMs": 2000,
    "enabled": true
  }
}
```

| Field | Description |
|-------|-------------|
| `provider` | Provider type: `ollama`, `openai`, `anthropic`, `custom` |
| `endpoint` | API endpoint URL |
| `model` | Model identifier (optional, provider-specific) |
| `apiKey` | API key (optional, for cloud providers) |
| `timeoutMs` | Timeout in milliseconds (default: 2000) |
| `enabled` | Enable/disable AI completions |

### What the AI Sees

The AI model receives full context for accurate suggestions:
- The complete document text
- Cursor position (line and column)
- Current line prefix and word prefix
- File URI
- Database schema (table names, column names, types)

Responses that exceed the timeout are silently dropped - you always get fast keyword and schema completions regardless of AI latency.

## Language Features

### Comment Support
- Line comments: `-- comment`
- Block comments: `/* comment */`

### Bracket Matching
Auto-closing and matching for `()`, `[]`, `{}`, `''`, `""`

### Folding
Region-based folding with `-- #region` and `-- #endregion` markers.

## LSP Binary

The extension bundles a native Rust language server (`lql-lsp`). On first activation, it searches for the binary in this order:

1. Bundled `bin/lql-lsp` in the extension directory
2. Local development build (`target/release/lql-lsp` or `target/debug/lql-lsp`)
3. Previously cached binary in VS Code global storage
4. Downloads from [GitHub Releases](https://github.com/Nimblesite/DataProvider/releases) matching the extension version
5. Falls back to `lql-lsp` on your system PATH
