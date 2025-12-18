using System.Globalization;
using Microsoft.Data.Sqlite;
using Results;

namespace Sync.SQLite;

/// <summary>
/// Repository for reading and writing sync log entries in SQLite.
/// Implements spec Section 7 (Unified Change Log) and Section 12 (Batching).
/// </summary>
public static class SyncLogRepository
{
    /// <summary>
    /// Fetches a batch of changes from the sync log.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="fromVersion">Fetch changes with version greater than this.</param>
    /// <param name="batchSize">Maximum number of changes to fetch.</param>
    /// <returns>List of sync log entries or database error.</returns>
    public static Result<IReadOnlyList<SyncLogEntry>, SyncError> FetchChanges(
        SqliteConnection connection,
        long fromVersion,
        int batchSize
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT version, table_name, pk_value, operation, payload, origin, timestamp
                FROM _sync_log
                WHERE version > @fromVersion
                ORDER BY version ASC
                LIMIT @batchSize
                """;
            cmd.Parameters.AddWithValue("@fromVersion", fromVersion);
            cmd.Parameters.AddWithValue("@batchSize", batchSize);

            var entries = new List<SyncLogEntry>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var entry = new SyncLogEntry(
                    Version: reader.GetInt64(0),
                    TableName: reader.GetString(1),
                    PkValue: reader.GetString(2),
                    Operation: ParseOperation(reader.GetString(3)),
                    Payload: reader.IsDBNull(4) ? null : reader.GetString(4),
                    Origin: reader.GetString(5),
                    Timestamp: reader.GetString(6)
                );
                entries.Add(entry);
            }

            return new Result<IReadOnlyList<SyncLogEntry>, SyncError>.Success(entries);
        }
        catch (SqliteException ex)
        {
            return new Result<IReadOnlyList<SyncLogEntry>, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to fetch changes: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets the last server version from sync state.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Last server version or database error.</returns>
    public static Result<long, SyncError> GetLastServerVersion(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM _sync_state WHERE key = 'last_server_version'";
            var result = cmd.ExecuteScalar();
            return result is string strValue && long.TryParse(strValue, out var version)
                ? new Result<long, SyncError>.Success(version)
                : new Result<long, SyncError>.Success(0);
        }
        catch (SqliteException ex)
        {
            return new Result<long, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to get last server version: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Updates the last server version in sync state.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="version">New last server version.</param>
    /// <returns>Success or database error.</returns>
    public static Result<bool, SyncError> UpdateLastServerVersion(
        SqliteConnection connection,
        long version
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE _sync_state SET value = @version WHERE key = 'last_server_version'";
            cmd.Parameters.AddWithValue("@version", version.ToString(CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
            return new Result<bool, SyncError>.Success(true);
        }
        catch (SqliteException ex)
        {
            return new Result<bool, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to update last server version: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Parses operation string to SyncOperation enum.
    /// </summary>
    private static SyncOperation ParseOperation(string operation) =>
        operation.ToLowerInvariant() switch
        {
            "insert" => SyncOperation.Insert,
            "update" => SyncOperation.Update,
            "delete" => SyncOperation.Delete,
            _ => SyncOperation.Insert,
        };
}
