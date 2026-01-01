using System.Globalization;

namespace Migration.Postgres;

/// <summary>
/// Result of a schema migration operation.
/// </summary>
/// <param name="Success">Whether the migration completed without errors.</param>
/// <param name="TablesCreated">Number of tables successfully created or already existing.</param>
/// <param name="Errors">List of table names and error messages for any failures.</param>
public sealed record MigrationResult(bool Success, int TablesCreated, IReadOnlyList<string> Errors);

/// <summary>
/// PostgreSQL DDL generator for schema operations.
/// </summary>
public static class PostgresDdlGenerator
{
    /// <summary>
    /// Migrate a schema definition to PostgreSQL, creating all tables.
    /// Each table is created independently - failures on one table don't block others.
    /// Uses CREATE TABLE IF NOT EXISTS for idempotency.
    /// </summary>
    /// <param name="connection">Open database connection.</param>
    /// <param name="schema">Schema definition to migrate.</param>
    /// <param name="onTableCreated">Optional callback for each table created (table name).</param>
    /// <param name="onTableFailed">Optional callback for each table that failed (table name, exception).</param>
    /// <returns>Migration result with success status and any errors.</returns>
    public static MigrationResult MigrateSchema(
        IDbConnection connection,
        SchemaDefinition schema,
        Action<string>? onTableCreated = null,
        Action<string, Exception>? onTableFailed = null
    )
    {
        var errors = new List<string>();
        var tablesCreated = 0;

        foreach (var table in schema.Tables)
        {
            try
            {
                var ddl = Generate(new CreateTableOperation(table));
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                cmd.ExecuteNonQuery();
                tablesCreated++;
                onTableCreated?.Invoke(table.Name);
            }
            catch (Exception ex)
            {
                errors.Add($"{table.Name}: {ex.Message}");
                onTableFailed?.Invoke(table.Name, ex);
            }
        }

        return new MigrationResult(
            Success: errors.Count == 0,
            TablesCreated: tablesCreated,
            Errors: errors.AsReadOnly()
        );
    }

    /// <summary>
    /// Generate PostgreSQL DDL for a schema operation.
    /// </summary>
    public static string Generate(SchemaOperation operation) =>
        operation switch
        {
            CreateTableOperation op => GenerateCreateTable(op.Table),
            AddColumnOperation op => GenerateAddColumn(op),
            CreateIndexOperation op => GenerateCreateIndex(op),
            AddForeignKeyOperation op => GenerateAddForeignKey(op),
            AddCheckConstraintOperation op => GenerateAddCheckConstraint(op),
            AddUniqueConstraintOperation op => GenerateAddUniqueConstraint(op),
            DropTableOperation op =>
                $"DROP TABLE IF EXISTS \"{op.Schema}\".\"{op.TableName}\" CASCADE",
            DropColumnOperation op =>
                $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" DROP COLUMN \"{op.ColumnName}\"",
            DropIndexOperation op => $"DROP INDEX IF EXISTS \"{op.Schema}\".\"{op.IndexName}\"",
            DropForeignKeyOperation op =>
                $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" DROP CONSTRAINT \"{op.ConstraintName}\"",
            _ => throw new NotSupportedException(
                $"Unknown operation type: {operation.GetType().Name}"
            ),
        };

    private static string GenerateCreateTable(TableDefinition table)
    {
        var sb = new StringBuilder();
        sb.Append(
            CultureInfo.InvariantCulture,
            $"CREATE TABLE IF NOT EXISTS \"{table.Schema}\".\"{table.Name}\" ("
        );

        var columnDefs = new List<string>();

        foreach (var column in table.Columns)
        {
            columnDefs.Add(GenerateColumnDef(column));
        }

        // Add primary key constraint
        if (table.PrimaryKey is not null && table.PrimaryKey.Columns.Count > 0)
        {
            var pkName = table.PrimaryKey.Name ?? $"PK_{table.Name}";
            var pkCols = string.Join(", ", table.PrimaryKey.Columns.Select(c => $"\"{c}\""));
            columnDefs.Add($"CONSTRAINT \"{pkName}\" PRIMARY KEY ({pkCols})");
        }

        // Add foreign key constraints
        foreach (var fk in table.ForeignKeys)
        {
            var fkName = fk.Name ?? $"FK_{table.Name}_{string.Join("_", fk.Columns)}";
            var fkCols = string.Join(", ", fk.Columns.Select(c => $"\"{c}\""));
            var refCols = string.Join(", ", fk.ReferencedColumns.Select(c => $"\"{c}\""));
            var onDelete = ForeignKeyActionToSql(fk.OnDelete);
            var onUpdate = ForeignKeyActionToSql(fk.OnUpdate);

            columnDefs.Add(
                $"CONSTRAINT \"{fkName}\" FOREIGN KEY ({fkCols}) REFERENCES \"{fk.ReferencedSchema}\".\"{fk.ReferencedTable}\" ({refCols}) ON DELETE {onDelete} ON UPDATE {onUpdate}"
            );
        }

        // Add unique constraints
        foreach (var uc in table.UniqueConstraints)
        {
            var ucName = uc.Name ?? $"UQ_{table.Name}_{string.Join("_", uc.Columns)}";
            var ucCols = string.Join(", ", uc.Columns.Select(c => $"\"{c}\""));
            columnDefs.Add($"CONSTRAINT \"{ucName}\" UNIQUE ({ucCols})");
        }

        // Add check constraints
        foreach (var cc in table.CheckConstraints)
        {
            columnDefs.Add($"CONSTRAINT \"{cc.Name}\" CHECK ({cc.Expression})");
        }

        sb.Append(string.Join(", ", columnDefs));
        sb.Append(')');

        // Generate CREATE INDEX statements for any indexes
        foreach (var index in table.Indexes)
        {
            sb.AppendLine(";");
            var unique = index.IsUnique ? "UNIQUE " : "";
            // Expression indexes use Expressions verbatim, column indexes quote column names
            var indexItems =
                index.Expressions.Count > 0
                    ? string.Join(", ", index.Expressions)
                    : string.Join(", ", index.Columns.Select(c => $"\"{c}\""));
            var filter = index.Filter is not null ? $" WHERE {index.Filter}" : "";
            sb.Append(
                CultureInfo.InvariantCulture,
                $"CREATE {unique}INDEX IF NOT EXISTS \"{index.Name}\" ON \"{table.Schema}\".\"{table.Name}\" ({indexItems}){filter}"
            );
        }

        return sb.ToString();
    }

    private static string GenerateColumnDef(ColumnDefinition column)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"\"{column.Name}\" ");

        // Handle identity columns
        if (column.IsIdentity)
        {
            sb.Append(
                column.Type switch
                {
                    SmallIntType => "SMALLSERIAL",
                    IntType => "SERIAL",
                    BigIntType => "BIGSERIAL",
                    _ => "SERIAL",
                }
            );
        }
        else
        {
            sb.Append(PortableTypeToPostgres(column.Type));
        }

        if (!column.IsNullable && !column.IsIdentity)
        {
            sb.Append(" NOT NULL");
        }

        // LQL expression takes precedence over raw SQL default
        if (column.DefaultLqlExpression is not null)
        {
            var translated = LqlDefaultTranslator.ToPostgres(column.DefaultLqlExpression);
            sb.Append(CultureInfo.InvariantCulture, $" DEFAULT {translated}");
        }
        else if (column.DefaultValue is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $" DEFAULT {column.DefaultValue}");
        }

        if (column.CheckConstraint is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $" CHECK ({column.CheckConstraint})");
        }

        return sb.ToString();
    }

    private static string GenerateAddColumn(AddColumnOperation op)
    {
        var colDef = GenerateColumnDef(op.Column);
        return $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" ADD COLUMN {colDef}";
    }

    private static string GenerateCreateIndex(CreateIndexOperation op)
    {
        var unique = op.Index.IsUnique ? "UNIQUE " : "";
        // Expression indexes use Expressions verbatim, column indexes quote column names
        var indexItems =
            op.Index.Expressions.Count > 0
                ? string.Join(", ", op.Index.Expressions)
                : string.Join(", ", op.Index.Columns.Select(c => $"\"{c}\""));
        var filter = op.Index.Filter is not null ? $" WHERE {op.Index.Filter}" : "";

        return $"CREATE {unique}INDEX IF NOT EXISTS \"{op.Index.Name}\" ON \"{op.Schema}\".\"{op.TableName}\" ({indexItems}){filter}";
    }

    private static string GenerateAddForeignKey(AddForeignKeyOperation op)
    {
        var fk = op.ForeignKey;
        var fkName = fk.Name ?? $"FK_{op.TableName}_{string.Join("_", fk.Columns)}";
        var fkCols = string.Join(", ", fk.Columns.Select(c => $"\"{c}\""));
        var refCols = string.Join(", ", fk.ReferencedColumns.Select(c => $"\"{c}\""));
        var onDelete = ForeignKeyActionToSql(fk.OnDelete);
        var onUpdate = ForeignKeyActionToSql(fk.OnUpdate);

        return $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" ADD CONSTRAINT \"{fkName}\" FOREIGN KEY ({fkCols}) REFERENCES \"{fk.ReferencedSchema}\".\"{fk.ReferencedTable}\" ({refCols}) ON DELETE {onDelete} ON UPDATE {onUpdate}";
    }

    private static string GenerateAddCheckConstraint(AddCheckConstraintOperation op) =>
        $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" ADD CONSTRAINT \"{op.CheckConstraint.Name}\" CHECK ({op.CheckConstraint.Expression})";

    private static string GenerateAddUniqueConstraint(AddUniqueConstraintOperation op)
    {
        var uc = op.UniqueConstraint;
        var ucName = uc.Name ?? $"UQ_{op.TableName}_{string.Join("_", uc.Columns)}";
        var ucCols = string.Join(", ", uc.Columns.Select(c => $"\"{c}\""));
        return $"ALTER TABLE \"{op.Schema}\".\"{op.TableName}\" ADD CONSTRAINT \"{ucName}\" UNIQUE ({ucCols})";
    }

    /// <summary>
    /// Map portable type to PostgreSQL type.
    /// </summary>
    public static string PortableTypeToPostgres(PortableType type) =>
        type switch
        {
            // Integer types
            TinyIntType => "SMALLINT",
            SmallIntType => "SMALLINT",
            IntType => "INTEGER",
            BigIntType => "BIGINT",
            BooleanType => "BOOLEAN",

            // Decimal types
            DecimalType(var p, var s) => $"NUMERIC({p},{s})",
            MoneyType => "NUMERIC(19,4)",
            SmallMoneyType => "NUMERIC(10,4)",
            FloatType => "REAL",
            DoubleType => "DOUBLE PRECISION",

            // String types
            CharType(var len) => $"CHAR({len})",
            VarCharType(var max) => $"VARCHAR({max})",
            NCharType(var len) => $"CHAR({len})",
            NVarCharType(var max) when max == int.MaxValue => "TEXT",
            NVarCharType(var max) => $"VARCHAR({max})",
            TextType => "TEXT",
            JsonType => "JSONB",
            XmlType => "XML",
            EnumType(var name, _) => name,

            // Binary types
            BinaryType(_) => "BYTEA",
            VarBinaryType(_) => "BYTEA",
            BlobType => "BYTEA",
            RowVersionType => "BYTEA",

            // Date/time types
            DateType => "DATE",
            TimeType(var p) => $"TIME({p})",
            DateTimeType(_) => "TIMESTAMP",
            DateTimeOffsetType => "TIMESTAMPTZ",

            // Other types
            UuidType => "UUID",
            GeometryType(var srid) => srid.HasValue ? $"GEOMETRY(Geometry,{srid})" : "GEOMETRY",
            GeographyType(var srid) => $"GEOGRAPHY(Geography,{srid})",

            _ => "TEXT",
        };

    private static string ForeignKeyActionToSql(ForeignKeyAction action) =>
        action switch
        {
            ForeignKeyAction.NoAction => "NO ACTION",
            ForeignKeyAction.Cascade => "CASCADE",
            ForeignKeyAction.SetNull => "SET NULL",
            ForeignKeyAction.SetDefault => "SET DEFAULT",
            ForeignKeyAction.Restrict => "RESTRICT",
            _ => "NO ACTION",
        };
}
