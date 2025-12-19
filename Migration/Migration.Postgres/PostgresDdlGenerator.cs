using System.Globalization;

namespace Migration.Postgres;

/// <summary>
/// PostgreSQL DDL generator for schema operations.
/// </summary>
public static class PostgresDdlGenerator
{
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
            var cols = string.Join(", ", index.Columns.Select(c => $"\"{c}\""));
            var filter = index.Filter is not null ? $" WHERE {index.Filter}" : "";
            sb.Append(
                CultureInfo.InvariantCulture,
                $"CREATE {unique}INDEX IF NOT EXISTS \"{index.Name}\" ON \"{table.Schema}\".\"{table.Name}\" ({cols}){filter}"
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

        if (column.DefaultValue is not null)
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
        var cols = string.Join(", ", op.Index.Columns.Select(c => $"\"{c}\""));
        var filter = op.Index.Filter is not null ? $" WHERE {op.Index.Filter}" : "";

        return $"CREATE {unique}INDEX IF NOT EXISTS \"{op.Index.Name}\" ON \"{op.Schema}\".\"{op.TableName}\" ({cols}){filter}";
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
