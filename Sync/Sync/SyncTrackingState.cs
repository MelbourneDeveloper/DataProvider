using System.Security.Cryptography;
using System.Text;

namespace Sync;

/// <summary>
/// Per-mapping sync state. Spec Section 7.5.2 - _sync_mapping_state table.
/// </summary>
/// <param name="MappingId">Mapping identifier.</param>
/// <param name="LastSyncedVersion">Last _sync_log version synced for this mapping.</param>
/// <param name="LastSyncTimestamp">ISO 8601 UTC timestamp of last sync.</param>
/// <param name="RecordsSynced">Total records synced via this mapping.</param>
public sealed record MappingSyncState(
    string MappingId,
    long LastSyncedVersion,
    string LastSyncTimestamp,
    long RecordsSynced
);

/// <summary>
/// Per-record hash for hash-based tracking. Spec Section 7.5.2 - _sync_record_hashes table.
/// </summary>
/// <param name="MappingId">Mapping identifier.</param>
/// <param name="SourcePk">JSON pk_value from source table.</param>
/// <param name="PayloadHash">SHA-256 hash of canonical JSON payload.</param>
/// <param name="SyncedAt">ISO 8601 UTC timestamp when synced.</param>
public sealed record RecordHash(
    string MappingId,
    string SourcePk,
    string PayloadHash,
    string SyncedAt
);

/// <summary>
/// Manages sync tracking state per spec Section 7.5.
/// Determines whether records should be synced based on tracking strategy.
/// </summary>
internal static class SyncTrackingManager
{
    /// <summary>
    /// Determines if an entry should be synced based on tracking state.
    /// Per spec Section 7.5.3 - Sync Decision Logic.
    /// </summary>
    /// <param name="entry">Sync log entry.</param>
    /// <param name="mapping">Table mapping with tracking config.</param>
    /// <param name="getMappingState">Function to get mapping state.</param>
    /// <param name="getRecordHash">Function to get record hash.</param>
    /// <returns>True if entry should be synced.</returns>
    public static bool ShouldSync(
        SyncLogEntry entry,
        TableMapping mapping,
        Func<string, MappingSyncState?> getMappingState,
        Func<string, string, RecordHash?> getRecordHash
    )
    {
        if (!mapping.SyncTracking.Enabled)
        {
            return true;
        }

        return mapping.SyncTracking.Strategy switch
        {
            SyncTrackingStrategy.Version => ShouldSyncByVersion(entry, mapping.Id, getMappingState),
            SyncTrackingStrategy.Hash => ShouldSyncByHash(entry, mapping.Id, getRecordHash),
            SyncTrackingStrategy.Timestamp => true, // Timestamp strategy handled at query time
            SyncTrackingStrategy.External => true, // External tracking is app responsibility
            _ => true,
        };
    }

    /// <summary>
    /// Checks if entry should sync based on version tracking.
    /// </summary>
    private static bool ShouldSyncByVersion(
        SyncLogEntry entry,
        string mappingId,
        Func<string, MappingSyncState?> getMappingState
    )
    {
        var state = getMappingState(mappingId);
        return state is null || entry.Version > state.LastSyncedVersion;
    }

    /// <summary>
    /// Checks if entry should sync based on payload hash.
    /// </summary>
    private static bool ShouldSyncByHash(
        SyncLogEntry entry,
        string mappingId,
        Func<string, string, RecordHash?> getRecordHash
    )
    {
        if (entry.Payload is null)
        {
            return true; // Always sync deletes
        }

        var existingHash = getRecordHash(mappingId, entry.PkValue);
        if (existingHash is null)
        {
            return true; // Never synced before
        }

        var currentHash = ComputePayloadHash(entry.Payload);
        return currentHash != existingHash.PayloadHash;
    }

    /// <summary>
    /// Computes SHA-256 hash of canonical JSON payload.
    /// Per spec Section 16.2 - Canonical JSON.
    /// </summary>
    /// <param name="payload">JSON payload to hash.</param>
    /// <returns>Hex-encoded SHA-256 hash.</returns>
    public static string ComputePayloadHash(string payload)
    {
        var canonical = CanonicalizeJson(payload);
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Canonicalizes JSON for hashing: sorted keys, no whitespace.
    /// Per spec Section 16.2.
    /// </summary>
    private static string CanonicalizeJson(string json)
    {
        var dict = HashVerifier.ParseJson(json);
        return HashVerifier.ToCanonicalJson(dict);
    }

    /// <summary>
    /// Creates updated mapping state after successful sync.
    /// </summary>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <param name="version">Latest synced version.</param>
    /// <param name="recordCount">Number of records synced.</param>
    /// <param name="existingState">Existing state to update.</param>
    /// <returns>Updated mapping state.</returns>
    public static MappingSyncState UpdateMappingState(
        string mappingId,
        long version,
        int recordCount,
        MappingSyncState? existingState
    ) =>
        new(
            MappingId: mappingId,
            LastSyncedVersion: version,
            LastSyncTimestamp: DateTime.UtcNow.ToString("O"),
            RecordsSynced: (existingState?.RecordsSynced ?? 0) + recordCount
        );

    /// <summary>
    /// Creates record hash for hash-based tracking.
    /// </summary>
    /// <param name="mappingId">Mapping identifier.</param>
    /// <param name="sourcePk">Source primary key JSON.</param>
    /// <param name="payload">Payload to hash.</param>
    /// <returns>Record hash entry.</returns>
    public static RecordHash CreateRecordHash(string mappingId, string sourcePk, string payload) =>
        new(
            MappingId: mappingId,
            SourcePk: sourcePk,
            PayloadHash: ComputePayloadHash(payload),
            SyncedAt: DateTime.UtcNow.ToString("O")
        );
}
