using Microsoft.Data.Sqlite;
using Results;

namespace Sync.SQLite;

/// <summary>
/// Repository for CRUD operations on _sync_clients table.
/// Server-side tracking of client sync state for tombstone retention.
/// Implements spec Section 13.3.
/// </summary>
public static class SyncClientRepository
{
    /// <summary>
    /// Gets all tracked clients.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>List of all clients or database error.</returns>
    public static Result<IReadOnlyList<SyncClient>, SyncError> GetAll(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT origin_id, last_sync_version, last_sync_timestamp, created_at
                FROM _sync_clients
                ORDER BY last_sync_version ASC
                """;

            var clients = new List<SyncClient>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                clients.Add(new SyncClient(
                    OriginId: reader.GetString(0),
                    LastSyncVersion: reader.GetInt64(1),
                    LastSyncTimestamp: reader.GetString(2),
                    CreatedAt: reader.GetString(3)
                ));
            }

            return new Result<IReadOnlyList<SyncClient>, SyncError>.Success(clients);
        }
        catch (SqliteException ex)
        {
            return new Result<IReadOnlyList<SyncClient>, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to get clients: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets a client by origin ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originId">The client's origin ID.</param>
    /// <returns>Client if found, null if not found, or database error.</returns>
    public static Result<SyncClient?, SyncError> GetByOrigin(
        SqliteConnection connection,
        string originId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT origin_id, last_sync_version, last_sync_timestamp, created_at
                FROM _sync_clients
                WHERE origin_id = @originId
                """;
            cmd.Parameters.AddWithValue("@originId", originId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Result<SyncClient?, SyncError>.Success(new SyncClient(
                    OriginId: reader.GetString(0),
                    LastSyncVersion: reader.GetInt64(1),
                    LastSyncTimestamp: reader.GetString(2),
                    CreatedAt: reader.GetString(3)
                ));
            }

            return new Result<SyncClient?, SyncError>.Success(null);
        }
        catch (SqliteException ex)
        {
            return new Result<SyncClient?, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to get client: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Inserts or updates a client record.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="client">The client to upsert.</param>
    /// <returns>Success or database error.</returns>
    public static Result<bool, SyncError> Upsert(SqliteConnection connection, SyncClient client)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO _sync_clients (origin_id, last_sync_version, last_sync_timestamp, created_at)
                VALUES (@originId, @lastVersion, @lastTimestamp, @createdAt)
                ON CONFLICT(origin_id) DO UPDATE SET
                    last_sync_version = @lastVersion,
                    last_sync_timestamp = @lastTimestamp
                """;
            cmd.Parameters.AddWithValue("@originId", client.OriginId);
            cmd.Parameters.AddWithValue("@lastVersion", client.LastSyncVersion);
            cmd.Parameters.AddWithValue("@lastTimestamp", client.LastSyncTimestamp);
            cmd.Parameters.AddWithValue("@createdAt", client.CreatedAt);

            cmd.ExecuteNonQuery();
            return new Result<bool, SyncError>.Success(true);
        }
        catch (SqliteException ex)
        {
            return new Result<bool, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to upsert client: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes a client by origin ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originId">The client's origin ID.</param>
    /// <returns>Success or database error.</returns>
    public static Result<bool, SyncError> Delete(SqliteConnection connection, string originId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM _sync_clients WHERE origin_id = @originId";
            cmd.Parameters.AddWithValue("@originId", originId);

            cmd.ExecuteNonQuery();
            return new Result<bool, SyncError>.Success(true);
        }
        catch (SqliteException ex)
        {
            return new Result<bool, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to delete client: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets the minimum sync version across all clients.
    /// This is the safe purge version for tombstones.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>Minimum version or 0 if no clients, or database error.</returns>
    public static Result<long, SyncError> GetMinVersion(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MIN(last_sync_version) FROM _sync_clients";

            var result = cmd.ExecuteScalar();
            var minVersion = result is long v ? v : 0;

            return new Result<long, SyncError>.Success(minVersion);
        }
        catch (SqliteException ex)
        {
            return new Result<long, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to get min version: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes stale clients (those inactive for too long).
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="originIds">List of origin IDs to delete.</param>
    /// <returns>Number of clients deleted or database error.</returns>
    public static Result<int, SyncError> DeleteStaleClients(
        SqliteConnection connection,
        IEnumerable<string> originIds)
    {
        try
        {
            var deleted = 0;
            foreach (var originId in originIds)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM _sync_clients WHERE origin_id = @originId";
                cmd.Parameters.AddWithValue("@originId", originId);
                deleted += cmd.ExecuteNonQuery();
            }

            return new Result<int, SyncError>.Success(deleted);
        }
        catch (SqliteException ex)
        {
            return new Result<int, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to delete stale clients: {ex.Message}")
            );
        }
    }
}
