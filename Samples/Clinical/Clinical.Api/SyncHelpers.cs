namespace Clinical.Api;

/// <summary>
/// Helper methods for sync operations.
/// </summary>
public static class SyncHelpers
{
    /// <summary>
    /// Converts a SyncError to a displayable error message.
    /// </summary>
    public static string ToMessage(SyncError error) =>
        error switch
        {
            SyncErrorDatabase db => db.Message,
            SyncErrorForeignKeyViolation fk => $"FK violation in {fk.TableName}: {fk.Details}",
            SyncErrorHashMismatch hash =>
                $"Hash mismatch: expected {hash.ExpectedHash}, got {hash.ActualHash}",
            SyncErrorFullResyncRequired resync =>
                $"Full resync required: client at {resync.ClientVersion}, oldest available {resync.OldestAvailableVersion}",
            SyncErrorDeferredChangeFailed deferred => $"Deferred change failed: {deferred.Reason}",
            SyncErrorUnresolvedConflict => "Unresolved conflict detected",
            _ => "Unknown sync error",
        };
}
