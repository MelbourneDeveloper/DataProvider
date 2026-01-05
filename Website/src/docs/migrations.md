---
layout: layouts/docs.njk
title: "Migrations"
description: Database-agnostic schema migration framework for .NET applications.
---

A database-agnostic schema migration framework for .NET applications. Define your schema once in YAML, deploy to SQLite, PostgreSQL, or SQL Server.

## Overview

The Migration framework provides:

- **Database-agnostic definitions** - Single schema definition works across SQLite, PostgreSQL, SQL Server
- **Additive-only by default** - Safe upgrades that only add, never remove
- **Idempotent operations** - Running migrations multiple times produces same result
- **Schema introspection** - Compare desired schema against actual database state
- **YAML-based schemas** - Version control friendly, human-readable definitions

## Quick Start

### 1. Define Your Schema (YAML)

Create a `schema.yaml` file:

```yaml
name: MyApp
tables:
  - name: Users
    columns:
      - name: Id
        type: { kind: uuid }
        nullable: false
      - name: Email
        type: { kind: varchar, maxLength: 255 }
        nullable: false
      - name: Name
        type: { kind: varchar, maxLength: 100 }
        nullable: true
      - name: CreatedAt
        type: { kind: datetime }
        nullable: false
        default: "CURRENT_TIMESTAMP"
    primaryKey:
      columns: [Id]
    indexes:
      - name: IX_Users_Email
        columns: [Email]
        unique: true
```

### 2. Run the Migration CLI

```bash
dotnet run --project Migration.Cli -- \
  --schema schema.yaml \
  --output myapp.db \
  --provider sqlite
```

This creates the database and applies the schema.

### 3. Update Your Schema

Add new tables or columns to your YAML file. Run the CLI again - it will only apply the changes.

## CLI Reference

```bash
Migration.Cli --schema <path> --output <connection> --provider <sqlite|postgres|sqlserver>
```

| Option | Required | Description |
|--------|----------|-------------|
| `--schema` | Yes | Path to YAML schema file |
| `--output` | Yes | Database connection string or file path |
| `--provider` | Yes | Database provider: `sqlite`, `postgres`, `sqlserver` |

## YAML Schema Reference

### Schema Structure

```yaml
name: SchemaName          # Required: Schema identifier
tables:                   # Required: List of tables
  - name: TableName       # Required: Table name
    schema: public        # Optional: Schema namespace (default: none for SQLite)
    comment: Description  # Optional: Table documentation
    columns: [...]        # Required: Column definitions
    primaryKey: {...}     # Optional: Primary key definition
    indexes: [...]        # Optional: Index definitions
    foreignKeys: [...]    # Optional: Foreign key definitions
```

### Column Definition

```yaml
columns:
  - name: ColumnName           # Required
    type: { kind: ... }        # Required: Type definition (see Type Reference)
    nullable: true             # Optional: Allow NULL (default: true)
    default: "expression"      # Optional: SQL default expression
    identity:                  # Optional: Auto-increment
      seed: 1
      increment: 1
    computed:                  # Optional: Computed column
      expression: "Price * Quantity"
      persisted: false
    checkConstraint: "Age > 0" # Optional: Column-level CHECK
    collation: "NOCASE"        # Optional: String collation
    comment: "Description"     # Optional: Column documentation
```

### Type Reference

#### Simple Types (no parameters)

| Kind | Description |
|------|-------------|
| `tinyint` | 8-bit integer |
| `smallint` | 16-bit integer |
| `int` | 32-bit integer |
| `bigint` | 64-bit integer |
| `float` | Single precision float |
| `double` | Double precision float |
| `text` | Unlimited text |
| `blob` | Binary data |
| `date` | Date only |
| `uuid` | UUID/GUID |
| `boolean` | True/false |

#### Parameterized Types

| Kind | Parameters | Example |
|------|------------|---------|
| `char` | `length` | `{ kind: char, length: 10 }` |
| `varchar` | `maxLength` | `{ kind: varchar, maxLength: 255 }` |
| `decimal` | `precision`, `scale` | `{ kind: decimal, precision: 18, scale: 2 }` |
| `datetime` | `precision` | `{ kind: datetime, precision: 3 }` |

### Type Mapping by Platform

| Portable Type | SQLite | PostgreSQL | SQL Server |
|---------------|--------|------------|------------|
| `uuid` | TEXT | UUID | UNIQUEIDENTIFIER |
| `varchar(n)` | TEXT | VARCHAR(n) | VARCHAR(n) |
| `int` | INTEGER | INTEGER | INT |
| `bigint` | INTEGER | BIGINT | BIGINT |
| `decimal(p,s)` | REAL | NUMERIC(p,s) | DECIMAL(p,s) |
| `boolean` | INTEGER | BOOLEAN | BIT |
| `datetime` | TEXT | TIMESTAMP | DATETIME2 |
| `text` | TEXT | TEXT | NVARCHAR(MAX) |
| `blob` | BLOB | BYTEA | VARBINARY(MAX) |

### Primary Key Definition

```yaml
primaryKey:
  name: PK_TableName      # Optional: Constraint name
  columns: [Id]           # Required: Column(s) in the key
```

### Index Definition

```yaml
indexes:
  - name: IX_TableName_Column    # Required: Index name
    columns: [Column1, Column2]  # Required: Indexed columns
    unique: false                # Optional: Unique constraint (default: false)
    filter: "Status = 'active'"  # Optional: Partial index filter (Postgres/SQL Server)
```

### Foreign Key Definition

```yaml
foreignKeys:
  - name: FK_Orders_Users           # Optional: Constraint name
    columns: [UserId]               # Required: Local column(s)
    referencedTable: Users          # Required: Referenced table
    referencedColumns: [Id]         # Required: Referenced column(s)
    onDelete: Cascade               # Optional: NoAction, Cascade, SetNull, SetDefault, Restrict
    onUpdate: NoAction              # Optional: Same options as onDelete
```

## Complete Example

```yaml
name: Ecommerce
tables:
  - name: Users
    comment: Application users
    columns:
      - name: Id
        type: { kind: uuid }
        nullable: false
      - name: Email
        type: { kind: varchar, maxLength: 255 }
        nullable: false
      - name: Name
        type: { kind: varchar, maxLength: 100 }
        nullable: true
      - name: Age
        type: { kind: int }
        nullable: true
        checkConstraint: "Age >= 0"
      - name: Status
        type: { kind: varchar, maxLength: 20 }
        nullable: false
        default: "'active'"
      - name: CreatedAt
        type: { kind: datetime }
        nullable: false
        default: "CURRENT_TIMESTAMP"
    primaryKey:
      name: PK_Users
      columns: [Id]
    indexes:
      - name: IX_Users_Email
        columns: [Email]
        unique: true
      - name: IX_Users_Status
        columns: [Status]

  - name: Products
    columns:
      - name: Id
        type: { kind: uuid }
        nullable: false
      - name: Sku
        type: { kind: char, length: 12 }
        nullable: false
        comment: Stock keeping unit
      - name: Name
        type: { kind: varchar, maxLength: 200 }
        nullable: false
      - name: Price
        type: { kind: decimal, precision: 10, scale: 2 }
        nullable: false
        default: "0.00"
        checkConstraint: "Price >= 0"
      - name: Stock
        type: { kind: int }
        nullable: false
        default: "0"
    primaryKey:
      columns: [Id]
    indexes:
      - name: IX_Products_Sku
        columns: [Sku]
        unique: true

  - name: Orders
    columns:
      - name: Id
        type: { kind: uuid }
        nullable: false
      - name: UserId
        type: { kind: uuid }
        nullable: false
      - name: ProductId
        type: { kind: uuid }
        nullable: false
      - name: Quantity
        type: { kind: int }
        nullable: false
      - name: Total
        type: { kind: decimal, precision: 10, scale: 2 }
        nullable: false
      - name: Status
        type: { kind: varchar, maxLength: 20 }
        nullable: false
        default: "'pending'"
      - name: CreatedAt
        type: { kind: datetime }
        nullable: false
        default: "CURRENT_TIMESTAMP"
    primaryKey:
      columns: [Id]
    foreignKeys:
      - name: FK_Orders_Users
        columns: [UserId]
        referencedTable: Users
        referencedColumns: [Id]
        onDelete: Cascade
      - name: FK_Orders_Products
        columns: [ProductId]
        referencedTable: Products
        referencedColumns: [Id]
        onDelete: Restrict
    indexes:
      - name: IX_Orders_UserId
        columns: [UserId]
      - name: IX_Orders_Status
        columns: [Status]
```

## MSBuild Integration

Add migration targets to your `.csproj` to run migrations at build time:

```xml
<!-- Create database from YAML using Migration.Cli -->
<Target Name="CreateDatabaseSchema" BeforeTargets="BeforeCompile">
  <Exec Command='dotnet run --project path/to/Migration.Cli.csproj -- --schema "$(MSBuildProjectDirectory)/schema.yaml" --output "$(MSBuildProjectDirectory)/app.db" --provider sqlite' />
</Target>
```

## Design Principles

The Migration framework follows strict coding rules:

- **No exceptions** - All operations return `Result<T, MigrationError>`
- **Additive-only** - Destructive operations (DROP) require explicit opt-in
- **Idempotent** - Safe to run multiple times
- **Database-agnostic** - Same YAML works across all supported databases

## Architecture

```
+-----------------------------------------------------------+
|                    Application Layer                       |
+-----------------------------------------------------------+
|                    Migration Engine                        |
|  +-------------+ +-------------+ +-------------+           |
|  |   Schema    | |    Diff     | |   DDL       |           |
|  |  Definition | |   Engine    | | Generator   |           |
|  +-------------+ +-------------+ +-------------+           |
+-----------------------------------------------------------+
|                  Provider Layer                            |
|     +----------+  +----------+  +----------+               |
|     | SQLite   |  | Postgres |  | SqlServer|               |
|     | Provider |  | Provider |  | Provider |               |
|     +----------+  +----------+  +----------+               |
+-----------------------------------------------------------+
```

## Next Steps

- [DataProvider Documentation](/docs/dataprovider/) - Generate code from SQL
- [Sync Documentation](/docs/sync/) - Offline-first synchronization
- [LQL Documentation](/docs/lql/) - Cross-database query language
