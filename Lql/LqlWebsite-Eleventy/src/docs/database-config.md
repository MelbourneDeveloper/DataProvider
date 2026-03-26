---
layout: layouts/docs.njk
title: Database Configuration
description: Connect the LQL Language Server to your database for schema-aware completions and hover.
---

The LQL Language Server can connect to your PostgreSQL database to provide schema-aware features like column completions, table hover, and qualified column hover.

## Why Connect a Database?

Without a database connection, the LSP still provides:
- Keyword and function completions
- Pipeline operation suggestions
- Parse error diagnostics
- Hover documentation for LQL constructs
- Document formatting and symbols

With a database connection, you additionally get:
- **Column completions** - Type `users.` and see all columns with types
- **Table name completions** - See all tables with column counts
- **Table hover** - Hover over a table name to see its full schema
- **Column hover** - Hover over `users.email` to see type, nullability, and PK status

## Connection Methods

The LSP resolves the database connection in this priority order:

### 1. VS Code Settings (recommended)

Add a connection string to your VS Code `settings.json`:

```json
{
  "lql.connectionString": "host=localhost dbname=myapp user=postgres password=secret"
}
```

This is passed to the LSP via `initializationOptions.connectionString`.

### 2. Environment Variable: LQL_CONNECTION_STRING

```bash
export LQL_CONNECTION_STRING="host=localhost dbname=myapp user=postgres password=secret"
```

### 3. Environment Variable: DATABASE_URL

```bash
export DATABASE_URL="postgres://postgres:secret@localhost/myapp"
```

## Supported Connection Formats

The LSP accepts multiple PostgreSQL connection string formats and normalizes them automatically.

### libpq Format

The native PostgreSQL format:

```
host=localhost dbname=myapp user=postgres password=secret
```

With port:

```
host=localhost port=5433 dbname=myapp user=postgres password=secret
```

### Npgsql Format (.NET style)

Semicolon-delimited key=value pairs. These are automatically converted to libpq format:

```
Host=localhost;Database=myapp;Username=postgres;Password=secret
```

Mapping:
- `Host` -> `host`
- `Database` -> `dbname`
- `Username` -> `user`
- `Password` -> `password`
- `Port` -> `port`

### URI Format

PostgreSQL connection URI:

```
postgres://postgres:secret@localhost/myapp
postgresql://postgres:secret@localhost:5433/myapp
```

## Schema Introspection

On startup (and when the connection is available), the LSP queries `information_schema.columns` and `information_schema.key_column_usage` to discover:

| Metadata | Source |
|----------|--------|
| Table names | `information_schema.columns` |
| Column names | `information_schema.columns` |
| Column types | `data_type` column |
| Nullability | `is_nullable` column |
| Primary keys | `information_schema.key_column_usage` |

The schema is cached in memory for fast lookups. Connection timeout is 10 seconds, query timeout is 30 seconds.

## Graceful Degradation

If the database is unreachable or the connection string is invalid:

- The LSP logs the error and continues without schema
- All non-schema features remain fully functional
- No error is shown to the user (check the **LQL Language Server** output channel for diagnostics)

This means you can use the extension without any database - you just won't get table/column completions.

## Schema-Aware Features in Detail

### Column Completions

When you type a table name followed by `.`, the LSP shows all columns for that table:

```
users.
```

Completion list shows:
```
id       uuid (PK) NOT NULL
name     text NOT NULL
email    text
status   text
```

### Table Completions

Table names appear in the completion list with metadata:

```
users    (4 columns: id, name, email, status)
orders   (6 columns: id, user_id, total, status, ...)
```

### Table Hover

Hovering over a table name shows the full schema:

```
Table: users

| Column | Type | PK | Nullable |
|--------|------|----|----------|
| id     | uuid | Y  | N        |
| name   | text |    | N        |
| email  | text |    | Y        |
| status | text |    | Y        |
```

### Qualified Column Hover

Hovering over `users.email` shows:

```
Column: users.email
Type: text
Nullable: yes
Primary Key: no
```

## Troubleshooting

### No schema completions

1. Check the **LQL Language Server** output channel (`View > Output > LQL Language Server`)
2. Verify your connection string is correct
3. Ensure PostgreSQL is running and accessible
4. Check firewall/network rules

### Connection string not picked up

1. VS Code settings take priority over environment variables
2. Restart VS Code after changing environment variables
3. Try the libpq format if other formats don't work

### Schema is stale

The schema is fetched once on startup. Restart the language server to refresh:
1. Open the command palette (`Ctrl+Shift+P`)
2. Run **Developer: Reload Window**
