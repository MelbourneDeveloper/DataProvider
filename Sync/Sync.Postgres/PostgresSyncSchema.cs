using Npgsql;

namespace Sync.Postgres;

/// <summary>
/// Creates and manages sync schema tables for PostgreSQL.
/// Implements spec Appendix A schema for PostgreSQL dialect.
/// </summary>
public static class PostgresSyncSchema
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
    /// Uses BIGSERIAL for version since PostgreSQL doesn't have AUTOINCREMENT.
    /// </summary>
    public const string CreateSyncLogTable = """
        CREATE TABLE IF NOT EXISTS _sync_log (
            version     BIGSERIAL PRIMARY KEY,
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
    /// SQL to create sync clients table (_sync_clients).
    /// Server-side tracking of client sync state for tombstone retention.
    /// </summary>
    public const string CreateSyncClientsTable = """
        CREATE TABLE IF NOT EXISTS _sync_clients (
            origin_id TEXT PRIMARY KEY,
            last_sync_version BIGINT NOT NULL DEFAULT 0,
            last_sync_timestamp TEXT NOT NULL,
            created_at TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_sync_clients_version ON _sync_clients(last_sync_version);
        """;

    /// <summary>
    /// SQL to create sync subscriptions table (_sync_subscriptions).
    /// Server-side tracking of real-time subscriptions.
    /// </summary>
    public const string CreateSyncSubscriptionsTable = """
        CREATE TABLE IF NOT EXISTS _sync_subscriptions (
            subscription_id TEXT PRIMARY KEY,
            origin_id TEXT NOT NULL,
            subscription_type TEXT NOT NULL CHECK (subscription_type IN ('record', 'table', 'query')),
            table_name TEXT NOT NULL,
            filter TEXT,
            created_at TEXT NOT NULL,
            expires_at TEXT
        );

        CREATE INDEX IF NOT EXISTS idx_subscriptions_table ON _sync_subscriptions(table_name);
        CREATE INDEX IF NOT EXISTS idx_subscriptions_origin ON _sync_subscriptions(origin_id);
        """;

    /// <summary>
    /// SQL to initialize sync state with default values.
    /// Uses ON CONFLICT DO NOTHING for idempotent initialization.
    /// </summary>
    public const string InitializeSyncState = """
        INSERT INTO _sync_state (key, value) VALUES ('origin_id', '') ON CONFLICT DO NOTHING;
        INSERT INTO _sync_state (key, value) VALUES ('last_server_version', '0') ON CONFLICT DO NOTHING;
        INSERT INTO _sync_state (key, value) VALUES ('last_push_version', '0') ON CONFLICT DO NOTHING;
        DELETE FROM _sync_session;
        INSERT INTO _sync_session (sync_active) VALUES (0);
        """;

    /// <summary>
    /// Creates all sync tables in the database.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult CreateSchema(NpgsqlConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                $"{CreateSyncStateTable}\n{CreateSyncSessionTable}\n{CreateSyncLogTable}\n{CreateSyncClientsTable}\n{CreateSyncSubscriptionsTable}\n{InitializeSyncState}";
            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to create sync schema: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Sets the origin ID for this replica. Should be called once on first sync.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="originId">UUID v4 origin ID.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult SetOriginId(NpgsqlConnection connection, string originId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE _sync_state SET value = @origin WHERE key = 'origin_id'";
            cmd.Parameters.AddWithValue("@origin", originId);
            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to set origin ID: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets the origin ID for this replica.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>Origin ID or database error.</returns>
    public static StringSyncResult GetOriginId(NpgsqlConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM _sync_state WHERE key = 'origin_id'";
            var result = cmd.ExecuteScalar();
            return result is string originId
                ? new StringSyncOk(originId)
                : new StringSyncError(new SyncErrorDatabase("Origin ID not found"));
        }
        catch (NpgsqlException ex)
        {
            return new StringSyncError(
                new SyncErrorDatabase($"Failed to get origin ID: {ex.Message}")
            );
        }
    }
}
