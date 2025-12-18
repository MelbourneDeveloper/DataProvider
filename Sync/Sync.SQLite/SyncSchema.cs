using Microsoft.Data.Sqlite;
using Results;

namespace Sync.SQLite;

/// <summary>
/// Creates and manages sync schema tables for SQLite.
/// Implements spec Appendix A schema.
/// </summary>
public static class SyncSchema
{
    /// <summary>
    /// SQL to create sync state table (_sync_state).
    /// Stores origin ID and version tracking.
    /// </summary>
    public const string CreateSyncStateTable = """
        CREATE TABLE IF NOT EXISTS _sync_state (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """;

    /// <summary>
    /// SQL to create sync session table (_sync_session).
    /// Single row with sync_active flag for trigger suppression.
    /// </summary>
    public const string CreateSyncSessionTable = """
        CREATE TABLE IF NOT EXISTS _sync_session (
            sync_active INTEGER DEFAULT 0
        );
        """;

    /// <summary>
    /// SQL to create sync log table (_sync_log).
    /// Unified change log with JSON payloads.
    /// </summary>
    public const string CreateSyncLogTable = """
        CREATE TABLE IF NOT EXISTS _sync_log (
            version     INTEGER PRIMARY KEY AUTOINCREMENT,
            table_name  TEXT NOT NULL,
            pk_value    TEXT NOT NULL,
            operation   TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
            payload     TEXT,
            origin      TEXT NOT NULL,
            timestamp   TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_sync_log_version ON _sync_log(version);
        CREATE INDEX IF NOT EXISTS idx_sync_log_table ON _sync_log(table_name, version);
        """;

    /// <summary>
    /// SQL to initialize sync state with default values.
    /// </summary>
    public const string InitializeSyncState = """
        INSERT OR IGNORE INTO _sync_state (key, value) VALUES ('origin_id', '');
        INSERT OR IGNORE INTO _sync_state (key, value) VALUES ('last_server_version', '0');
        INSERT OR IGNORE INTO _sync_state (key, value) VALUES ('last_push_version', '0');
        INSERT OR IGNORE INTO _sync_session (sync_active) VALUES (0);
        """;

    /// <summary>
    /// Creates all sync tables in the database.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Success or database error.</returns>
    public static Result<bool, SyncError> CreateSchema(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"{CreateSyncStateTable}\n{CreateSyncSessionTable}\n{CreateSyncLogTable}\n{InitializeSyncState}";
            cmd.ExecuteNonQuery();
            return new Result<bool, SyncError>.Success(true);
        }
        catch (SqliteException ex)
        {
            return new Result<bool, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to create sync schema: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Sets the origin ID for this replica. Should be called once on first sync.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originId">UUID v4 origin ID.</param>
    /// <returns>Success or database error.</returns>
    public static Result<bool, SyncError> SetOriginId(SqliteConnection connection, string originId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE _sync_state SET value = @origin WHERE key = 'origin_id'";
            cmd.Parameters.AddWithValue("@origin", originId);
            cmd.ExecuteNonQuery();
            return new Result<bool, SyncError>.Success(true);
        }
        catch (SqliteException ex)
        {
            return new Result<bool, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to set origin ID: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets the origin ID for this replica.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Origin ID or database error.</returns>
    public static Result<string, SyncError> GetOriginId(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM _sync_state WHERE key = 'origin_id'";
            var result = cmd.ExecuteScalar();
            return result is string originId
                ? new Result<string, SyncError>.Success(originId)
                : new Result<string, SyncError>.Failure(
                    new SyncErrorDatabase("Origin ID not found")
                );
        }
        catch (SqliteException ex)
        {
            return new Result<string, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to get origin ID: {ex.Message}")
            );
        }
    }
}
