namespace Sync;

/// <summary>
/// Represents the sync state for a replica. Maps to _sync_state table (Appendix A).
/// </summary>
/// <param name="OriginId">UUID v4 identifying this replica. Generated once, never changes.</param>
/// <param name="LastServerVersion">Last version successfully pulled from server.</param>
/// <param name="LastPushVersion">Last local version successfully pushed to server.</param>
public sealed record SyncState(string OriginId, long LastServerVersion, long LastPushVersion);

/// <summary>
/// Represents the ephemeral sync session state (_sync_session table).
/// </summary>
/// <param name="SyncActive">When true (1), triggers are suppressed during change application.</param>
public sealed record SyncSession(bool SyncActive);

/// <summary>
/// Represents a client tracked by the server for tombstone retention (_sync_clients).
/// Maps to spec Section 13.3.
/// </summary>
/// <param name="OriginId">UUID of the client replica.</param>
/// <param name="LastSyncVersion">Last version this client has synced to.</param>
/// <param name="LastSyncTimestamp">ISO 8601 UTC timestamp of last sync.</param>
/// <param name="CreatedAt">ISO 8601 UTC timestamp when client was first registered.</param>
public sealed record SyncClient(
    string OriginId,
    long LastSyncVersion,
    string LastSyncTimestamp,
    string CreatedAt
);
