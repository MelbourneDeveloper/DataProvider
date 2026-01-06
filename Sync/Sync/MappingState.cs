namespace Sync;

/// <summary>
/// Per-mapping sync state for version-based tracking.
/// Stored in _sync_mapping_state table per spec Section 7.5.2.
/// </summary>
/// <param name="MappingId">Unique mapping identifier.</param>
/// <param name="LastSyncedVersion">Last _sync_log version synced for this mapping.</param>
/// <param name="LastSyncTimestamp">ISO 8601 timestamp of last sync.</param>
/// <param name="RecordsSynced">Total records synced for this mapping.</param>
public sealed record MappingStateEntry(
    string MappingId,
    long LastSyncedVersion,
    string LastSyncTimestamp,
    long RecordsSynced
)
{
    /// <summary>
    /// Creates initial state for a new mapping.
    /// </summary>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <param name="timestamp">Current timestamp.</param>
    /// <returns>Initial mapping state.</returns>
    public static MappingStateEntry Initial(string mappingId, string timestamp) =>
        new(mappingId, 0, timestamp, 0);
}

/// <summary>
/// Per-record hash entry for hash-based sync tracking.
/// Stored in _sync_record_hashes table per spec Section 7.5.2.
/// </summary>
/// <param name="MappingId">Mapping identifier.</param>
/// <param name="SourcePk">JSON-serialized source primary key.</param>
/// <param name="PayloadHash">SHA-256 hash of canonical JSON payload.</param>
/// <param name="SyncedAt">ISO 8601 timestamp when synced.</param>
public sealed record RecordHashEntry(
    string MappingId,
    string SourcePk,
    string PayloadHash,
    string SyncedAt
);

/// <summary>
/// Static methods for managing mapping state.
/// Implements spec Section 7.5.3 - Sync Decision Logic.
/// </summary>
internal static class MappingStateManager
{
    /// <summary>
    /// Determines if a change should be synced based on version tracking.
    /// </summary>
    /// <param name="changeVersion">Version of the change.</param>
    /// <param name="state">Current mapping state.</param>
    /// <returns>True if change should be synced.</returns>
    public static bool ShouldSyncByVersion(long changeVersion, MappingStateEntry state) =>
        changeVersion > state.LastSyncedVersion;

    /// <summary>
    /// Determines if a change should be synced based on hash tracking.
    /// </summary>
    /// <param name="currentHash">Hash of current payload.</param>
    /// <param name="storedHash">Previously stored hash (null if not synced).</param>
    /// <returns>True if change should be synced.</returns>
    public static bool ShouldSyncByHash(string currentHash, string? storedHash) =>
        storedHash is null || !currentHash.Equals(storedHash, StringComparison.Ordinal);

    /// <summary>
    /// Determines if a change should be synced based on timestamp tracking.
    /// </summary>
    /// <param name="changeTimestamp">Timestamp of the change.</param>
    /// <param name="lastSyncedAt">Last sync timestamp for the record (null if not synced).</param>
    /// <returns>True if change should be synced.</returns>
    public static bool ShouldSyncByTimestamp(string changeTimestamp, string? lastSyncedAt) =>
        lastSyncedAt is null
        || string.Compare(changeTimestamp, lastSyncedAt, StringComparison.Ordinal) > 0;

    /// <summary>
    /// Updates mapping state after successful sync.
    /// </summary>
    /// <param name="state">Current state.</param>
    /// <param name="toVersion">Version synced to.</param>
    /// <param name="recordCount">Number of records synced.</param>
    /// <param name="timestamp">Current timestamp.</param>
    /// <returns>Updated state.</returns>
    public static MappingStateEntry UpdateAfterSync(
        MappingStateEntry state,
        long toVersion,
        long recordCount,
        string timestamp
    ) =>
        state with
        {
            LastSyncedVersion = toVersion,
            LastSyncTimestamp = timestamp,
            RecordsSynced = state.RecordsSynced + recordCount,
        };
}
