using Microsoft.Data.Sqlite;
using Results;

namespace Sync.SQLite;

/// <summary>
/// Manages sync session state for trigger suppression in SQLite.
/// Implements spec Section 8 (Trigger Suppression).
/// </summary>
public static class SyncSessionManager
{
    /// <summary>
    /// Enables trigger suppression. Call before applying incoming changes.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Success or database error.</returns>
    public static Result<bool, SyncError> EnableSuppression(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE _sync_session SET sync_active = 1";
            cmd.ExecuteNonQuery();
            return new Result<bool, SyncError>.Success(true);
        }
        catch (SqliteException ex)
        {
            return new Result<bool, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to enable trigger suppression: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Disables trigger suppression. Call after sync completes.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Success or database error.</returns>
    public static Result<bool, SyncError> DisableSuppression(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE _sync_session SET sync_active = 0";
            cmd.ExecuteNonQuery();
            return new Result<bool, SyncError>.Success(true);
        }
        catch (SqliteException ex)
        {
            return new Result<bool, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to disable trigger suppression: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Checks if trigger suppression is currently active.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>True if suppression active, or database error.</returns>
    public static Result<bool, SyncError> IsSuppressionActive(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT sync_active FROM _sync_session LIMIT 1";
            var result = cmd.ExecuteScalar();
            return new Result<bool, SyncError>.Success(
                result is long value && value == 1
            );
        }
        catch (SqliteException ex)
        {
            return new Result<bool, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to check suppression state: {ex.Message}")
            );
        }
    }
}
