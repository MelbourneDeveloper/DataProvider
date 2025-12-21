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

### 1.3 Inspiration

This framework draws inspiration from:

- Prisma Migrate (declarative schema approach)
- EF Core Migrations (diff-based upgrades)
- Flyway (version-based migrations)

However, it follows its own patterns per the codebase conventions (FP style, no classes, Result types).

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

### 3.1 Core Components

| Component | Responsibility |
|-----------|---------------|
| **Schema Definition** | Database-agnostic model of tables, columns, indexes, keys |
| **Schema Inspector** | Reads current database schema into definition model |
| **Diff Engine** | Compares desired vs actual schema, produces change set |
| **DDL Generator** | Converts change set to platform-specific SQL |
| **Migration Runner** | Executes DDL with proper transaction handling |

---

## 4. Schema Definition Model

### 4.1 Core Records

Schema is defined using immutable records (per CLAUDE.md - no classes):

```csharp
/// <summary>
/// Complete database schema definition.
/// </summary>
public sealed record SchemaDefinition
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<TableDefinition> Tables { get; init; } = [];
}

/// <summary>
/// Single table definition with columns, indexes, and all constraints.
/// </summary>
public sealed record TableDefinition
{
    /// <summary>Database schema (e.g., "public", "dbo").</summary>
    public string Schema { get; init; } = "public";

    /// <summary>Table name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Column definitions in order.</summary>
    public IReadOnlyList<ColumnDefinition> Columns { get; init; } = [];

    /// <summary>Index definitions.</summary>
    public IReadOnlyList<IndexDefinition> Indexes { get; init; } = [];

    /// <summary>Foreign key constraints.</summary>
    public IReadOnlyList<ForeignKeyDefinition> ForeignKeys { get; init; } = [];

    /// <summary>Primary key constraint.</summary>
    public PrimaryKeyDefinition? PrimaryKey { get; init; }

    /// <summary>Unique constraints (semantic alternative to unique indexes).</summary>
    public IReadOnlyList<UniqueConstraintDefinition> UniqueConstraints { get; init; } = [];

    /// <summary>Table-level check constraints (multi-column).</summary>
    public IReadOnlyList<CheckConstraintDefinition> CheckConstraints { get; init; } = [];

    /// <summary>Table comment/description for documentation.</summary>
    public string? Comment { get; init; }
}

/// <summary>
/// Column definition with type and all database-agnostic constraints.
/// </summary>
public sealed record ColumnDefinition
{
    /// <summary>Column name (case-insensitive for comparison).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Portable type with full precision/scale/length info.</summary>
    public PortableType Type { get; init; }

    /// <summary>Whether NULL values are allowed.</summary>
    public bool IsNullable { get; init; } = true;

    /// <summary>SQL default expression (platform-specific, e.g., "CURRENT_TIMESTAMP").</summary>
    public string? DefaultValue { get; init; }

    /// <summary>Auto-increment/identity column.</summary>
    public bool IsIdentity { get; init; }

    /// <summary>Identity seed value (starting number).</summary>
    public long IdentitySeed { get; init; } = 1;

    /// <summary>Identity increment value.</summary>
    public long IdentityIncrement { get; init; } = 1;

    /// <summary>Computed column expression (if computed).</summary>
    public string? ComputedExpression { get; init; }

    /// <summary>Whether computed column is persisted/stored.</summary>
    public bool IsComputedPersisted { get; init; }

    /// <summary>Collation for string columns (e.g., "NOCASE", "en_US.UTF-8").</summary>
    public string? Collation { get; init; }

    /// <summary>Check constraint expression for this column only.</summary>
    public string? CheckConstraint { get; init; }

    /// <summary>Column comment/description for documentation.</summary>
    public string? Comment { get; init; }
}

/// <summary>
/// Check constraint that spans multiple columns.
/// </summary>
public sealed record CheckConstraintDefinition
{
    /// <summary>Constraint name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>SQL boolean expression (e.g., "Price >= 0 AND Quantity >= 0").</summary>
    public string Expression { get; init; } = string.Empty;
}

/// <summary>
/// Unique constraint (alternative to unique index for semantic clarity).
/// </summary>
public sealed record UniqueConstraintDefinition
{
    /// <summary>Constraint name.</summary>
    public string? Name { get; init; }

    /// <summary>Columns that must be unique together.</summary>
    public IReadOnlyList<string> Columns { get; init; } = [];
}

/// <summary>
/// Primary key constraint definition.
/// </summary>
public sealed record PrimaryKeyDefinition
{
    public string? Name { get; init; }
    public IReadOnlyList<string> Columns { get; init; } = [];
}

/// <summary>
/// Index definition (unique or non-unique).
/// </summary>
public sealed record IndexDefinition
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Columns { get; init; } = [];
    public bool IsUnique { get; init; }
    public string? Filter { get; init; }  // Partial index WHERE clause
}

/// <summary>
/// Foreign key constraint definition.
/// </summary>
public sealed record ForeignKeyDefinition
{
    public string? Name { get; init; }
    public IReadOnlyList<string> Columns { get; init; } = [];
    public string ReferencedTable { get; init; } = string.Empty;
    public string ReferencedSchema { get; init; } = "public";
    public IReadOnlyList<string> ReferencedColumns { get; init; } = [];
    public ForeignKeyAction OnDelete { get; init; } = ForeignKeyAction.NoAction;
    public ForeignKeyAction OnUpdate { get; init; } = ForeignKeyAction.NoAction;
}

/// <summary>
/// Foreign key referential action.
/// </summary>
public enum ForeignKeyAction
{
    NoAction,
    Cascade,
    SetNull,
    SetDefault,
    Restrict
}
```

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

### 4.3 JSON Schema Format

Schema can also be defined as JSON for tooling/UI integration. The JSON format mirrors the C# records exactly:

```json
{
    "name": "MyApp",
    "tables": [
        {
            "schema": "public",
            "name": "Product",
            "comment": "Product catalog",
            "columns": [
                {
                    "name": "Id",
                    "type": { "kind": "bigint" },
                    "nullable": false,
                    "identity": {
                        "seed": 1,
                        "increment": 1
                    }
                },
                {
                    "name": "Sku",
                    "type": { "kind": "char", "length": 12, "fixed": true },
                    "nullable": false,
                    "comment": "Stock keeping unit"
                },
                {
                    "name": "Name",
                    "type": { "kind": "string", "maxLength": 200 },
                    "nullable": false
                },
                {
                    "name": "Description",
                    "type": { "kind": "text" },
                    "nullable": true
                },
                {
                    "name": "Price",
                    "type": { "kind": "decimal", "precision": 10, "scale": 2 },
                    "nullable": false,
                    "default": "0.00",
                    "checkConstraint": "Price >= 0"
                },
                {
                    "name": "Weight",
                    "type": { "kind": "decimal", "precision": 8, "scale": 3 },
                    "nullable": true,
                    "checkConstraint": "Weight IS NULL OR Weight > 0"
                },
                {
                    "name": "IsActive",
                    "type": { "kind": "boolean" },
                    "nullable": false,
                    "default": "true"
                },
                {
                    "name": "Metadata",
                    "type": { "kind": "json" },
                    "nullable": true
                },
                {
                    "name": "CreatedAt",
                    "type": { "kind": "datetime", "precision": 3 },
                    "nullable": false,
                    "default": "CURRENT_TIMESTAMP"
                },
                {
                    "name": "ModifiedAt",
                    "type": { "kind": "datetimeoffset" },
                    "nullable": true
                },
                {
                    "name": "FullText",
                    "type": { "kind": "string", "maxLength": 500 },
                    "nullable": true,
                    "computed": {
                        "expression": "Name + ' ' + COALESCE(Description, '')",
                        "persisted": false
                    }
                },
                {
                    "name": "RowVersion",
                    "type": { "kind": "timestamp" },
                    "nullable": false
                }
            ],
            "primaryKey": {
                "name": "PK_Product",
                "columns": ["Id"]
            },
            "indexes": [
                {
                    "name": "IX_Product_Sku",
                    "columns": ["Sku"],
                    "unique": true
                },
                {
                    "name": "IX_Product_Active_Name",
                    "columns": ["IsActive", "Name"],
                    "unique": false,
                    "filter": "IsActive = 1"
                }
            ],
            "uniqueConstraints": [
                {
                    "name": "UQ_Product_Name",
                    "columns": ["Name"]
                }
            ],
            "checkConstraints": [
                {
                    "name": "CK_Product_ValidPrice",
                    "expression": "Price >= 0 AND (Weight IS NULL OR Weight > 0)"
                }
            ]
        },
        {
            "schema": "public",
            "name": "OrderItem",
            "columns": [
                {
                    "name": "Id",
                    "type": { "kind": "uuid" },
                    "nullable": false,
                    "default": "NEWID()"
                },
                {
                    "name": "OrderId",
                    "type": { "kind": "uuid" },
                    "nullable": false
                },
                {
                    "name": "ProductId",
                    "type": { "kind": "bigint" },
                    "nullable": false
                },
                {
                    "name": "Quantity",
                    "type": { "kind": "int" },
                    "nullable": false,
                    "checkConstraint": "Quantity > 0"
                },
                {
                    "name": "UnitPrice",
                    "type": { "kind": "decimal", "precision": 10, "scale": 2 },
                    "nullable": false
                },
                {
                    "name": "LineTotal",
                    "type": { "kind": "decimal", "precision": 12, "scale": 2 },
                    "nullable": false,
                    "computed": {
                        "expression": "Quantity * UnitPrice",
                        "persisted": true
                    }
                }
            ],
            "primaryKey": {
                "columns": ["Id"]
            },
            "foreignKeys": [
                {
                    "name": "FK_OrderItem_Product",
                    "columns": ["ProductId"],
                    "referencedTable": "Product",
                    "referencedSchema": "public",
                    "referencedColumns": ["Id"],
                    "onDelete": "Restrict",
                    "onUpdate": "Cascade"
                }
            ]
        }
    ]
}
```

### 4.4 JSON Type Schema Reference (Discriminated Unions)

Type definitions in JSON format match the C# discriminated unions. The `kind` property discriminates the type, and each type has exactly the properties it needs:

#### Types with NO parameters

| Type Kind | Properties | Example |
|-----------|-----------|---------|
| `tinyint` | (none) | `{ "kind": "tinyint" }` |
| `smallint` | (none) | `{ "kind": "smallint" }` |
| `int` | (none) | `{ "kind": "int" }` |
| `bigint` | (none) | `{ "kind": "bigint" }` |
| `float` | (none) | `{ "kind": "float" }` |
| `double` | (none) | `{ "kind": "double" }` |
| `money` | (none) | `{ "kind": "money" }` |
| `smallmoney` | (none) | `{ "kind": "smallmoney" }` |
| `text` | (none) | `{ "kind": "text" }` |
| `blob` | (none) | `{ "kind": "blob" }` |
| `date` | (none) | `{ "kind": "date" }` |
| `datetimeoffset` | (none) | `{ "kind": "datetimeoffset" }` |
| `rowversion` | (none) | `{ "kind": "rowversion" }` |
| `uuid` | (none) | `{ "kind": "uuid" }` |
| `boolean` | (none) | `{ "kind": "boolean" }` |
| `json` | (none) | `{ "kind": "json" }` |
| `xml` | (none) | `{ "kind": "xml" }` |

#### Types with LENGTH parameter

| Type Kind | Required | Example |
|-----------|----------|---------|
| `char` | `length` (int) | `{ "kind": "char", "length": 10 }` |
| `nchar` | `length` (int) | `{ "kind": "nchar", "length": 50 }` |
| `binary` | `length` (int) | `{ "kind": "binary", "length": 16 }` |

#### Types with MAXLENGTH parameter

| Type Kind | Required | Example |
|-----------|----------|---------|
| `varchar` | `maxLength` (int) | `{ "kind": "varchar", "maxLength": 255 }` |
| `nvarchar` | `maxLength` (int) | `{ "kind": "nvarchar", "maxLength": 100 }` |
| `varbinary` | `maxLength` (int) | `{ "kind": "varbinary", "maxLength": 8000 }` |

For MAX length, use `2147483647` (int.MaxValue):
```json
{ "kind": "nvarchar", "maxLength": 2147483647 }
```

#### Types with PRECISION parameter

| Type Kind | Required | Example |
|-----------|----------|---------|
| `time` | `precision` (0-7) | `{ "kind": "time", "precision": 3 }` |
| `datetime` | `precision` (0-7) | `{ "kind": "datetime", "precision": 3 }` |

#### Types with PRECISION and SCALE parameters

| Type Kind | Required | Example |
|-----------|----------|---------|
| `decimal` | `precision`, `scale` | `{ "kind": "decimal", "precision": 18, "scale": 2 }` |

#### Types with SPECIAL parameters

| Type Kind | Required | Example |
|-----------|----------|---------|
| `enum` | `name`, `values` | `{ "kind": "enum", "name": "OrderStatus", "values": ["Pending", "Shipped", "Delivered"] }` |
| `geometry` | `srid` (optional) | `{ "kind": "geometry", "srid": 4326 }` |
| `geography` | `srid` (default 4326) | `{ "kind": "geography", "srid": 4326 }` |

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

### 5.1 Portable Types (Discriminated Unions)

The type system uses **discriminated unions** where each type record carries exactly the metadata it needs - no more, no less. Types that don't need parameters have no parameters. Types that need length have length. Types that need precision and scale have both.

```csharp
/// <summary>
/// Database-agnostic type definition. Base sealed type for discriminated union.
/// Pattern match on derived types to extract type-specific metadata.
/// </summary>
public abstract record PortableType;

// ═══════════════════════════════════════════════════════════════════
// INTEGER TYPES - No parameters needed (bit size is implicit in type)
// ═══════════════════════════════════════════════════════════════════

/// <summary>8-bit integer: 0 to 255 (unsigned) or -128 to 127 (signed).</summary>
public sealed record TinyIntType : PortableType;

/// <summary>16-bit signed integer: -32,768 to 32,767.</summary>
public sealed record SmallIntType : PortableType;

/// <summary>32-bit signed integer: -2,147,483,648 to 2,147,483,647.</summary>
public sealed record IntType : PortableType;

/// <summary>64-bit signed integer: -9.2E18 to 9.2E18.</summary>
public sealed record BigIntType : PortableType;

// ═══════════════════════════════════════════════════════════════════
// EXACT NUMERIC TYPES - Require precision and/or scale
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Exact decimal number with specified precision and scale.
/// Precision = total number of digits (1-38).
/// Scale = digits after decimal point (0 to Precision).
/// Example: DECIMAL(10,2) stores values like 12345678.99
/// </summary>
/// <param name="Precision">Total digits (1-38)</param>
/// <param name="Scale">Decimal places (0 to Precision)</param>
public sealed record DecimalType(int Precision, int Scale) : PortableType;

/// <summary>
/// Currency type with fixed precision for financial calculations.
/// Equivalent to DECIMAL(19,4) - supports values up to ~922 trillion.
/// </summary>
public sealed record MoneyType : PortableType;

/// <summary>
/// Small currency type with reduced precision.
/// Equivalent to DECIMAL(10,4) - supports values up to ~214,748.
/// </summary>
public sealed record SmallMoneyType : PortableType;

// ═══════════════════════════════════════════════════════════════════
// FLOATING POINT TYPES - No parameters (IEEE standard sizes)
// ═══════════════════════════════════════════════════════════════════

/// <summary>32-bit IEEE 754 floating point. ~7 significant digits.</summary>
public sealed record FloatType : PortableType;

/// <summary>64-bit IEEE 754 floating point. ~15 significant digits.</summary>
public sealed record DoubleType : PortableType;

// ═══════════════════════════════════════════════════════════════════
// STRING TYPES - Each variant has exactly the parameters it needs
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Fixed-length ASCII/single-byte character string. Always padded to exact length.
/// </summary>
/// <param name="Length">Exact character count (1-8000)</param>
public sealed record CharType(int Length) : PortableType;

/// <summary>
/// Variable-length ASCII/single-byte character string up to max length.
/// </summary>
/// <param name="MaxLength">Maximum characters (1-8000)</param>
public sealed record VarCharType(int MaxLength) : PortableType;

/// <summary>
/// Fixed-length Unicode character string. Always padded to exact length.
/// Uses 2 bytes per character (UCS-2/UTF-16).
/// </summary>
/// <param name="Length">Exact character count (1-4000)</param>
public sealed record NCharType(int Length) : PortableType;

/// <summary>
/// Variable-length Unicode character string up to max length.
/// Uses 2 bytes per character (UCS-2/UTF-16).
/// </summary>
/// <param name="MaxLength">Maximum characters (1-4000, or int.MaxValue for MAX)</param>
public sealed record NVarCharType(int MaxLength) : PortableType;

/// <summary>
/// Unlimited length text storage. No length parameter needed.
/// Maps to TEXT (Postgres/SQLite), NVARCHAR(MAX) (SQL Server).
/// </summary>
public sealed record TextType : PortableType;

// ═══════════════════════════════════════════════════════════════════
// BINARY TYPES - Length-based variants
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Fixed-length binary data. Always padded to exact length.
/// </summary>
/// <param name="Length">Exact byte count (1-8000)</param>
public sealed record BinaryType(int Length) : PortableType;

/// <summary>
/// Variable-length binary data up to max length.
/// </summary>
/// <param name="MaxLength">Maximum bytes (1-8000, or int.MaxValue for MAX)</param>
public sealed record VarBinaryType(int MaxLength) : PortableType;

/// <summary>
/// Unlimited binary storage. No length parameter needed.
/// Maps to BLOB (SQLite), BYTEA (Postgres), VARBINARY(MAX) (SQL Server).
/// </summary>
public sealed record BlobType : PortableType;

// ═══════════════════════════════════════════════════════════════════
// DATE/TIME TYPES - Some need precision, some don't
// ═══════════════════════════════════════════════════════════════════

/// <summary>Date only (no time component). No parameters needed.</summary>
public sealed record DateType : PortableType;

/// <summary>
/// Time only (no date component) with fractional seconds precision.
/// </summary>
/// <param name="Precision">Fractional seconds digits (0-7, default 7)</param>
public sealed record TimeType(int Precision) : PortableType;

/// <summary>
/// Date and time without timezone.
/// </summary>
/// <param name="Precision">Fractional seconds digits (0-7, default 3)</param>
public sealed record DateTimeType(int Precision) : PortableType;

/// <summary>
/// Date and time with timezone offset. No precision parameter.
/// Always stores full precision with timezone info.
/// </summary>
public sealed record DateTimeOffsetType : PortableType;

/// <summary>
/// Row version / timestamp for optimistic concurrency.
/// Auto-generated binary value that changes on each update.
/// No parameters - platform determines size (8 bytes typically).
/// </summary>
public sealed record RowVersionType : PortableType;

// ═══════════════════════════════════════════════════════════════════
// OTHER TYPES - Specialized types with specific parameters
// ═══════════════════════════════════════════════════════════════════

/// <summary>128-bit globally unique identifier. No parameters needed.</summary>
public sealed record UuidType : PortableType;

/// <summary>Boolean true/false value. No parameters needed.</summary>
public sealed record BooleanType : PortableType;

/// <summary>
/// JSON document storage. No parameters needed.
/// Maps to JSONB (Postgres), TEXT (SQLite), NVARCHAR(MAX) (SQL Server).
/// </summary>
public sealed record JsonType : PortableType;

/// <summary>
/// XML document storage. No parameters needed.
/// Maps to XML (SQL Server/Postgres), TEXT (SQLite).
/// </summary>
public sealed record XmlType : PortableType;

/// <summary>
/// Database enumeration type with named values.
/// Postgres: Creates actual ENUM type.
/// SQL Server/SQLite: Maps to constrained string.
/// </summary>
/// <param name="Name">Enum type name for DDL</param>
/// <param name="Values">Allowed enum values in order</param>
public sealed record EnumType(string Name, IReadOnlyList<string> Values) : PortableType;

/// <summary>
/// Spatial geometry type for GIS data.
/// </summary>
/// <param name="Srid">Spatial Reference ID (e.g., 4326 for WGS84)</param>
public sealed record GeometryType(int? Srid) : PortableType;

/// <summary>
/// Spatial geography type for Earth-surface GIS data.
/// </summary>
/// <param name="Srid">Spatial Reference ID (default 4326 for WGS84)</param>
public sealed record GeographyType(int Srid = 4326) : PortableType;
```

### 5.1.1 Pattern Matching Usage

With discriminated unions, you pattern match to extract the exact parameters each type has:

```csharp
public static string ToSqlServerType(PortableType type) => type switch
{
    // Integer types - no parameters to extract
    TinyIntType => "TINYINT",
    SmallIntType => "SMALLINT",
    IntType => "INT",
    BigIntType => "BIGINT",

    // Decimal - extract precision and scale
    DecimalType(var p, var s) => $"DECIMAL({p},{s})",
    MoneyType => "MONEY",
    SmallMoneyType => "SMALLMONEY",

    // Floating point - no parameters
    FloatType => "REAL",
    DoubleType => "FLOAT",

    // String types - each has exactly the parameter it needs
    CharType(var len) => $"CHAR({len})",
    VarCharType(var max) => $"VARCHAR({max})",
    NCharType(var len) => $"NCHAR({len})",
    NVarCharType(var max) when max == int.MaxValue => "NVARCHAR(MAX)",
    NVarCharType(var max) => $"NVARCHAR({max})",
    TextType => "NVARCHAR(MAX)",

    // Binary types
    BinaryType(var len) => $"BINARY({len})",
    VarBinaryType(var max) when max == int.MaxValue => "VARBINARY(MAX)",
    VarBinaryType(var max) => $"VARBINARY({max})",
    BlobType => "VARBINARY(MAX)",

    // Date/time types - extract precision where applicable
    DateType => "DATE",
    TimeType(var p) => $"TIME({p})",
    DateTimeType(var p) => $"DATETIME2({p})",
    DateTimeOffsetType => "DATETIMEOFFSET",
    RowVersionType => "ROWVERSION",

    // Other types
    UuidType => "UNIQUEIDENTIFIER",
    BooleanType => "BIT",
    JsonType => "NVARCHAR(MAX)",  // JSON support via NVARCHAR
    XmlType => "XML",
    EnumType(var name, _) => $"NVARCHAR(100)",  // CHECK constraint added separately
    GeometryType(_) => "GEOMETRY",
    GeographyType(_) => "GEOGRAPHY",

    _ => throw new NotSupportedException($"Unknown type: {type.GetType().Name}")
};

public static string ToPostgresType(PortableType type) => type switch
{
    TinyIntType => "SMALLINT",  // Postgres has no TINYINT
    SmallIntType => "SMALLINT",
    IntType => "INTEGER",
    BigIntType => "BIGINT",

    DecimalType(var p, var s) => $"NUMERIC({p},{s})",
    MoneyType => "NUMERIC(19,4)",
    SmallMoneyType => "NUMERIC(10,4)",

    FloatType => "REAL",
    DoubleType => "DOUBLE PRECISION",

    CharType(var len) => $"CHAR({len})",
    VarCharType(var max) => $"VARCHAR({max})",
    NCharType(var len) => $"CHAR({len})",  // Postgres is always Unicode
    NVarCharType(var max) => $"VARCHAR({max})",
    TextType => "TEXT",

    BinaryType(_) => "BYTEA",  // Postgres BYTEA is always variable
    VarBinaryType(_) => "BYTEA",
    BlobType => "BYTEA",

    DateType => "DATE",
    TimeType(var p) => $"TIME({p})",
    DateTimeType(_) => "TIMESTAMP",
    DateTimeOffsetType => "TIMESTAMPTZ",
    RowVersionType => "BYTEA",  // Manual implementation needed

    UuidType => "UUID",
    BooleanType => "BOOLEAN",
    JsonType => "JSONB",
    XmlType => "XML",
    EnumType(var name, _) => name,  // Use CREATE TYPE for enum

    GeometryType(var srid) => srid.HasValue ? $"GEOMETRY(Geometry,{srid})" : "GEOMETRY",
    GeographyType(var srid) => $"GEOGRAPHY(Geography,{srid})",

    _ => throw new NotSupportedException($"Unknown type: {type.GetType().Name}")
};

public static string ToSqliteType(PortableType type) => type switch
{
    // SQLite has limited type affinity - INTEGER, REAL, TEXT, BLOB
    TinyIntType or SmallIntType or IntType or BigIntType => "INTEGER",
    DecimalType(_, _) or MoneyType or SmallMoneyType => "REAL",
    FloatType or DoubleType => "REAL",
    CharType(_) or VarCharType(_) or NCharType(_) or NVarCharType(_) or TextType => "TEXT",
    BinaryType(_) or VarBinaryType(_) or BlobType => "BLOB",
    DateType or TimeType(_) or DateTimeType(_) or DateTimeOffsetType => "TEXT",
    RowVersionType => "BLOB",
    UuidType => "TEXT",
    BooleanType => "INTEGER",
    JsonType or XmlType => "TEXT",
    EnumType(_, _) => "TEXT",
    GeometryType(_) or GeographyType(_) => "BLOB",  // Store as WKB
    _ => throw new NotSupportedException($"Unknown type: {type.GetType().Name}")
};
```

### 5.1.2 Factory Methods for Convenience

While types are constructed directly, convenience factory methods can be provided:

```csharp
public static class PortableTypes
{
    // Integer types - direct construction, no factory needed
    public static TinyIntType TinyInt => new();
    public static SmallIntType SmallInt => new();
    public static IntType Int => new();
    public static BigIntType BigInt => new();

    // Decimal requires parameters
    public static DecimalType Decimal(int precision, int scale) => new(precision, scale);
    public static MoneyType Money => new();

    // Floating point
    public static FloatType Float => new();
    public static DoubleType Double => new();

    // Strings - factory method clarifies intent
    public static CharType Char(int length) => new(length);
    public static VarCharType VarChar(int maxLength) => new(maxLength);
    public static NCharType NChar(int length) => new(length);
    public static NVarCharType NVarChar(int maxLength) => new(maxLength);
    public static NVarCharType NVarCharMax => new(int.MaxValue);
    public static TextType Text => new();

    // Binary
    public static BinaryType Binary(int length) => new(length);
    public static VarBinaryType VarBinary(int maxLength) => new(maxLength);
    public static VarBinaryType VarBinaryMax => new(int.MaxValue);
    public static BlobType Blob => new();

    // Date/time - defaults for common cases
    public static DateType Date => new();
    public static TimeType Time(int precision = 7) => new(precision);
    public static DateTimeType DateTime(int precision = 3) => new(precision);
    public static DateTimeOffsetType DateTimeOffset => new();
    public static RowVersionType RowVersion => new();

    // Other
    public static UuidType Uuid => new();
    public static BooleanType Boolean => new();
    public static JsonType Json => new();
    public static XmlType Xml => new();
    public static EnumType Enum(string name, params string[] values) => new(name, values);
}
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

The diff engine produces a list of schema operations:

```csharp
/// <summary>
/// Base type for all schema operations.
/// </summary>
public abstract record SchemaOperation;

// Table operations
public sealed record CreateTable(TableDefinition Table) : SchemaOperation;
public sealed record DropTable(string Schema, string Name) : SchemaOperation;

// Column operations
public sealed record AddColumn(string Schema, string Table, ColumnDefinition Column) : SchemaOperation;
public sealed record DropColumn(string Schema, string Table, string Column) : SchemaOperation;
public sealed record AlterColumn(string Schema, string Table, ColumnDefinition OldColumn, ColumnDefinition NewColumn) : SchemaOperation;

// Index operations
public sealed record CreateIndex(string Schema, string Table, IndexDefinition Index) : SchemaOperation;
public sealed record DropIndex(string Schema, string Table, string IndexName) : SchemaOperation;

// Constraint operations
public sealed record AddPrimaryKey(string Schema, string Table, PrimaryKeyDefinition PrimaryKey) : SchemaOperation;
public sealed record DropPrimaryKey(string Schema, string Table, string? ConstraintName) : SchemaOperation;
public sealed record AddForeignKey(string Schema, string Table, ForeignKeyDefinition ForeignKey) : SchemaOperation;
public sealed record DropForeignKey(string Schema, string Table, string ConstraintName) : SchemaOperation;
```

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

Destructive operations require explicit configuration:

```csharp
var options = new MigrationOptions
{
    AllowDropTable = false,      // Default: false
    AllowDropColumn = false,     // Default: false
    AllowDropIndex = true,       // Default: false (but often safe)
    AllowAlterColumn = false,    // Default: false
};

var result = MigrationRunner.Apply(connection, operations, options, logger);
```

---

## 7. Migration Execution

### 7.1 Migration Runner

The migration runner executes operations with proper transaction handling:

```csharp
/// <summary>
/// Applies schema operations to a database.
/// </summary>
public static class MigrationRunner
{
    /// <summary>
    /// Applies schema operations to the database.
    /// </summary>
    /// <param name="connection">Database connection.</param>
    /// <param name="operations">Operations to apply.</param>
    /// <param name="options">Migration options.</param>
    /// <param name="logger">Logger for migration progress.</param>
    /// <returns>Success or migration error.</returns>
    public static MigrationResult Apply(
        IDbConnection connection,
        IReadOnlyList<SchemaOperation> operations,
        MigrationOptions options,
        ILogger logger
    );

    /// <summary>
    /// Generates DDL without executing.
    /// </summary>
    public static Result<string, MigrationError> GenerateDdl(
        IReadOnlyList<SchemaOperation> operations,
        DatabasePlatform platform
    );
}
```

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

The diff engine compares desired schema against current database state:

```csharp
/// <summary>
/// Compares two schemas and produces operations to transform source into target.
/// </summary>
public static class SchemaDiff
{
    /// <summary>
    /// Calculates operations needed to transform current schema to desired schema.
    /// </summary>
    /// <param name="current">Current database schema (from introspection).</param>
    /// <param name="desired">Desired schema (from definition).</param>
    /// <returns>List of operations to apply.</returns>
    public static IReadOnlyList<SchemaOperation> Calculate(
        SchemaDefinition current,
        SchemaDefinition desired
    );
}
```

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

Each provider implements schema introspection:

```csharp
/// <summary>
/// Reads current database schema.
/// </summary>
public static class SchemaInspector
{
    /// <summary>
    /// Reads complete schema from database.
    /// </summary>
    /// <param name="connection">Database connection.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>Current schema or error.</returns>
    public static Result<SchemaDefinition, MigrationError> Inspect(
        IDbConnection connection,
        ILogger logger
    );
}
```

### 8.5 Schema Capture to Metadata

**CRITICAL REQUIREMENT**: The migration framework MUST support capturing an existing database schema and persisting it to metadata. This enables:

1. **Brownfield Adoption**: Capture schema from existing production databases
2. **Schema Versioning**: Store captured schema as JSON for version control
3. **Cross-Platform Migration**: Capture from one platform, apply to another
4. **Audit Trail**: Record point-in-time schema snapshots

#### 8.5.1 Schema Capture API

```csharp
/// <summary>
/// Captures database schema and serializes to JSON metadata.
/// </summary>
public static class SchemaSerializer
{
    /// <summary>
    /// Serialize schema definition to JSON for storage/versioning.
    /// </summary>
    public static string ToJson(SchemaDefinition schema);

    /// <summary>
    /// Deserialize schema from JSON metadata.
    /// </summary>
    public static SchemaDefinition FromJson(string json);
}
```

#### 8.5.2 Capture Workflow

```csharp
// 1. Connect to existing database
using var connection = new SqliteConnection("Data Source=legacy.db");
connection.Open();

// 2. CAPTURE existing schema
var captureResult = SchemaInspector.Inspect(connection, logger);
if (captureResult is SchemaResult.Error error)
{
    logger.LogError("Capture failed: {Error}", error.Value.Message);
    return;
}
var schema = captureResult.Value;

// 3. Serialize to metadata (for version control)
var json = SchemaSerializer.ToJson(schema);
File.WriteAllText("schema-v1.json", json);

// 4. Later: Load from metadata and apply to new database
var savedSchema = SchemaSerializer.FromJson(File.ReadAllText("schema-v1.json"));
var operations = SchemaDiff.Calculate(emptySchema, savedSchema);
MigrationRunner.Apply(newConnection, operations, MigrationOptions.Default, logger);
```

#### 8.5.3 Captured Schema Contents

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

Each database platform implements DDL generation:

```csharp
/// <summary>
/// Generates platform-specific DDL.
/// </summary>
public static class DdlGenerator
{
    /// <summary>
    /// Generates DDL for a schema operation.
    /// </summary>
    /// <param name="operation">Schema operation.</param>
    /// <param name="platform">Target platform.</param>
    /// <returns>DDL SQL string.</returns>
    public static string Generate(SchemaOperation operation, DatabasePlatform platform);
}

public enum DatabasePlatform
{
    SQLite,
    PostgreSQL,
    SqlServer
}
```

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

```csharp
/// <summary>
/// Base type for migration errors.
/// </summary>
public abstract record MigrationError(string Message);

/// <summary>
/// Error during schema introspection.
/// </summary>
public sealed record IntrospectionError(string Message, Exception? Inner = null) : MigrationError(Message);

/// <summary>
/// Error during DDL generation.
/// </summary>
public sealed record DdlGenerationError(string Message, SchemaOperation Operation) : MigrationError(Message);

/// <summary>
/// Error during DDL execution.
/// </summary>
public sealed record ExecutionError(string Message, string Sql, Exception? Inner = null) : MigrationError(Message);

/// <summary>
/// Validation error (e.g., destructive operation not allowed).
/// </summary>
public sealed record ValidationError(string Message, SchemaOperation Operation) : MigrationError(Message);
```

### 10.2 Result Types

All operations return Result types (per CLAUDE.md):

```csharp
// Type aliases for common results
using MigrationResult = Result<MigrationSummary, MigrationError>;
using InspectionResult = Result<SchemaDefinition, MigrationError>;
using DdlResult = Result<string, MigrationError>;
```

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

Tests that spin up a fresh database and apply full schema:

```csharp
[Fact]
public void CreateDatabaseFromScratch_SQLite()
{
    // Arrange
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();

    var schema = Schema.Define("Test")
        .Table("Users", t => t
            .Column("Id", PortableType.Uuid, c => c.PrimaryKey())
            .Column("Email", PortableType.String(255), c => c.NotNull())
            .Index("idx_email", "Email", unique: true)
        )
        .Table("Orders", t => t
            .Column("Id", PortableType.Int64, c => c.PrimaryKey().Identity())
            .Column("UserId", PortableType.Uuid, c => c.NotNull())
            .Column("Total", PortableType.Decimal(10, 2))
            .ForeignKey("UserId", "Users", "Id")
        )
        .Build();

    // Act
    var emptySchema = SchemaInspector.Inspect(connection, logger);
    var operations = SchemaDiff.Calculate(emptySchema.Value, schema);
    var result = MigrationRunner.Apply(connection, operations, MigrationOptions.Default, logger);

    // Assert
    Assert.True(result is MigrationResult.Ok);

    // Verify tables exist
    var inspected = SchemaInspector.Inspect(connection, logger);
    Assert.Equal(2, inspected.Value.Tables.Count);
    Assert.Contains(inspected.Value.Tables, t => t.Name == "Users");
    Assert.Contains(inspected.Value.Tables, t => t.Name == "Orders");
}
```

### 12.3 Upgrade Tests

Tests that add to an existing database:

```csharp
[Fact]
public void UpgradeExistingDatabase_AddColumn()
{
    // Arrange - create initial schema
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();

    var v1 = Schema.Define("Test")
        .Table("Users", t => t
            .Column("Id", PortableType.Uuid, c => c.PrimaryKey())
            .Column("Email", PortableType.String(255))
        )
        .Build();

    // Apply v1
    var ops1 = SchemaDiff.Calculate(new SchemaDefinition(), v1);
    _ = MigrationRunner.Apply(connection, ops1, MigrationOptions.Default, logger);

    // Act - upgrade to v2 with new column
    var v2 = Schema.Define("Test")
        .Table("Users", t => t
            .Column("Id", PortableType.Uuid, c => c.PrimaryKey())
            .Column("Email", PortableType.String(255))
            .Column("Name", PortableType.String(100))  // NEW
            .Column("CreatedAt", PortableType.DateTime) // NEW
        )
        .Build();

    var current = SchemaInspector.Inspect(connection, logger).Value;
    var ops2 = SchemaDiff.Calculate(current, v2);
    var result = MigrationRunner.Apply(connection, ops2, MigrationOptions.Default, logger);

    // Assert
    Assert.True(result is MigrationResult.Ok);
    Assert.Equal(2, ops2.Count); // Two AddColumn operations

    var final = SchemaInspector.Inspect(connection, logger).Value;
    var users = final.Tables.Single(t => t.Name == "Users");
    Assert.Equal(4, users.Columns.Count);
}
```

### 12.4 PostgreSQL Tests with Testcontainers

Real PostgreSQL testing using Docker:

```csharp
public class PostgresMigrationTests : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;
    private NpgsqlConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _postgres.StartAsync();

        _connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public void CreateSchema_PostgreSQL_NativeTypes()
    {
        // Arrange
        var schema = Schema.Define("Test")
            .Table("Events", t => t
                .Column("Id", PortableType.Uuid, c => c.PrimaryKey())
                .Column("Data", PortableType.Json, c => c.NotNull())
                .Column("OccurredAt", PortableType.DateTimeOffset)
            )
            .Build();

        // Act
        var operations = SchemaDiff.Calculate(new SchemaDefinition(), schema);
        var result = MigrationRunner.Apply(_connection, operations, MigrationOptions.Default, logger);

        // Assert
        Assert.True(result is MigrationResult.Ok);

        // Verify PostgreSQL-specific types
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_name = 'events'
            """;
        using var reader = cmd.ExecuteReader();

        var columns = new Dictionary<string, string>();
        while (reader.Read())
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }

        Assert.Equal("uuid", columns["id"]);
        Assert.Equal("jsonb", columns["data"]);
        Assert.Contains("timestamp", columns["occurredat"]);
    }
}
```

### 12.5 Idempotency Tests

Verify migrations can run multiple times safely:

```csharp
[Fact]
public void Migration_IsIdempotent_NoErrorOnRerun()
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();

    var schema = Schema.Define("Test")
        .Table("Items", t => t
            .Column("Id", PortableType.Int32, c => c.PrimaryKey())
            .Column("Name", PortableType.String(50))
            .Index("idx_name", "Name")
        )
        .Build();

    // Run migration twice
    for (int i = 0; i < 2; i++)
    {
        var current = SchemaInspector.Inspect(connection, logger).Value;
        var operations = SchemaDiff.Calculate(current, schema);
        var result = MigrationRunner.Apply(connection, operations, MigrationOptions.Default, logger);

        Assert.True(result is MigrationResult.Ok);

        // Second run should have 0 operations (already up to date)
        if (i == 1)
        {
            Assert.Empty(operations);
        }
    }
}
```

### 12.6 Cross-Platform Test Matrix

Each test should run against all platforms:

```csharp
public class CrossPlatformMigrationTests
{
    public static IEnumerable<object[]> Platforms =>
    [
        [DatabasePlatform.SQLite, () => CreateSqliteConnection()],
        [DatabasePlatform.PostgreSQL, () => CreatePostgresConnection()],
        [DatabasePlatform.SqlServer, () => CreateSqlServerConnection()],
    ];

    [Theory]
    [MemberData(nameof(Platforms))]
    public void SameSchema_WorksOnAllPlatforms(DatabasePlatform platform, Func<IDbConnection> factory)
    {
        using var connection = factory();

        var schema = Schema.Define("Test")
            .Table("Products", t => t
                .Column("Id", PortableType.Uuid, c => c.PrimaryKey())
                .Column("Name", PortableType.String(200), c => c.NotNull())
                .Column("Price", PortableType.Decimal(10, 2))
                .Column("Active", PortableType.Boolean)
                .Index("idx_name", "Name")
            )
            .Build();

        var operations = SchemaDiff.Calculate(new SchemaDefinition(), schema);
        var result = MigrationRunner.Apply(connection, operations, MigrationOptions.Default, logger);

        Assert.True(result is MigrationResult.Ok);

        var inspected = SchemaInspector.Inspect(connection, logger).Value;
        Assert.Single(inspected.Tables);
        Assert.Equal(4, inspected.Tables[0].Columns.Count);
    }
}
```

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
| **Schema serialize to JSON metadata** | Required | Required | Required |
| **Destructive op returns useful error** | Required | Required | Required |

---

## 13. Schema Capture and Metadata

A critical feature of the Migration framework is the ability to **capture existing database schemas** and serialize them to JSON metadata. This enables:

1. **Brownfield scenarios** - Capture existing database schema before applying migrations
2. **Schema versioning** - Store schema snapshots in source control
3. **Documentation** - Generate schema documentation from metadata
4. **Validation** - Compare captured schema against expected schema
5. **CI/CD** - Verify schema matches expected state in deployment pipelines

### 13.1 Schema Serializer

```csharp
/// <summary>
/// Serializes and deserializes schema definitions to/from JSON.
/// Used for capturing existing database schemas and storing as metadata.
/// </summary>
public static class SchemaSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new PortableTypeJsonConverter() }
    };

    /// <summary>
    /// Serialize a schema definition to JSON string.
    /// </summary>
    public static string ToJson(SchemaDefinition schema) =>
        JsonSerializer.Serialize(schema, Options);

    /// <summary>
    /// Deserialize a schema definition from JSON string.
    /// </summary>
    public static SchemaDefinition FromJson(string json) =>
        JsonSerializer.Deserialize<SchemaDefinition>(json, Options)
        ?? throw new JsonException("Failed to deserialize schema");
}
```

### 13.2 JSON Schema Format

The JSON format uses camelCase property names and preserves all schema details:

```json
{
  "name": "MyDatabase",
  "tables": [
    {
      "schema": "public",
      "name": "Users",
      "columns": [
        {
          "name": "Id",
          "type": { "type": "Uuid" },
          "isNullable": false
        },
        {
          "name": "Email",
          "type": { "type": "VarChar", "length": 255 },
          "isNullable": false
        },
        {
          "name": "Balance",
          "type": { "type": "Decimal", "precision": 18, "scale": 2 },
          "isNullable": true
        }
      ],
      "primaryKey": {
        "name": "PK_Users",
        "columns": ["Id"]
      },
      "indexes": [
        {
          "name": "idx_users_email",
          "columns": ["Email"],
          "isUnique": true
        }
      ],
      "foreignKeys": [],
      "uniqueConstraints": [],
      "checkConstraints": []
    }
  ]
}
```

### 13.3 PortableType JSON Converter

The `PortableTypeJsonConverter` handles the discriminated union serialization:

```csharp
/// <summary>
/// JSON converter for PortableType discriminated union.
/// </summary>
public sealed class PortableTypeJsonConverter : JsonConverter<PortableType>
{
    public override PortableType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var typeName = root.GetProperty("type").GetString();

        return typeName switch
        {
            "Int" => new IntType(),
            "BigInt" => new BigIntType(),
            "VarChar" => new VarCharType(root.GetProperty("length").GetInt32()),
            "Decimal" => new DecimalType(
                root.GetProperty("precision").GetInt32(),
                root.GetProperty("scale").GetInt32()
            ),
            // ... other types
            _ => new TextType()
        };
    }

    public override void Write(Utf8JsonWriter writer, PortableType value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case IntType:
                writer.WriteString("type", "Int");
                break;
            case VarCharType v:
                writer.WriteString("type", "VarChar");
                writer.WriteNumber("length", v.MaxLength);
                break;
            case DecimalType d:
                writer.WriteString("type", "Decimal");
                writer.WriteNumber("precision", d.Precision);
                writer.WriteNumber("scale", d.Scale);
                break;
            // ... other types
        }
        writer.WriteEndObject();
    }
}
```

### 13.4 Schema Capture Workflow

Typical workflow for capturing and using schema metadata:

```csharp
// 1. Connect to existing database
using var connection = new SqliteConnection("Data Source=existing.db");
connection.Open();

// 2. Capture current schema
var inspectResult = SqliteSchemaInspector.Inspect(connection);
if (inspectResult is InspectionResult.Error err)
{
    logger.LogError("Failed to inspect schema: {Error}", err.Value.Message);
    return;
}
var currentSchema = inspectResult.Value;

// 3. Serialize to JSON for storage
var json = SchemaSerializer.ToJson(currentSchema);
File.WriteAllText("schema-snapshot.json", json);

// 4. Later, load and compare
var storedJson = File.ReadAllText("schema-snapshot.json");
var storedSchema = SchemaSerializer.FromJson(storedJson);

// 5. Compare with desired schema
var desiredSchema = Schema.Define("MyDb")
    .Table("Users", t => t
        .Column("Id", new UuidType(), c => c.PrimaryKey())
        .Column("Email", new VarCharType(255), c => c.NotNull())
    )
    .Build();

var operations = SchemaDiff.Calculate(storedSchema, desiredSchema);
```

### 13.5 Required Schema Capture Tests

```csharp
[Fact]
public void SchemaCapture_ExistingDatabase_ReturnsCompleteSchema()
{
    // Arrange - create database with raw SQL (simulating existing DB)
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    using var cmd = connection.CreateCommand();
    cmd.CommandText = """
        CREATE TABLE Products (
            Id INTEGER PRIMARY KEY,
            Name TEXT NOT NULL,
            Price REAL
        );
        CREATE INDEX idx_products_name ON Products(Name);
        """;
    cmd.ExecuteNonQuery();

    // Act - capture schema
    var result = SqliteSchemaInspector.Inspect(connection);

    // Assert
    Assert.True(result is InspectionResult.Ok);
    var schema = result.Value;
    Assert.Single(schema.Tables);
    var table = schema.Tables[0];
    Assert.Equal("Products", table.Name);
    Assert.Equal(3, table.Columns.Count);
    Assert.Single(table.Indexes);
}

[Fact]
public void SchemaCapture_SerializesToJson_RoundTrip()
{
    // Arrange
    var schema = Schema.Define("Test")
        .Table("Users", t => t
            .Column("Id", new UuidType(), c => c.PrimaryKey())
            .Column("Email", new VarCharType(255), c => c.NotNull())
            .Column("Balance", new DecimalType(18, 2))
            .Index("idx_email", "Email", unique: true)
        )
        .Build();

    // Act
    var json = SchemaSerializer.ToJson(schema);
    var roundTripped = SchemaSerializer.FromJson(json);

    // Assert
    Assert.Equal(schema.Name, roundTripped.Name);
    Assert.Equal(schema.Tables.Count, roundTripped.Tables.Count);
    var table = roundTripped.Tables[0];
    Assert.Equal("Users", table.Name);
    Assert.Equal(3, table.Columns.Count);
    Assert.Single(table.Indexes);
}
```

---

## 14. Appendices

### Appendix A: Sync Framework Schema

The Sync framework uses Migration to create its infrastructure tables:

```csharp
var syncSchema = Schema.Define("_sync")
    .Table("_sync_state", t => t
        .Column("key", PortableType.String(255), c => c.PrimaryKey())
        .Column("value", PortableType.Text, c => c.NotNull())
    )
    .Table("_sync_session", t => t
        .Column("sync_active", PortableType.Int32, c => c.NotNull().Default("0"))
    )
    .Table("_sync_log", t => t
        .Column("version", PortableType.Int64, c => c.PrimaryKey().Identity())
        .Column("table_name", PortableType.String(255), c => c.NotNull())
        .Column("pk_value", PortableType.Text, c => c.NotNull())
        .Column("operation", PortableType.String(10), c => c.NotNull())
        .Column("payload", PortableType.Text)
        .Column("origin", PortableType.String(36), c => c.NotNull())
        .Column("timestamp", PortableType.String(30), c => c.NotNull())
        .Index("idx_sync_log_version", "version")
        .Index("idx_sync_log_table", "table_name", "version")
    )
    .Table("_sync_clients", t => t
        .Column("origin_id", PortableType.String(36), c => c.PrimaryKey())
        .Column("last_sync_version", PortableType.Int64, c => c.NotNull().Default("0"))
        .Column("last_sync_timestamp", PortableType.String(30), c => c.NotNull())
        .Column("created_at", PortableType.String(30), c => c.NotNull())
        .Index("idx_sync_clients_version", "last_sync_version")
    )
    .Table("_sync_subscriptions", t => t
        .Column("subscription_id", PortableType.String(36), c => c.PrimaryKey())
        .Column("origin_id", PortableType.String(36), c => c.NotNull())
        .Column("subscription_type", PortableType.String(10), c => c.NotNull())
        .Column("table_name", PortableType.String(255), c => c.NotNull())
        .Column("filter", PortableType.Text)
        .Column("created_at", PortableType.String(30), c => c.NotNull())
        .Column("expires_at", PortableType.String(30))
        .Index("idx_subscriptions_table", "table_name")
        .Index("idx_subscriptions_origin", "origin_id")
    )
    .Build();
```

### Appendix B: Example Usage

```csharp
// Define desired schema
var schema = Schema.Define("MyApp")
    .Table("Users", t => t
        .Column("Id", PortableType.Uuid, c => c.PrimaryKey())
        .Column("Email", PortableType.String(255), c => c.NotNull())
        .Column("Name", PortableType.String(100))
        .Column("CreatedAt", PortableType.DateTime, c => c.NotNull())
        .Index("idx_users_email", "Email", unique: true)
    )
    .Build();

// Inspect current database
using var connection = new SqliteConnection("Data Source=app.db");
connection.Open();

var currentResult = SchemaInspector.Inspect(connection, logger);
if (currentResult is InspectionError error)
{
    logger.LogError("Failed to inspect: {Error}", error.Message);
    return;
}

var current = ((InspectionResult.Ok)currentResult).Value;

// Calculate diff
var operations = SchemaDiff.Calculate(current, schema);

// Apply migrations (additive only by default)
var result = MigrationRunner.Apply(
    connection,
    operations,
    MigrationOptions.Default,
    logger
);

if (result is MigrationResult.Ok ok)
{
    logger.LogInformation("Applied {Count} operations", ok.Value.OperationsApplied);
}
```

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
