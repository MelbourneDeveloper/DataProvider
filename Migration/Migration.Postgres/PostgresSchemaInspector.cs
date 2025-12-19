namespace Migration.Postgres;

/// <summary>
/// Inspects PostgreSQL database schema and returns a SchemaDefinition.
/// </summary>
public static class PostgresSchemaInspector
{
    /// <summary>
    /// Inspect all tables in a PostgreSQL database.
    /// </summary>
    /// <param name="connection">Open PostgreSQL connection</param>
    /// <param name="schemaName">Schema to inspect (default: public)</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>Schema definition of the database</returns>
    public static SchemaResult Inspect(
        NpgsqlConnection connection,
        string schemaName = "public",
        ILogger? logger = null
    )
    {
        try
        {
            var tables = new List<TableDefinition>();

            // Get all user tables
            using var tablesCmd = connection.CreateCommand();
            tablesCmd.CommandText = """
                SELECT table_name 
                FROM information_schema.tables 
                WHERE table_schema = @schema 
                AND table_type = 'BASE TABLE'
                ORDER BY table_name
                """;
            tablesCmd.Parameters.AddWithValue("@schema", schemaName);

            var tableNames = new List<string>();
            using (var reader = tablesCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    tableNames.Add(reader.GetString(0));
                }
            }

            foreach (var tableName in tableNames)
            {
                var tableResult = InspectTable(connection, schemaName, tableName, logger);
                if (tableResult is TableResult.Ok<TableDefinition, MigrationError> ok)
                {
                    tables.Add(ok.Value);
                }
                else if (
                    tableResult is TableResult.Error<TableDefinition, MigrationError> tableError
                )
                {
                    logger?.LogWarning(
                        "Failed to inspect table {Table}: {Error}",
                        tableName,
                        tableError.Value
                    );
                }
            }

            return new SchemaResult.Ok<SchemaDefinition, MigrationError>(
                new SchemaDefinition { Name = schemaName, Tables = tables.AsReadOnly() }
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to inspect PostgreSQL schema");
            return new SchemaResult.Error<SchemaDefinition, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
    }

    /// <summary>
    /// Inspect a single table.
    /// </summary>
    public static TableResult InspectTable(
        NpgsqlConnection connection,
        string schemaName,
        string tableName,
        ILogger? logger = null
    )
    {
        try
        {
            var columns = new List<ColumnDefinition>();
            var indexes = new List<IndexDefinition>();
            var foreignKeys = new List<ForeignKeyDefinition>();
            PrimaryKeyDefinition? primaryKey = null;

            // Get column info
            using var colCmd = connection.CreateCommand();
            colCmd.CommandText = """
                SELECT 
                    column_name,
                    data_type,
                    is_nullable,
                    column_default,
                    character_maximum_length,
                    numeric_precision,
                    numeric_scale
                FROM information_schema.columns
                WHERE table_schema = @schema AND table_name = @table
                ORDER BY ordinal_position
                """;
            colCmd.Parameters.AddWithValue("@schema", schemaName);
            colCmd.Parameters.AddWithValue("@table", tableName);

            using (var reader = colCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var dataType = reader.GetString(1);
                    var isNullable = reader.GetString(2) == "YES";
                    var defaultValue = reader.IsDBNull(3) ? null : reader.GetString(3);
                    var charMaxLen = reader.IsDBNull(4) ? null : (int?)reader.GetInt32(4);
                    var numPrecision = reader.IsDBNull(5) ? null : (int?)reader.GetInt32(5);
                    var numScale = reader.IsDBNull(6) ? null : (int?)reader.GetInt32(6);

                    var isIdentity =
                        defaultValue?.Contains("nextval", StringComparison.OrdinalIgnoreCase)
                        ?? false;

                    columns.Add(
                        new ColumnDefinition
                        {
                            Name = name,
                            Type = PostgresTypeToPortable(
                                dataType,
                                charMaxLen,
                                numPrecision,
                                numScale
                            ),
                            IsNullable = isNullable,
                            DefaultValue = isIdentity ? null : defaultValue,
                            IsIdentity = isIdentity,
                        }
                    );
                }
            }

            // Get primary key
            using var pkCmd = connection.CreateCommand();
            pkCmd.CommandText = """
                SELECT kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu 
                    ON tc.constraint_name = kcu.constraint_name 
                    AND tc.table_schema = kcu.table_schema
                WHERE tc.table_schema = @schema 
                AND tc.table_name = @table 
                AND tc.constraint_type = 'PRIMARY KEY'
                ORDER BY kcu.ordinal_position
                """;
            pkCmd.Parameters.AddWithValue("@schema", schemaName);
            pkCmd.Parameters.AddWithValue("@table", tableName);

            var pkColumns = new List<string>();
            using (var reader = pkCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    pkColumns.Add(reader.GetString(0));
                }
            }

            if (pkColumns.Count > 0)
            {
                primaryKey = new PrimaryKeyDefinition
                {
                    Name = $"PK_{tableName}",
                    Columns = pkColumns.AsReadOnly(),
                };
            }

            // Get indexes
            using var idxCmd = connection.CreateCommand();
            idxCmd.CommandText = """
                SELECT 
                    i.relname AS index_name,
                    a.attname AS column_name,
                    ix.indisunique AS is_unique
                FROM pg_class t
                JOIN pg_index ix ON t.oid = ix.indrelid
                JOIN pg_class i ON i.oid = ix.indexrelid
                JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(ix.indkey)
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE n.nspname = @schema 
                AND t.relname = @table
                AND NOT ix.indisprimary
                ORDER BY i.relname, a.attnum
                """;
            idxCmd.Parameters.AddWithValue("@schema", schemaName);
            idxCmd.Parameters.AddWithValue("@table", tableName);

            var indexData = new Dictionary<string, (bool IsUnique, List<string> Columns)>();
            using (var reader = idxCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var indexName = reader.GetString(0);
                    var columnName = reader.GetString(1);
                    var isUnique = reader.GetBoolean(2);

                    if (!indexData.TryGetValue(indexName, out var data))
                    {
                        data = (isUnique, []);
                        indexData[indexName] = data;
                    }
                    data.Columns.Add(columnName);
                }
            }

            foreach (var (indexName, (isUnique, indexColumns)) in indexData)
            {
                indexes.Add(
                    new IndexDefinition
                    {
                        Name = indexName,
                        Columns = indexColumns.AsReadOnly(),
                        IsUnique = isUnique,
                    }
                );
            }

            // Get foreign keys
            using var fkCmd = connection.CreateCommand();
            fkCmd.CommandText = """
                SELECT
                    tc.constraint_name,
                    kcu.column_name,
                    ccu.table_name AS referenced_table,
                    ccu.column_name AS referenced_column,
                    rc.delete_rule,
                    rc.update_rule
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu 
                    ON tc.constraint_name = kcu.constraint_name 
                    AND tc.table_schema = kcu.table_schema
                JOIN information_schema.constraint_column_usage ccu 
                    ON ccu.constraint_name = tc.constraint_name
                JOIN information_schema.referential_constraints rc 
                    ON tc.constraint_name = rc.constraint_name
                WHERE tc.table_schema = @schema 
                AND tc.table_name = @table 
                AND tc.constraint_type = 'FOREIGN KEY'
                """;
            fkCmd.Parameters.AddWithValue("@schema", schemaName);
            fkCmd.Parameters.AddWithValue("@table", tableName);

            using (var reader = fkCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var constraintName = reader.GetString(0);
                    var columnName = reader.GetString(1);
                    var refTable = reader.GetString(2);
                    var refColumn = reader.GetString(3);
                    var deleteRule = reader.GetString(4);
                    var updateRule = reader.GetString(5);

                    foreignKeys.Add(
                        new ForeignKeyDefinition
                        {
                            Name = constraintName,
                            Columns = [columnName],
                            ReferencedTable = refTable,
                            ReferencedSchema = schemaName,
                            ReferencedColumns = [refColumn],
                            OnDelete = ParseForeignKeyAction(deleteRule),
                            OnUpdate = ParseForeignKeyAction(updateRule),
                        }
                    );
                }
            }

            return new TableResult.Ok<TableDefinition, MigrationError>(
                new TableDefinition
                {
                    Schema = schemaName,
                    Name = tableName,
                    Columns = columns.AsReadOnly(),
                    Indexes = indexes.AsReadOnly(),
                    ForeignKeys = foreignKeys.AsReadOnly(),
                    PrimaryKey = primaryKey,
                }
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to inspect table {Schema}.{Table}", schemaName, tableName);
            return new TableResult.Error<TableDefinition, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
    }

    /// <summary>
    /// Convert PostgreSQL type to portable type.
    /// </summary>
    public static PortableType PostgresTypeToPortable(
        string pgType,
        int? charMaxLen,
        int? numPrecision,
        int? numScale
    )
    {
        var upper = pgType.ToUpperInvariant();

        return upper switch
        {
            "SMALLINT" => new SmallIntType(),
            "INTEGER" or "INT" or "INT4" => new IntType(),
            "BIGINT" or "INT8" => new BigIntType(),
            "BOOLEAN" or "BOOL" => new BooleanType(),

            "NUMERIC" or "DECIMAL" when numPrecision.HasValue && numScale.HasValue =>
                new DecimalType(numPrecision.Value, numScale.Value),
            "NUMERIC" or "DECIMAL" => new DecimalType(18, 2),
            "REAL" or "FLOAT4" => new FloatType(),
            "DOUBLE PRECISION" or "FLOAT8" => new DoubleType(),

            "CHARACTER VARYING" or "VARCHAR" when charMaxLen.HasValue => new VarCharType(
                charMaxLen.Value
            ),
            "CHARACTER VARYING" or "VARCHAR" => new TextType(),
            "CHARACTER" or "CHAR" when charMaxLen.HasValue => new CharType(charMaxLen.Value),
            "TEXT" => new TextType(),
            "JSONB" or "JSON" => new JsonType(),
            "XML" => new XmlType(),

            "BYTEA" => new BlobType(),

            "DATE" => new DateType(),
            "TIME" or "TIME WITHOUT TIME ZONE" => new TimeType(),
            "TIMESTAMP" or "TIMESTAMP WITHOUT TIME ZONE" => new DateTimeType(),
            "TIMESTAMP WITH TIME ZONE" or "TIMESTAMPTZ" => new DateTimeOffsetType(),

            "UUID" => new UuidType(),

            _ => new TextType(),
        };
    }

    private static ForeignKeyAction ParseForeignKeyAction(string action) =>
        action.ToUpperInvariant() switch
        {
            "CASCADE" => ForeignKeyAction.Cascade,
            "SET NULL" => ForeignKeyAction.SetNull,
            "SET DEFAULT" => ForeignKeyAction.SetDefault,
            "RESTRICT" => ForeignKeyAction.Restrict,
            _ => ForeignKeyAction.NoAction,
        };
}
