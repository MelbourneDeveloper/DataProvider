using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Sync.SQLite;

/// <summary>
/// SQLite repository for sync mapping state and record hashes.
/// Implements spec Section 7.5.2 tables.
/// </summary>
public static class MappingRepository
{
    /// <summary>
    /// Gets mapping state by ID.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <returns>Mapping state or null if not found.</returns>
    public static MappingStateResult GetMappingState(SqliteConnection connection, string mappingId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT mapping_id, last_synced_version, last_sync_timestamp, records_synced
                FROM _sync_mapping_state
                WHERE mapping_id = @mappingId
                """;
            cmd.Parameters.AddWithValue("@mappingId", mappingId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return new MappingStateOk(null);
            }

            return new MappingStateOk(
                new MappingStateEntry(
                    reader.GetString(0),
                    reader.GetInt64(1),
                    reader.GetString(2),
                    reader.GetInt64(3)
                )
            );
        }
        catch (SqliteException ex)
        {
            return new MappingStateError(
                new SyncErrorDatabase($"Failed to get mapping state: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets all mapping states.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <returns>List of mapping states.</returns>
    public static MappingStateListResult GetAllMappingStates(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT mapping_id, last_synced_version, last_sync_timestamp, records_synced
                FROM _sync_mapping_state
                ORDER BY mapping_id
                """;

            var states = new List<MappingStateEntry>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                states.Add(
                    new MappingStateEntry(
                        reader.GetString(0),
                        reader.GetInt64(1),
                        reader.GetString(2),
                        reader.GetInt64(3)
                    )
                );
            }

            return new MappingStateListOk(states);
        }
        catch (SqliteException ex)
        {
            return new MappingStateListError(
                new SyncErrorDatabase($"Failed to get mapping states: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Upserts mapping state.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="state">Mapping state to save.</param>
    /// <returns>Success or error.</returns>
    public static BoolSyncResult UpsertMappingState(
        SqliteConnection connection,
        MappingStateEntry state
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO _sync_mapping_state (mapping_id, last_synced_version, last_sync_timestamp, records_synced)
                VALUES (@mappingId, @version, @timestamp, @records)
                ON CONFLICT (mapping_id) DO UPDATE SET
                    last_synced_version = @version,
                    last_sync_timestamp = @timestamp,
                    records_synced = @records
                """;
            cmd.Parameters.AddWithValue("@mappingId", state.MappingId);
            cmd.Parameters.AddWithValue("@version", state.LastSyncedVersion);
            cmd.Parameters.AddWithValue("@timestamp", state.LastSyncTimestamp);
            cmd.Parameters.AddWithValue("@records", state.RecordsSynced);
            cmd.ExecuteNonQuery();

            return new BoolSyncOk(true);
        }
        catch (SqliteException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to upsert mapping state: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes mapping state.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <returns>Success or error.</returns>
    public static BoolSyncResult DeleteMappingState(SqliteConnection connection, string mappingId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM _sync_mapping_state WHERE mapping_id = @mappingId";
            cmd.Parameters.AddWithValue("@mappingId", mappingId);
            cmd.ExecuteNonQuery();

            return new BoolSyncOk(true);
        }
        catch (SqliteException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to delete mapping state: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Gets record hash by mapping ID and source PK.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <param name="sourcePk">Source primary key JSON.</param>
    /// <returns>Record hash or null if not found.</returns>
    public static RecordHashResult GetRecordHash(
        SqliteConnection connection,
        string mappingId,
        string sourcePk
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT mapping_id, source_pk, payload_hash, synced_at
                FROM _sync_record_hashes
                WHERE mapping_id = @mappingId AND source_pk = @sourcePk
                """;
            cmd.Parameters.AddWithValue("@mappingId", mappingId);
            cmd.Parameters.AddWithValue("@sourcePk", sourcePk);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return new RecordHashOk(null);
            }

            return new RecordHashOk(
                new RecordHashEntry(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)
                )
            );
        }
        catch (SqliteException ex)
        {
            return new RecordHashError(
                new SyncErrorDatabase($"Failed to get record hash: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Upserts record hash.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="hash">Record hash to save.</param>
    /// <returns>Success or error.</returns>
    public static BoolSyncResult UpsertRecordHash(SqliteConnection connection, RecordHashEntry hash)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO _sync_record_hashes (mapping_id, source_pk, payload_hash, synced_at)
                VALUES (@mappingId, @sourcePk, @hash, @syncedAt)
                ON CONFLICT (mapping_id, source_pk) DO UPDATE SET
                    payload_hash = @hash,
                    synced_at = @syncedAt
                """;
            cmd.Parameters.AddWithValue("@mappingId", hash.MappingId);
            cmd.Parameters.AddWithValue("@sourcePk", hash.SourcePk);
            cmd.Parameters.AddWithValue("@hash", hash.PayloadHash);
            cmd.Parameters.AddWithValue("@syncedAt", hash.SyncedAt);
            cmd.ExecuteNonQuery();

            return new BoolSyncOk(true);
        }
        catch (SqliteException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to upsert record hash: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes record hash.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <param name="sourcePk">Source primary key JSON.</param>
    /// <returns>Success or error.</returns>
    public static BoolSyncResult DeleteRecordHash(
        SqliteConnection connection,
        string mappingId,
        string sourcePk
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                DELETE FROM _sync_record_hashes
                WHERE mapping_id = @mappingId AND source_pk = @sourcePk
                """;
            cmd.Parameters.AddWithValue("@mappingId", mappingId);
            cmd.Parameters.AddWithValue("@sourcePk", sourcePk);
            cmd.ExecuteNonQuery();

            return new BoolSyncOk(true);
        }
        catch (SqliteException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to delete record hash: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Deletes all record hashes for a mapping.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <returns>Number of deleted hashes or error.</returns>
    public static IntSyncResult DeleteRecordHashesByMapping(
        SqliteConnection connection,
        string mappingId
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM _sync_record_hashes WHERE mapping_id = @mappingId";
            cmd.Parameters.AddWithValue("@mappingId", mappingId);
            var count = cmd.ExecuteNonQuery();

            return new IntSyncOk(count);
        }
        catch (SqliteException ex)
        {
            return new IntSyncError(
                new SyncErrorDatabase($"Failed to delete record hashes: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Counts record hashes for a mapping.
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <returns>Count or error.</returns>
    public static LongSyncResult CountRecordHashes(SqliteConnection connection, string mappingId)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM _sync_record_hashes WHERE mapping_id = @mappingId";
            cmd.Parameters.AddWithValue("@mappingId", mappingId);
            var count = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);

            return new LongSyncOk(count);
        }
        catch (SqliteException ex)
        {
            return new LongSyncError(
                new SyncErrorDatabase($"Failed to count record hashes: {ex.Message}")
            );
        }
    }
}
