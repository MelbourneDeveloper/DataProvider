# .NET Schema Migration Framework Specification

## Table of Contents

1. [Introduction](#1-introduction)
2. [Goals & Non-Goals](#2-goals--non-goals)
3. [Architecture Overview](#3-architecture-overview)
4. [Schema Definition Model](#4-schema-definition-model)
5. [Type System](#5-type-system)
6. [Schema Operations](#6-schema-operations)
7. [Migration Execution](#7-migration-execution)
8. [Diff Engine](#8-diff-engine)
9. [Database Providers](#9-database-providers)
10. [Error Handling](#10-error-handling)
11. [Conformance Requirements](#11-conformance-requirements)
12. [E2E Testing Requirements](#12-e2e-testing-requirements)
13. [Appendices](#13-appendices)

---

## 1. Introduction

This specification defines a database-agnostic schema migration framework for .NET applications. The framework enables declarative schema definitions that can create databases from scratch or upgrade existing databases through additive migrations.

### 1.1 Scope

This specification covers:

- Database-agnostic schema definitions (tables, columns, indexes, keys, triggers, functions, etc.)
- Schema creation from scratch (greenfield deployments)
- Additive schema upgrades (adding columns, tables, indexes)
- Schema introspection and diff calculation
- Platform-specific DDL generation (SQLite, PostgreSQL, SQL Server)

This specification does **not** cover:

- Destructive migrations (dropping columns, tables) - these require explicit opt-in
- Data migrations (transforming existing data)
- Rollback mechanisms (out of scope for v1)

### 1.2 Relationship to Other Frameworks

The Migration framework is **independent** but serves as a foundation for:

- **Sync Framework**: Uses Migration to create sync infrastructure tables (`_sync_log`, `_sync_state`, etc.)
- **DataProvider**: Uses schema introspection for code generation
- **LQL**: Can leverage schema metadata for query validation

---

## 2. Goals & Non-Goals

### 2.1 Goals

- **Database-agnostic definitions**: Single schema definition works across SQLite, PostgreSQL, SQL Server
- **Additive-only by default**: Safe upgrades that only add, never remove
- **Idempotent operations**: Running migrations multiple times produces same result
- **Introspection-first**: Compare desired schema against actual database state
- **Explicit over implicit**: No magic - every operation is visible and auditable
- **Zero dependencies**: Pure .NET, no external migration tools outside this repo. But should use other libraries in this repo.

### 2.2 Non-Goals

- **ORM functionality**: This is schema management only, not data access
- **Automatic rollbacks**: Destructive operations require explicit handling
- **Migration history tables**: Version tracking is application responsibility
- **Complex data transforms**: Use LQL scripts or application code for data migration

---

## 3. Architecture Overview

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
|                    Database Layer                          |
|              (SQLite / PostgreSQL / SQL Server)            |
+-----------------------------------------------------------+
```

---

## 4. Schema Definition Model

### 4.1 Core Records

Schema is defined using immutable records. The key types are:

- **SchemaDefinition** - Root container with schema name and list of tables
- **TableDefinition** - Table with columns, indexes, foreign keys, primary key, unique constraints, check constraints, and optional comment
- **ColumnDefinition** - Column with name, portable type, nullable flag, default value, identity settings, computed expression, collation, check constraint, and comment
- **IndexDefinition** - Index with name, columns, unique flag, and optional filter (partial index)
- **ForeignKeyDefinition** - FK with columns, referenced table/columns, and ON DELETE/UPDATE actions
- **PrimaryKeyDefinition** - PK with optional name and column list
- **UniqueConstraintDefinition** - Unique constraint with columns
- **CheckConstraintDefinition** - Check constraint with SQL boolean expression

Foreign key actions: `NoAction`, `Cascade`, `SetNull`, `SetDefault`, `Restrict`

### 4.2 Fluent Builder (Optional)

For ergonomic schema definition:

```csharp
var schema = Schema.Define("MyApp")
    .Table("Person", t => t
        .Column("Id", PortableType.Uuid, c => c.PrimaryKey())
        .Column("Name", PortableType.String(100), c => c.NotNull())
        .Column("Email", PortableType.String(255))
        .Column("CreatedAt", PortableType.DateTime, c => c.NotNull().Default("CURRENT_TIMESTAMP"))
        .Index("idx_person_email", "Email", unique: true)
    )
    .Table("Order", t => t
        .Column("Id", PortableType.Uuid, c => c.PrimaryKey())
        .Column("PersonId", PortableType.Uuid, c => c.NotNull())
        .Column("Total", PortableType.Decimal(10, 2), c => c.NotNull())
        .ForeignKey("PersonId", "Person", "Id", onDelete: ForeignKeyAction.Cascade)
    )
    .Build();
```

### 4.3 YAML Schema Format

Schema files use YAML format. See `migration_exe_spec.md` for CLI usage. The YAML format mirrors the C# records:

```yaml
name: MyApp
tables:
  - schema: public
    name: Product
    comment: Product catalog
    columns:
      - name: Id
        type: { kind: bigint }
        nullable: false
        identity: { seed: 1, increment: 1 }
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
      - name: IsActive
        type: { kind: boolean }
        nullable: false
        default: "true"
    primaryKey:
      name: PK_Product
      columns: [Id]
    indexes:
      - name: IX_Product_Sku
        columns: [Sku]
        unique: true
    foreignKeys: []
```

### 4.4 YAML Type Reference

Type definitions use the `kind` property to discriminate:

#### Types with NO parameters

| Type Kind | Example |
|-----------|---------|
| `tinyint` | `kind: tinyint` |
| `smallint` | `kind: smallint` |
| `int` | `kind: int` |
| `bigint` | `kind: bigint` |
| `float` | `kind: float` |
| `double` | `kind: double` |
| `text` | `kind: text` |
| `blob` | `kind: blob` |
| `date` | `kind: date` |
| `uuid` | `kind: uuid` |
| `boolean` | `kind: boolean` |

#### Types with parameters

| Type Kind | Parameters | Example |
|-----------|------------|---------|
| `char` | `length` | `{ kind: char, length: 10 }` |
| `varchar` | `maxLength` | `{ kind: varchar, maxLength: 255 }` |
| `decimal` | `precision`, `scale` | `{ kind: decimal, precision: 18, scale: 2 }` |
| `datetime` | `precision` | `{ kind: datetime, precision: 3 }` |

### 4.5 Column Property Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `name` | string | (required) | Column name |
| `type` | object | (required) | Type definition (see 4.4) |
| `nullable` | boolean | `true` | Allow NULL values |
| `default` | string | `null` | SQL default expression |
| `identity.seed` | integer | `1` | Auto-increment start |
| `identity.increment` | integer | `1` | Auto-increment step |
| `computed.expression` | string | `null` | Computed column SQL |
| `computed.persisted` | boolean | `false` | Store computed value |
| `checkConstraint` | string | `null` | Column-level CHECK |
| `collation` | string | `null` | String collation |
| `comment` | string | `null` | Documentation |

---

## 5. Type System

### 5.1 Portable Types

The type system uses **discriminated unions** where each type record carries exactly the metadata it needs. Types without parameters (like `BigIntType`) have none. Types with parameters (like `DecimalType(int Precision, int Scale)`) carry only what they need.

Pattern match on type to generate platform-specific DDL:

```csharp
public static string ToSqlServerType(PortableType type) => type switch
{
    BigIntType => "BIGINT",
    DecimalType(var p, var s) => $"DECIMAL({p},{s})",
    VarCharType(var max) => $"VARCHAR({max})",
    TextType => "NVARCHAR(MAX)",
    UuidType => "UNIQUEIDENTIFIER",
    // ... etc
};
```

### 5.2 Type Mapping Table

Complete mapping of all discriminated union types to platform-specific DDL:

#### Integer Types

| Portable Type | SQLite | PostgreSQL | SQL Server |
|---------------|--------|------------|------------|
| `TinyIntType` | INTEGER | SMALLINT | TINYINT |
| `SmallIntType` | INTEGER | SMALLINT | SMALLINT |
| `IntType` | INTEGER | INTEGER | INT |
| `BigIntType` | INTEGER | BIGINT | BIGINT |

#### Exact Numeric Types

| Portable Type | SQLite | PostgreSQL | SQL Server |
|---------------|--------|------------|------------|
| `DecimalType(p,s)` | REAL | NUMERIC(p,s) | DECIMAL(p,s) |
| `MoneyType` | REAL | NUMERIC(19,4) | MONEY |
| `SmallMoneyType` | REAL | NUMERIC(10,4) | SMALLMONEY |

#### Floating Point Types

| Portable Type | SQLite | PostgreSQL | SQL Server |
|---------------|--------|------------|------------|
| `FloatType` | REAL | REAL | REAL |
| `DoubleType` | REAL | DOUBLE PRECISION | FLOAT |

#### String Types

| Portable Type | SQLite | PostgreSQL | SQL Server | Notes |
|---------------|--------|------------|------------|-------|
| `CharType(n)` | TEXT | CHAR(n) | CHAR(n) | Fixed-length, padded |
| `VarCharType(n)` | TEXT | VARCHAR(n) | VARCHAR(n) | Variable, single-byte |
| `NCharType(n)` | TEXT | CHAR(n) | NCHAR(n) | Fixed-length, Unicode |
| `NVarCharType(n)` | TEXT | VARCHAR(n) | NVARCHAR(n) | Variable, Unicode |
| `NVarCharType(MAX)` | TEXT | TEXT | NVARCHAR(MAX) | n = int.MaxValue |
| `TextType` | TEXT | TEXT | NVARCHAR(MAX) | Unlimited |

#### Binary Types

| Portable Type | SQLite | PostgreSQL | SQL Server | Notes |
|---------------|--------|------------|------------|-------|
| `BinaryType(n)` | BLOB | BYTEA | BINARY(n) | Fixed-length |
| `VarBinaryType(n)` | BLOB | BYTEA | VARBINARY(n) | Variable |
| `VarBinaryType(MAX)` | BLOB | BYTEA | VARBINARY(MAX) | n = int.MaxValue |
| `BlobType` | BLOB | BYTEA | VARBINARY(MAX) | Unlimited |

#### Date/Time Types

| Portable Type | SQLite | PostgreSQL | SQL Server | Notes |
|---------------|--------|------------|------------|-------|
| `DateType` | TEXT | DATE | DATE | Date only |
| `TimeType(p)` | TEXT | TIME(p) | TIME(p) | p = 0-7 precision |
| `DateTimeType(p)` | TEXT | TIMESTAMP | DATETIME2(p) | p = 0-7 precision |
| `DateTimeOffsetType` | TEXT | TIMESTAMPTZ | DATETIMEOFFSET | With timezone |
| `RowVersionType` | BLOB | BYTEA | ROWVERSION | Concurrency token |

#### Other Types

| Portable Type | SQLite | PostgreSQL | SQL Server | Notes |
|---------------|--------|------------|------------|-------|
| `UuidType` | TEXT | UUID | UNIQUEIDENTIFIER | 128-bit GUID |
| `BooleanType` | INTEGER | BOOLEAN | BIT | True/false |
| `JsonType` | TEXT | JSONB | NVARCHAR(MAX) | JSON document |
| `XmlType` | TEXT | XML | XML | XML document |
| `EnumType(name, vals)` | TEXT | {name} | NVARCHAR(100) | + CHECK constraint |
| `GeometryType(srid)` | BLOB | GEOMETRY | GEOMETRY | Spatial data |
| `GeographyType(srid)` | BLOB | GEOGRAPHY | GEOGRAPHY | Earth-surface GIS |

#### SQLite Type Affinity Notes

SQLite uses type affinity rather than strict types. The migration framework stores the full portable type in metadata to preserve precision/length information even though SQLite only has 5 storage classes:

| SQLite Affinity | Storage | Portable Types Mapped |
|-----------------|---------|----------------------|
| INTEGER | 64-bit signed | All int types, boolean |
| REAL | 64-bit float | Float, double, decimal |
| TEXT | UTF-8/16 string | All string types, datetime, uuid, json, xml, enum |
| BLOB | Raw bytes | All binary types, geometry, geography |
| NULL | Null value | (any nullable column) |

To preserve type metadata for upgrades, store the original portable type definition in a `__schema_metadata` table.

### 5.3 Identity/Auto-Increment

Identity columns are handled per-platform:

| Platform | Identity Syntax |
|----------|----------------|
| SQLite | `INTEGER PRIMARY KEY` (implicit ROWID alias) |
| PostgreSQL | `SERIAL` / `BIGSERIAL` or `GENERATED ALWAYS AS IDENTITY` |
| SQL Server | `IDENTITY(1,1)` |

---

## 6. Schema Operations

### 6.1 Operation Types

The diff engine produces a list of schema operations as discriminated union records:

- **Table**: `CreateTable`, `DropTable`
- **Column**: `AddColumn`, `DropColumn`, `AlterColumn`
- **Index**: `CreateIndex`, `DropIndex`
- **Constraint**: `AddPrimaryKey`, `DropPrimaryKey`, `AddForeignKey`, `DropForeignKey`

All operations carry the schema name, table name, and relevant definition or constraint name.

### 6.2 Additive-Only Mode (Default)

By default, the migration engine only applies **additive** operations:

| Operation | Allowed by Default |
|-----------|-------------------|
| `CreateTable` | Yes |
| `AddColumn` | Yes |
| `CreateIndex` | Yes |
| `AddPrimaryKey` | Yes |
| `AddForeignKey` | Yes |
| `DropTable` | **No** - requires explicit opt-in |
| `DropColumn` | **No** - requires explicit opt-in |
| `DropIndex` | **No** - requires explicit opt-in |
| `AlterColumn` | **No** - requires explicit opt-in |

### 6.3 Destructive Operations

Destructive operations require explicit opt-in via `MigrationOptions`:

- `AllowDropTable` (default: false)
- `AllowDropColumn` (default: false)
- `AllowDropIndex` (default: false)
- `AllowAlterColumn` (default: false)

---

## 7. Migration Execution

### 7.1 Migration Runner

`MigrationRunner` executes operations with transaction handling. Key methods:

- `Apply(connection, operations, options, logger)` → `MigrationResult`
- `GenerateDdl(operations, platform)` → `Result<string, MigrationError>` (preview without executing)

### 7.2 Transaction Strategy

| Platform | Transaction Behavior |
|----------|---------------------|
| SQLite | Single transaction for all DDL (SQLite supports transactional DDL) |
| PostgreSQL | Single transaction for all DDL (PostgreSQL supports transactional DDL) |
| SQL Server | Per-statement (SQL Server DDL has transaction limitations) |

### 7.3 Execution Flow

```
1. Validate all operations against options (fail fast for disallowed destructive ops)
2. Begin transaction (if supported)
3. For each operation:
   a. Generate platform-specific DDL
   b. Log operation details
   c. Execute DDL
   d. Verify success
4. Commit transaction (or rollback on error)
5. Return result with applied operations
```

---

## 8. Diff Engine

### 8.1 Schema Comparison

`SchemaDiff.Calculate(current, desired)` compares desired schema against current database state and returns the list of operations needed to transform current into desired.

### 8.2 Comparison Rules

| Element | Comparison Logic |
|---------|-----------------|
| Tables | Match by schema + name (case-insensitive) |
| Columns | Match by name within table (case-insensitive) |
| Indexes | Match by name (case-insensitive) |
| Primary Keys | Match by table (only one per table) |
| Foreign Keys | Match by name (case-insensitive) |

### 8.3 Diff Algorithm

```
For each table in desired schema:
    If table not in current:
        Emit CreateTable
    Else:
        For each column in desired table:
            If column not in current table:
                Emit AddColumn
            Else if column differs:
                Emit AlterColumn

        For each column in current table not in desired:
            Emit DropColumn

        Compare indexes, primary key, foreign keys similarly

For each table in current not in desired:
    Emit DropTable
```

### 8.4 Schema Introspection

Each provider implements `SchemaInspector.Inspect(connection, logger)` → `Result<SchemaDefinition, MigrationError>` to read the current database schema.

### 8.5 Schema Capture to Metadata

**CRITICAL REQUIREMENT**: The migration framework MUST support capturing an existing database schema and persisting it to metadata. This enables:

1. **Brownfield Adoption**: Capture schema from existing production databases
2. **Schema Versioning**: Store captured schema as JSON for version control
3. **Cross-Platform Migration**: Capture from one platform, apply to another
4. **Audit Trail**: Record point-in-time schema snapshots

#### 8.5.1 Schema Capture Workflow

1. Connect to existing database
2. Call `SchemaInspector.Inspect()` to capture current schema
3. Call `SchemaSerializer.ToYaml()` to serialize for version control
4. Later: `SchemaSerializer.FromYaml()` to load, then `SchemaDiff.Calculate()` and `MigrationRunner.Apply()`

#### 8.5.2 Captured Schema Contents

The schema inspector MUST capture:

| Element | Required |
|---------|----------|
| All tables in schema | Yes |
| All columns with types | Yes |
| Primary keys | Yes |
| Indexes (non-primary) | Yes |
| Foreign keys with actions | Yes |
| Default values | Yes |
| NOT NULL constraints | Yes |
| Identity/auto-increment | Yes |

---

## 9. Database Providers

### 9.1 Provider Interface

`DdlGenerator.Generate(operation, platform)` produces platform-specific DDL SQL.

Platforms: `SQLite`, `PostgreSQL`, `SqlServer`

### 9.2 SQLite Provider

SQLite-specific considerations:

- No native UUID type (uses TEXT)
- No native BOOLEAN type (uses INTEGER 0/1)
- No ALTER COLUMN support (requires table rebuild)
- No DROP COLUMN before SQLite 3.35 (requires table rebuild)
- Transactional DDL supported

### 9.3 PostgreSQL Provider

PostgreSQL-specific considerations:

- Native UUID, BOOLEAN, JSONB types
- Full ALTER COLUMN support
- Partial index support
- Transactional DDL supported
- Case-sensitive identifiers (lowercase by default)

### 9.4 SQL Server Provider

SQL Server-specific considerations:

- NVARCHAR for Unicode strings
- UNIQUEIDENTIFIER for UUIDs
- Limited transactional DDL
- Schema support (dbo, etc.)

---

## 10. Error Handling

### 10.1 Error Types

All errors extend `MigrationError(Message)`:

- **IntrospectionError** - Failed to read database schema
- **DdlGenerationError** - Failed to generate DDL for an operation
- **ExecutionError** - DDL execution failed (includes SQL that failed)
- **ValidationError** - Operation not allowed (e.g., destructive op without opt-in)

### 10.2 Result Types

All operations return Result types (never throw). Common aliases:

- `MigrationResult = Result<MigrationSummary, MigrationError>`
- `InspectionResult = Result<SchemaDefinition, MigrationError>`
- `DdlResult = Result<string, MigrationError>`

---

## 11. Conformance Requirements

An implementation is **conformant** if:

1. Schema definitions are database-agnostic records
2. All portable types map correctly to each supported platform
3. Diff engine correctly identifies additive operations
4. Destructive operations require explicit opt-in
5. DDL generation produces valid SQL for each platform
6. Migration runner handles transactions appropriately per platform
7. Schema introspection correctly reads existing schema
8. All operations return Result types (never throw for expected errors)
9. All public members have XML documentation
10. Logging via ILogger at appropriate levels
11. E2E tests cover greenfield creation and upgrade scenarios
12. E2E tests run against real databases (SQLite in-memory, PostgreSQL via Testcontainers)

---

## 12. E2E Testing Requirements

End-to-end tests are **critical** for validating that migrations work correctly against real databases. No mocks allowed.

### 12.1 Test Categories

| Category | Description |
|----------|-------------|
| **Greenfield** | Create database from scratch using schema definition |
| **Upgrade** | Add tables/columns/indexes to existing database |
| **Idempotency** | Run same migration twice, verify no errors |
| **Cross-Platform** | Same schema definition works on SQLite, PostgreSQL, SQL Server |
| **Introspection** | Verify inspected schema matches created schema |

### 12.2 Greenfield Tests

Create fresh database, define schema with fluent API, apply via `SchemaDiff.Calculate()` + `MigrationRunner.Apply()`, verify tables exist via introspection.

### 12.3 Upgrade Tests

Apply v1 schema, then v2 with new columns. Verify diff produces `AddColumn` operations and final schema has all columns.

### 12.4 PostgreSQL Tests with Testcontainers

Use `Testcontainers.PostgreSql` to spin up real PostgreSQL. Verify native types (UUID, JSONB, TIMESTAMPTZ) are created correctly by querying `information_schema.columns`.

### 12.5 Idempotency Tests

Run migration twice. Second run should produce zero operations (schema already matches desired state).

### 12.6 Cross-Platform Test Matrix

Use `[Theory]` with `[MemberData]` to run the same schema definition against SQLite, PostgreSQL, and SQL Server. Verify identical results across all platforms.

### 12.7 Required Test Coverage

An implementation MUST include tests for:

| Scenario | SQLite | PostgreSQL | SQL Server |
|----------|--------|------------|------------|
| Create single table | Required | Required | Required |
| Create table with all portable types | Required | Required | Required |
| Create table with indexes | Required | Required | Required |
| Create table with foreign keys | Required | Required | Required |
| Add column to existing table | Required | Required | Required |
| Add index to existing table | Required | Required | Required |
| Add foreign key to existing table | Required | Required | Required |
| Idempotent migration | Required | Required | Required |
| Introspect and round-trip schema | Required | Required | Required |
| **Schema capture from existing DB** | Required | Required | Required |
| **Schema serialize to YAML metadata** | Required | Required | Required |
| **Destructive op returns useful error** | Required | Required | Required |

---

## 13. Schema Capture and Metadata

A critical feature of the Migration framework is the ability to **capture existing database schemas** and serialize them to YAML. This enables:

1. **Brownfield scenarios** - Capture existing database schema before applying migrations
2. **Schema versioning** - Store schema snapshots in source control
3. **Documentation** - Generate schema documentation from metadata
4. **Validation** - Compare captured schema against expected schema
5. **CI/CD** - Verify schema matches expected state in deployment pipelines

### 13.1 Schema Serializer

`SchemaSerializer.ToYaml(schema)` and `SchemaSerializer.FromYaml(yaml)` enable round-trip serialization for version control and brownfield adoption.

### 13.2 Required Schema Capture Tests

1. **Capture existing database** - Create DB with raw SQL, call inspector, verify complete schema returned
2. **YAML round-trip** - Serialize schema to YAML, deserialize, verify equality

---

## 14. Appendices

### Appendix A: Sync Framework Schema

The Sync framework uses Migration to create infrastructure tables: `_sync_state`, `_sync_session`, `_sync_log`, `_sync_clients`, `_sync_subscriptions`. See Sync framework documentation for details.

### Appendix B: Example Usage

Typical workflow:

1. Define schema with fluent `Schema.Define()` API
2. Open connection, call `SchemaInspector.Inspect()` to get current state
3. Call `SchemaDiff.Calculate(current, desired)` to get operations
4. Call `MigrationRunner.Apply()` with operations and options
5. Log applied operation count from result

### Appendix C: Platform-Specific DDL Examples

**Create Table - SQLite:**
```sql
CREATE TABLE IF NOT EXISTS Users (
    Id TEXT PRIMARY KEY,
    Email TEXT NOT NULL,
    Name TEXT,
    CreatedAt TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email ON Users(Email);
```

**Create Table - PostgreSQL:**
```sql
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY,
    email VARCHAR(255) NOT NULL,
    name VARCHAR(100),
    created_at TIMESTAMP NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email ON users(email);
```

**Create Table - SQL Server:**
```sql
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Email NVARCHAR(255) NOT NULL,
    Name NVARCHAR(100),
    CreatedAt DATETIME2 NOT NULL
);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_users_email')
CREATE UNIQUE INDEX idx_users_email ON Users(Email);
```

---

## References

- [SQLite CREATE TABLE](https://sqlite.org/lang_createtable.html)
- [PostgreSQL CREATE TABLE](https://www.postgresql.org/docs/current/sql-createtable.html)
- [SQL Server CREATE TABLE](https://docs.microsoft.com/en-us/sql/t-sql/statements/create-table-transact-sql)
- [Prisma Migrate](https://www.prisma.io/docs/orm/prisma-migrate)
