#pragma warning disable CA1848 // Use LoggerMessage delegates for performance

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Sync.Postgres;

/// <summary>
/// Column information for trigger generation.
/// </summary>
/// <param name="Name">Column name.</param>
/// <param name="Type">Column type.</param>
/// <param name="IsPrimaryKey">Whether this is the primary key.</param>
public sealed record TriggerColumnInfo(string Name, string Type, bool IsPrimaryKey);

/// <summary>
/// Generates sync triggers for PostgreSQL tables.
/// Implements spec Section 9 (LQL Trigger Generation) for PostgreSQL dialect.
/// </summary>
public static class PostgresTriggerGenerator
{
    /// <summary>
    /// Generates sync triggers for a table.
    /// Creates INSERT, UPDATE, and DELETE triggers that log changes to _sync_log.
    /// </summary>
    /// <param name="tableName">Name of the table to generate triggers for.</param>
    /// <param name="columns">List of column names to include in payload.</param>
    /// <param name="pkColumn">Primary key column name.</param>
    /// <returns>SQL DDL for all three triggers.</returns>
    public static string GenerateTriggers(
        string tableName,
        IReadOnlyList<string> columns,
        string pkColumn
    )
    {
        var sb = new StringBuilder();

        sb.AppendLine(GenerateInsertTrigger(tableName, columns, pkColumn));
        sb.AppendLine();
        sb.AppendLine(GenerateUpdateTrigger(tableName, columns, pkColumn));
        sb.AppendLine();
        sb.AppendLine(GenerateDeleteTrigger(tableName, pkColumn));

        return sb.ToString();
    }

    /// <summary>
    /// Generates triggers for a table by reading schema from database.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="tableName">Table to generate triggers for.</param>
    /// <param name="logger">Logger for trigger generation.</param>
    /// <returns>Trigger SQL or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Information schema query is safe metadata query"
    )]
    public static StringSyncResult GenerateTriggersFromSchema(
        NpgsqlConnection connection,
        string tableName,
        ILogger logger
    )
    {
        logger.LogDebug("TRIGGER: Generating triggers from schema for table {Table}", tableName);

        var columnsResult = GetTableColumns(connection, tableName);

        if (columnsResult is ColumnInfoListError error)
        {
            logger.LogError(
                "TRIGGER: Failed to get columns for {Table}: {Error}",
                tableName,
                error.Value
            );
            return new StringSyncError(error.Value);
        }

        var columns = ((ColumnInfoListOk)columnsResult).Value;

        if (columns.Count == 0)
        {
            logger.LogError("TRIGGER: Table {Table} not found or has no columns", tableName);
            return new StringSyncError(
                new SyncErrorDatabase($"Table '{tableName}' not found or has no columns")
            );
        }

        var pkColumn = columns.FirstOrDefault(c => c.IsPrimaryKey);
        if (pkColumn is null)
        {
            logger.LogError("TRIGGER: Table {Table} has no primary key", tableName);
            return new StringSyncError(
                new SyncErrorDatabase($"Table '{tableName}' has no primary key")
            );
        }

        logger.LogDebug(
            "TRIGGER: Table {Table} has {ColumnCount} columns, PK={PK}",
            tableName,
            columns.Count,
            pkColumn.Name
        );

        var columnNames = columns.Select(c => c.Name).ToList();
        var triggers = GenerateTriggers(tableName, columnNames, pkColumn.Name);

        logger.LogInformation(
            "TRIGGER: Generated INSERT/UPDATE/DELETE triggers for table {Table}",
            tableName
        );

        return new StringSyncOk(triggers);
    }

    /// <summary>
    /// Creates sync triggers in the database for a table.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="tableName">Table to create triggers for.</param>
    /// <param name="logger">Logger for trigger creation.</param>
    /// <returns>Success or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Trigger DDL is generated from trusted schema metadata"
    )]
    public static BoolSyncResult CreateTriggers(
        NpgsqlConnection connection,
        string tableName,
        ILogger logger
    )
    {
        logger.LogDebug("TRIGGER: Creating triggers for table {Table}", tableName);

        var triggersResult = GenerateTriggersFromSchema(connection, tableName, logger);

        if (triggersResult is StringSyncError error)
        {
            return new BoolSyncError(error.Value);
        }

        var triggers = ((StringSyncOk)triggersResult).Value;

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = triggers;
            cmd.ExecuteNonQuery();

            logger.LogInformation("TRIGGER: Created triggers for table {Table}", tableName);
            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "TRIGGER: Failed to create triggers for {Table}", tableName);
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to create triggers: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Drops sync triggers for a table.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="tableName">Table to drop triggers for.</param>
    /// <param name="logger">Logger for trigger operations.</param>
    /// <returns>Success or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Table name is used for DROP TRIGGER IF EXISTS which is safe"
    )]
    public static BoolSyncResult DropTriggers(
        NpgsqlConnection connection,
        string tableName,
        ILogger logger
    )
    {
        logger.LogDebug("TRIGGER: Dropping triggers for table {Table}", tableName);

        try
        {
            using var cmd = connection.CreateCommand();
            var lowerTable = tableName.ToLowerInvariant();
            cmd.CommandText = string.Format(
                CultureInfo.InvariantCulture,
                """
                DROP TRIGGER IF EXISTS {0}_sync_insert ON {1};
                DROP TRIGGER IF EXISTS {0}_sync_update ON {1};
                DROP TRIGGER IF EXISTS {0}_sync_delete ON {1};
                DROP FUNCTION IF EXISTS {0}_sync_insert_fn();
                DROP FUNCTION IF EXISTS {0}_sync_update_fn();
                DROP FUNCTION IF EXISTS {0}_sync_delete_fn();
                """,
                lowerTable,
                tableName
            );
            cmd.ExecuteNonQuery();

            logger.LogInformation("TRIGGER: Dropped triggers for table {Table}", tableName);
            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "TRIGGER: Failed to drop triggers for {Table}", tableName);
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to drop triggers: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets column information for a table.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="tableName">Table name.</param>
    /// <returns>List of columns or database error.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Information schema query is safe metadata query"
    )]
    public static ColumnInfoListResult GetTableColumns(
        NpgsqlConnection connection,
        string tableName
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT 
                    c.column_name,
                    c.data_type,
                    CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_pk
                FROM information_schema.columns c
                LEFT JOIN (
                    SELECT kcu.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage kcu 
                        ON tc.constraint_name = kcu.constraint_name
                        AND tc.table_schema = kcu.table_schema
                    WHERE tc.table_name = @tableName
                        AND tc.constraint_type = 'PRIMARY KEY'
                ) pk ON c.column_name = pk.column_name
                WHERE c.table_name = @tableName
                    AND c.table_schema = 'public'
                ORDER BY c.ordinal_position
                """;
            cmd.Parameters.AddWithValue("@tableName", tableName.ToLowerInvariant());

            var columns = new List<TriggerColumnInfo>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                columns.Add(
                    new TriggerColumnInfo(
                        Name: reader.GetString(0),
                        Type: reader.GetString(1),
                        IsPrimaryKey: reader.GetBoolean(2)
                    )
                );
            }

            return new ColumnInfoListOk(columns);
        }
        catch (NpgsqlException ex)
        {
            return new ColumnInfoListError(
                new SyncErrorDatabase($"Failed to get table columns: {ex.Message}")
            );
        }
    }

    private static string GenerateInsertTrigger(
        string tableName,
        IReadOnlyList<string> columns,
        string pkColumn
    )
    {
        var lowerTable = tableName.ToLowerInvariant();
        return string.Format(
            CultureInfo.InvariantCulture,
            """
            CREATE OR REPLACE FUNCTION {0}_sync_insert_fn() RETURNS TRIGGER AS $$
            BEGIN
                IF (SELECT sync_active FROM _sync_session LIMIT 1) = 0 THEN
                    INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                    VALUES (
                        '{1}',
                        jsonb_build_object('{2}', NEW.{2})::text,
                        'insert',
                        {3}::text,
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
                    );
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER {0}_sync_insert
            AFTER INSERT ON {1}
            FOR EACH ROW EXECUTE FUNCTION {0}_sync_insert_fn();
            """,
            lowerTable,
            tableName,
            pkColumn,
            BuildJsonbObject(columns, "NEW")
        );
    }

    private static string GenerateUpdateTrigger(
        string tableName,
        IReadOnlyList<string> columns,
        string pkColumn
    )
    {
        var lowerTable = tableName.ToLowerInvariant();
        return string.Format(
            CultureInfo.InvariantCulture,
            """
            CREATE OR REPLACE FUNCTION {0}_sync_update_fn() RETURNS TRIGGER AS $$
            BEGIN
                IF (SELECT sync_active FROM _sync_session LIMIT 1) = 0 THEN
                    INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                    VALUES (
                        '{1}',
                        jsonb_build_object('{2}', NEW.{2})::text,
                        'update',
                        {3}::text,
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
                    );
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER {0}_sync_update
            AFTER UPDATE ON {1}
            FOR EACH ROW EXECUTE FUNCTION {0}_sync_update_fn();
            """,
            lowerTable,
            tableName,
            pkColumn,
            BuildJsonbObject(columns, "NEW")
        );
    }

    private static string GenerateDeleteTrigger(string tableName, string pkColumn)
    {
        var lowerTable = tableName.ToLowerInvariant();
        return string.Format(
            CultureInfo.InvariantCulture,
            """
            CREATE OR REPLACE FUNCTION {0}_sync_delete_fn() RETURNS TRIGGER AS $$
            BEGIN
                IF (SELECT sync_active FROM _sync_session LIMIT 1) = 0 THEN
                    INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
                    VALUES (
                        '{1}',
                        jsonb_build_object('{2}', OLD.{2})::text,
                        'delete',
                        NULL,
                        (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                        to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
                    );
                END IF;
                RETURN OLD;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER {0}_sync_delete
            AFTER DELETE ON {1}
            FOR EACH ROW EXECUTE FUNCTION {0}_sync_delete_fn();
            """,
            lowerTable,
            tableName,
            pkColumn
        );
    }

    private static string BuildJsonbObject(IReadOnlyList<string> columns, string prefix)
    {
        var pairs = columns.Select(c => $"'{c}', {prefix}.{c}");
        return $"jsonb_build_object({string.Join(", ", pairs)})";
    }
}
