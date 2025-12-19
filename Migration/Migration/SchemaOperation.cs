namespace Migration;

/// <summary>
/// Base type for schema migration operations.
/// Pattern match to get specific operation details.
/// </summary>
public abstract record SchemaOperation;

/// <summary>
/// Create a new table.
/// </summary>
public sealed record CreateTableOperation(TableDefinition Table) : SchemaOperation;

/// <summary>
/// Add a column to an existing table.
/// </summary>
public sealed record AddColumnOperation(string Schema, string TableName, ColumnDefinition Column)
    : SchemaOperation;

/// <summary>
/// Create an index on a table.
/// </summary>
public sealed record CreateIndexOperation(string Schema, string TableName, IndexDefinition Index)
    : SchemaOperation;

/// <summary>
/// Add a foreign key constraint.
/// </summary>
public sealed record AddForeignKeyOperation(
    string Schema,
    string TableName,
    ForeignKeyDefinition ForeignKey
) : SchemaOperation;

/// <summary>
/// Add a check constraint.
/// </summary>
public sealed record AddCheckConstraintOperation(
    string Schema,
    string TableName,
    CheckConstraintDefinition CheckConstraint
) : SchemaOperation;

/// <summary>
/// Add a unique constraint.
/// </summary>
public sealed record AddUniqueConstraintOperation(
    string Schema,
    string TableName,
    UniqueConstraintDefinition UniqueConstraint
) : SchemaOperation;

// ═══════════════════════════════════════════════════════════════════
// DESTRUCTIVE OPERATIONS - Require explicit opt-in
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Drop a table. DESTRUCTIVE - requires explicit opt-in.
/// </summary>
public sealed record DropTableOperation(string Schema, string TableName) : SchemaOperation;

/// <summary>
/// Drop a column. DESTRUCTIVE - requires explicit opt-in.
/// </summary>
public sealed record DropColumnOperation(string Schema, string TableName, string ColumnName)
    : SchemaOperation;

/// <summary>
/// Drop an index.
/// </summary>
public sealed record DropIndexOperation(string Schema, string TableName, string IndexName)
    : SchemaOperation;

/// <summary>
/// Drop a foreign key constraint.
/// </summary>
public sealed record DropForeignKeyOperation(string Schema, string TableName, string ConstraintName)
    : SchemaOperation;
