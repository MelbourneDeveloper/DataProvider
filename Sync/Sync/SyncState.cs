namespace Sync;

/// <summary>
/// Represents the sync state for a replica. Maps to _sync_state table (Appendix A).
/// </summary>
/// <param name="OriginId">UUID v4 identifying this replica. Generated once, never changes.</param>
/// <param name="LastServerVersion">Last version successfully pulled from server.</param>
/// <param name="LastPushVersion">Last local version successfully pushed to server.</param>
/// <example>
/// <code>
/// // Initialize sync state for a new client
/// var state = new SyncState(
///     OriginId: Guid.NewGuid().ToString(),
///     LastServerVersion: 0,
///     LastPushVersion: 0
/// );
///
/// // After pulling changes from server
/// var updatedState = state with { LastServerVersion = 142 };
///
/// // After pushing changes to server
/// var finalState = updatedState with { LastPushVersion = 65 };
/// </code>
/// </example>
public sealed record SyncState(string OriginId, long LastServerVersion, long LastPushVersion);

/// <summary>
/// Represents the ephemeral sync session state (_sync_session table).
/// </summary>
/// <param name="SyncActive">When true (1), triggers are suppressed during change application.</param>
/// <example>
/// <code>
/// // Enable trigger suppression during sync
/// var session = new SyncSession(SyncActive: true);
/// await syncSessionRepository.SetAsync(session);
///
/// // Apply remote changes without triggering local change logging
/// foreach (var change in remoteChanges)
/// {
///     await ApplyChange(change);
/// }
///
/// // Disable trigger suppression
/// var inactive = new SyncSession(SyncActive: false);
/// await syncSessionRepository.SetAsync(inactive);
/// </code>
/// </example>
public sealed record SyncSession(bool SyncActive);

/// <summary>
/// Represents a client tracked by the server for tombstone retention (_sync_clients).
/// Maps to spec Section 13.3.
/// </summary>
/// <param name="OriginId">UUID of the client replica.</param>
/// <param name="LastSyncVersion">Last version this client has synced to.</param>
/// <param name="LastSyncTimestamp">ISO 8601 UTC timestamp of last sync.</param>
/// <param name="CreatedAt">ISO 8601 UTC timestamp when client was first registered.</param>
/// <example>
/// <code>
/// // Register a new sync client
/// var client = new SyncClient(
///     OriginId: Guid.NewGuid().ToString(),
///     LastSyncVersion: 0,
///     LastSyncTimestamp: DateTime.UtcNow.ToString("o"),
///     CreatedAt: DateTime.UtcNow.ToString("o")
/// );
///
/// // Store in repository
/// await syncClientRepository.UpsertAsync(client);
/// </code>
/// </example>
public sealed record SyncClient(
    string OriginId,
    long LastSyncVersion,
    string LastSyncTimestamp,
    string CreatedAt
);
