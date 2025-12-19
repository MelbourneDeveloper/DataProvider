namespace Clinical.Api;

/// <summary>
/// Helper methods for sync operations.
/// </summary>
public static class SyncHelpers
{
    /// <summary>
    /// Converts a SyncError to a displayable error message.
    /// </summary>
    public static string ToMessage(Sync.SyncError error) =>
        error switch
        {
            Sync.SyncErrorDatabase db => db.Message,
            Sync.SyncErrorForeignKeyViolation fk => $"FK violation in {fk.TableName}: {fk.Details}",
            Sync.SyncErrorHashMismatch hash =>
                $"Hash mismatch: expected {hash.ExpectedHash}, got {hash.ActualHash}",
            Sync.SyncErrorFullResyncRequired resync =>
                $"Full resync required: client at {resync.ClientVersion}, oldest available {resync.OldestAvailableVersion}",
            Sync.SyncErrorDeferredChangeFailed deferred =>
                $"Deferred change failed: {deferred.Reason}",
            Sync.SyncErrorUnresolvedConflict conflict => "Unresolved conflict detected",
            _ => "Unknown sync error",
        };
}
