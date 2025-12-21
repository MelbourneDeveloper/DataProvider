namespace Sync.Postgres;

/// <summary>
/// Manages sync session state for PostgreSQL.
/// Implements spec Section 8 (Trigger Suppression) for Postgres.
/// </summary>
public static class PostgresSyncSession
{
    /// <summary>
    /// Enables suppression (sets sync_active = 1).
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult EnableSuppression(NpgsqlConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE _sync_session SET sync_active = 1";
            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to enable suppression: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Disables suppression (sets sync_active = 0).
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult DisableSuppression(NpgsqlConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE _sync_session SET sync_active = 0";
            cmd.ExecuteNonQuery();
            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to disable suppression: {ex.Message}")
            );
        }
    }
}
