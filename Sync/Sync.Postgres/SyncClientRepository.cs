using System.Globalization;
using Npgsql;

namespace Sync.Postgres;

/// <summary>
/// Repository for sync client tracking in PostgreSQL.
/// Implements spec Section 13.3 for tombstone retention.
/// </summary>
public static class SyncClientRepository
{
    /// <summary>
    /// Gets all registered sync clients.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="logger">Logger for repository operations.</param>
    /// <returns>List of sync clients or database error.</returns>
    public static SyncClientListResult GetAll(NpgsqlConnection connection, ILogger logger)
    {
        logger.LogDebug("POSTGRES CLIENTS: Getting all clients");

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT origin_id, last_sync_version, last_sync_timestamp, created_at
                FROM _sync_clients
                ORDER BY origin_id
                """;

            var clients = new List<SyncClient>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                clients.Add(
                    new SyncClient(
                        OriginId: reader.GetString(0),
                        LastSyncVersion: reader.GetInt64(1),
                        LastSyncTimestamp: reader.GetString(2),
                        CreatedAt: reader.GetString(3)
                    )
                );
            }

            logger.LogDebug("POSTGRES CLIENTS: Found {Count} clients", clients.Count);
            return new SyncClientListOk(clients);
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "POSTGRES CLIENTS: Failed to get clients");
            return new SyncClientListError(
                new SyncErrorDatabase($"Failed to get sync clients: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets a sync client by origin ID.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="originId">Client origin ID.</param>
    /// <param name="logger">Logger for repository operations.</param>
    /// <returns>Sync client or null if not found.</returns>
    public static SyncClientResult GetByOriginId(
        NpgsqlConnection connection,
        string originId,
        ILogger logger
    )
    {
        logger.LogDebug("POSTGRES CLIENTS: Getting client {OriginId}", originId);

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
                var client = new SyncClient(
                    OriginId: reader.GetString(0),
                    LastSyncVersion: reader.GetInt64(1),
                    LastSyncTimestamp: reader.GetString(2),
                    CreatedAt: reader.GetString(3)
                );
                logger.LogDebug("POSTGRES CLIENTS: Found client {OriginId}", originId);
                return new SyncClientOk(client);
            }

            logger.LogDebug("POSTGRES CLIENTS: Client {OriginId} not found", originId);
            return new SyncClientOk(null);
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "POSTGRES CLIENTS: Failed to get client {OriginId}", originId);
            return new SyncClientError(
                new SyncErrorDatabase($"Failed to get sync client: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Upserts a sync client record.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="client">Client to upsert.</param>
    /// <param name="logger">Logger for repository operations.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Upsert(
        NpgsqlConnection connection,
        SyncClient client,
        ILogger logger
    )
    {
        logger.LogDebug(
            "POSTGRES CLIENTS: Upserting client {OriginId} at version {Version}",
            client.OriginId,
            client.LastSyncVersion
        );

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO _sync_clients (origin_id, last_sync_version, last_sync_timestamp, created_at)
                VALUES (@originId, @version, @timestamp, @createdAt)
                ON CONFLICT (origin_id) DO UPDATE SET
                    last_sync_version = @version,
                    last_sync_timestamp = @timestamp
                """;
            cmd.Parameters.AddWithValue("@originId", client.OriginId);
            cmd.Parameters.AddWithValue("@version", client.LastSyncVersion);
            cmd.Parameters.AddWithValue("@timestamp", client.LastSyncTimestamp);
            cmd.Parameters.AddWithValue("@createdAt", client.CreatedAt);
            cmd.ExecuteNonQuery();

            logger.LogInformation(
                "POSTGRES CLIENTS: Upserted client {OriginId} at version {Version}",
                client.OriginId,
                client.LastSyncVersion
            );
            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "POSTGRES CLIENTS: Failed to upsert client");
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to upsert sync client: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes a sync client record.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="originId">Client origin ID to delete.</param>
    /// <param name="logger">Logger for repository operations.</param>
    /// <returns>Success or database error.</returns>
    public static BoolSyncResult Delete(
        NpgsqlConnection connection,
        string originId,
        ILogger logger
    )
    {
        logger.LogDebug("POSTGRES CLIENTS: Deleting client {OriginId}", originId);

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM _sync_clients WHERE origin_id = @originId";
            cmd.Parameters.AddWithValue("@originId", originId);
            cmd.ExecuteNonQuery();

            logger.LogInformation("POSTGRES CLIENTS: Deleted client {OriginId}", originId);
            return new BoolSyncOk(true);
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "POSTGRES CLIENTS: Failed to delete client");
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to delete sync client: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets the minimum last sync version across all clients.
    /// Used to determine safe tombstone purge version.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="logger">Logger for repository operations.</param>
    /// <returns>Minimum version or 0 if no clients.</returns>
    public static LongSyncResult GetMinLastSyncVersion(NpgsqlConnection connection, ILogger logger)
    {
        logger.LogDebug("POSTGRES CLIENTS: Getting min last sync version");

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(MIN(last_sync_version), 0) FROM _sync_clients";
            var result = cmd.ExecuteScalar();
            var version = result is long v ? v : 0;

            logger.LogDebug("POSTGRES CLIENTS: Min last sync version = {Version}", version);
            return new LongSyncOk(version);
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "POSTGRES CLIENTS: Failed to get min last sync version");
            return new LongSyncError(
                new SyncErrorDatabase($"Failed to get min last sync version: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Finds clients that are stale (haven't synced in the given number of days).
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="staleDays">Number of days to consider stale.</param>
    /// <param name="logger">Logger for repository operations.</param>
    /// <returns>List of stale clients.</returns>
    public static SyncClientListResult FindStaleClients(
        NpgsqlConnection connection,
        int staleDays,
        ILogger logger
    )
    {
        logger.LogDebug("POSTGRES CLIENTS: Finding clients stale for {Days} days", staleDays);

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-staleDays).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT origin_id, last_sync_version, last_sync_timestamp, created_at
                FROM _sync_clients
                WHERE last_sync_timestamp < @cutoff
                ORDER BY last_sync_timestamp
                """;
            cmd.Parameters.AddWithValue("@cutoff", cutoffDate);

            var clients = new List<SyncClient>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                clients.Add(
                    new SyncClient(
                        OriginId: reader.GetString(0),
                        LastSyncVersion: reader.GetInt64(1),
                        LastSyncTimestamp: reader.GetString(2),
                        CreatedAt: reader.GetString(3)
                    )
                );
            }

            logger.LogInformation("POSTGRES CLIENTS: Found {Count} stale clients", clients.Count);
            return new SyncClientListOk(clients);
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "POSTGRES CLIENTS: Failed to find stale clients");
            return new SyncClientListError(
                new SyncErrorDatabase($"Failed to find stale clients: {ex.Message}")
            );
        }
    }
}
