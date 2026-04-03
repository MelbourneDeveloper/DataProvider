using Microsoft.Data.Sqlite;
using Npgsql;

// TODO: Logging!!
#pragma warning disable IDE0060 // Remove unused parameter

namespace Nimblesite.Sync.Http;

/// <summary>
/// Helper methods for sync database operations.
/// Static methods that work with both SQLite and PostgreSQL.
/// </summary>
public static class Nimblesite.Sync.CoreHelpers
{
    /// <summary>
    /// Fetches changes from the sync log for the specified database type.
    /// </summary>
    public static List<Nimblesite.Sync.CoreLogEntry> FetchChanges(
        string connectionString,
        string dbType,
        long fromVersion,
        int batchSize,
        ILogger logger
    ) =>
        dbType.ToLowerInvariant() switch
        {
            "sqlite" => FetchChangesFromSqlite(connectionString, fromVersion, batchSize, logger),
            "postgres" => FetchChangesFromPostgres(
                connectionString,
                fromVersion,
                batchSize,
                logger
            ),
            _ => throw new ArgumentException($"Unknown database type: {dbType}"),
        };

    /// <summary>
    /// Applies changes to the database for the specified database type.
    /// </summary>
    public static int ApplyChanges(
        string connectionString,
        string dbType,
        List<Nimblesite.Sync.CoreLogEntryDto> changes,
        string originId,
        ILogger logger
    ) =>
        dbType.ToLowerInvariant() switch
        {
            "sqlite" => ApplyChangesToSqlite(connectionString, changes, originId, logger),
            "postgres" => ApplyChangesToPostgres(connectionString, changes, originId, logger),
            _ => throw new ArgumentException($"Unknown database type: {dbType}"),
        };

    /// <summary>
    /// Upserts a sync client for the specified database type.
    /// </summary>
    public static bool UpsertClient(
        string connectionString,
        string dbType,
        Nimblesite.Sync.CoreClient client,
        ILogger logger
    ) =>
        dbType.ToLowerInvariant() switch
        {
            "sqlite" => UpsertClientSqlite(connectionString, client, logger),
            "postgres" => UpsertClientPostgres(connectionString, client, logger),
            _ => throw new ArgumentException($"Unknown database type: {dbType}"),
        };

    /// <summary>
    /// Gets the maximum sync version for the specified database type.
    /// </summary>
    public static long GetMaxVersion(string connectionString, string dbType, ILogger logger) =>
        dbType.ToLowerInvariant() switch
        {
            "sqlite" => GetMaxVersionSqlite(connectionString, logger),
            "postgres" => GetMaxVersionPostgres(connectionString, logger),
            _ => throw new ArgumentException($"Unknown database type: {dbType}"),
        };

    private static List<Nimblesite.Sync.CoreLogEntry> FetchChangesFromSqlite(
        string connectionString,
        long fromVersion,
        int batchSize,
        ILogger logger
    )
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        return SQLite
            .Nimblesite.Sync.CoreLogRepository.FetchChanges(conn, fromVersion, batchSize)
            .Match<List<Nimblesite.Sync.CoreLogEntry>>(ok => [.. ok], _ => []);
    }

    private static List<Nimblesite.Sync.CoreLogEntry> FetchChangesFromPostgres(
        string connectionString,
        long fromVersion,
        int batchSize,
        ILogger logger
    )
    {
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        return Postgres
            .PostgresSyncLogRepository.FetchChanges(conn, fromVersion, batchSize)
            .Match<List<Nimblesite.Sync.CoreLogEntry>>(ok => [.. ok], _ => []);
    }

    private static int ApplyChangesToSqlite(
        string connectionString,
        List<Nimblesite.Sync.CoreLogEntryDto> changes,
        string originId,
        ILogger logger
    )
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();

        SQLite.Nimblesite.Sync.CoreSessionManager.EnableSuppression(conn);

        try
        {
            var applied = 0;
            foreach (var change in changes)
            {
                if (change.Origin == originId)
                    continue;

                var entry = new Nimblesite.Sync.CoreLogEntry(
                    change.Version,
                    change.TableName,
                    change.PkValue,
                    Enum.Parse<Nimblesite.Sync.CoreOperation>(change.Operation, true),
                    change.Payload,
                    change.Origin,
                    change.Timestamp
                );

                var result = SQLite.ChangeApplierSQLite.ApplyChange(conn, entry);
                if (result is Outcome.Result<bool, Nimblesite.Sync.CoreError>.Ok<bool, Nimblesite.Sync.CoreError>)
                    applied++;
            }
            return applied;
        }
        finally
        {
            SQLite.Nimblesite.Sync.CoreSessionManager.DisableSuppression(conn);
        }
    }

    private static int ApplyChangesToPostgres(
        string connectionString,
        List<Nimblesite.Sync.CoreLogEntryDto> changes,
        string originId,
        ILogger logger
    )
    {
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();

        Postgres.PostgresSyncSession.EnableSuppression(conn);

        try
        {
            var applied = 0;
            foreach (var change in changes)
            {
                if (change.Origin == originId)
                    continue;

                var entry = new Nimblesite.Sync.CoreLogEntry(
                    change.Version,
                    change.TableName,
                    change.PkValue,
                    Enum.Parse<Nimblesite.Sync.CoreOperation>(change.Operation, true),
                    change.Payload,
                    change.Origin,
                    change.Timestamp
                );

                var result = Postgres.PostgresChangeApplier.ApplyChange(conn, entry, logger);
                if (result is Outcome.Result<bool, Nimblesite.Sync.CoreError>.Ok<bool, Nimblesite.Sync.CoreError>)
                    applied++;
            }
            return applied;
        }
        finally
        {
            Postgres.PostgresSyncSession.DisableSuppression(conn);
        }
    }

    private static bool UpsertClientSqlite(
        string connectionString,
        Nimblesite.Sync.CoreClient client,
        ILogger logger
    )
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        var result = SQLite.Nimblesite.Sync.CoreClientRepository.Upsert(conn, client);
        return result is Outcome.Result<bool, Nimblesite.Sync.CoreError>.Ok<bool, Nimblesite.Sync.CoreError>;
    }

    private static bool UpsertClientPostgres(
        string connectionString,
        Nimblesite.Sync.CoreClient client,
        ILogger logger
    )
    {
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        var result = Postgres.PostgresSyncClientRepository.Upsert(conn, client);
        return result is Outcome.Result<bool, Nimblesite.Sync.CoreError>.Ok<bool, Nimblesite.Sync.CoreError>;
    }

    private static long GetMaxVersionSqlite(string connectionString, ILogger logger)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        return SQLite.Nimblesite.Sync.CoreLogRepository.GetMaxVersion(conn).Match(ok => ok, _ => 0);
    }

    private static long GetMaxVersionPostgres(string connectionString, ILogger logger)
    {
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        return Postgres.PostgresSyncLogRepository.GetMaxVersion(conn).Match(ok => ok, _ => 0);
    }
}
