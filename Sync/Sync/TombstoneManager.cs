namespace Sync;

/// <summary>
/// Manages tombstone retention and purging.
/// Implements spec Section 13 (Tombstone Retention).
/// </summary>
public static class TombstoneManager
{
    /// <summary>
    /// Default maximum age for inactive clients before removal.
    /// </summary>
    public static readonly TimeSpan DefaultClientInactivityLimit = TimeSpan.FromDays(90);

    /// <summary>
    /// Calculates the safe purge version - tombstones below this can be deleted.
    /// This is the minimum version that ALL known clients have synced past.
    /// </summary>
    /// <param name="clients">All tracked client sync states.</param>
    /// <returns>Safe purge version, or 0 if no clients.</returns>
    public static long CalculateSafePurgeVersion(IEnumerable<SyncClient> clients)
    {
        var clientList = clients.ToList();
        return clientList.Count == 0 ? 0 : clientList.Min(c => c.LastSyncVersion);
    }

    /// <summary>
    /// Determines if a client requires full resync (missed tombstones).
    /// </summary>
    /// <param name="clientLastVersion">Client's last synced version.</param>
    /// <param name="oldestAvailableVersion">Oldest version still in sync log.</param>
    /// <returns>True if client needs full resync.</returns>
    public static bool RequiresFullResync(long clientLastVersion, long oldestAvailableVersion) =>
        clientLastVersion < oldestAvailableVersion;

    /// <summary>
    /// Identifies stale clients that should be removed from tracking.
    /// </summary>
    /// <param name="clients">All tracked clients.</param>
    /// <param name="now">Current UTC timestamp.</param>
    /// <param name="inactivityLimit">Max allowed inactivity period.</param>
    /// <returns>List of client origin IDs to remove.</returns>
    public static IReadOnlyList<string> FindStaleClients(
        IEnumerable<SyncClient> clients,
        DateTime now,
        TimeSpan inactivityLimit
    )
    {
        var stale = new List<string>();

        foreach (var client in clients)
        {
            if (DateTime.TryParse(client.LastSyncTimestamp, out var lastSync))
            {
                if (now - lastSync > inactivityLimit)
                {
                    stale.Add(client.OriginId);
                }
            }
        }

        return stale;
    }

    /// <summary>
    /// Purges tombstones that all clients have synced past.
    /// </summary>
    /// <param name="clients">All tracked clients.</param>
    /// <param name="purgeTombstones">Function to delete tombstones below version.</param>
    /// <returns>Number of tombstones purged or error.</returns>
    public static IntSyncResult PurgeTombstones(
        IEnumerable<SyncClient> clients,
        Func<long, IntSyncResult> purgeTombstones
    )
    {
        var safeVersion = CalculateSafePurgeVersion(clients);

        if (safeVersion <= 0)
        {
            return new IntSyncOk(0);
        }

        return purgeTombstones(safeVersion);
    }

    /// <summary>
    /// Updates or creates a client tracking record after successful sync.
    /// </summary>
    /// <param name="originId">Client origin ID.</param>
    /// <param name="syncedToVersion">Version client synced to.</param>
    /// <param name="timestamp">Current UTC timestamp.</param>
    /// <param name="existingClient">Existing client record if any.</param>
    /// <returns>Updated client record.</returns>
    public static SyncClient UpdateClientSyncState(
        string originId,
        long syncedToVersion,
        string timestamp,
        SyncClient? existingClient
    ) =>
        existingClient is null
            ? new SyncClient(originId, syncedToVersion, timestamp, timestamp)
            : existingClient with
            {
                LastSyncVersion = syncedToVersion,
                LastSyncTimestamp = timestamp,
            };

    /// <summary>
    /// Creates a full resync error for a client that has fallen behind.
    /// </summary>
    /// <param name="clientVersion">Client's last synced version.</param>
    /// <param name="oldestVersion">Oldest available version in log.</param>
    /// <returns>FullResyncRequired error.</returns>
    public static SyncErrorFullResyncRequired CreateFullResyncError(
        long clientVersion,
        long oldestVersion
    ) => new(clientVersion, oldestVersion);
}
