namespace Sync;

/// <summary>
/// Applies changes to the local database with FK violation defer/retry.
/// Implements spec Section 11 (Bi-Directional Sync Protocol).
/// </summary>
public static class ChangeApplier
{
    /// <summary>
    /// Applies a batch of changes with FK violation handling.
    /// Changes are applied in version order. FK violations are deferred and retried.
    /// </summary>
    /// <param name="batch">The batch of changes to apply.</param>
    /// <param name="myOriginId">This replica's origin ID (to skip own changes).</param>
    /// <param name="maxRetryPasses">Maximum retry passes for deferred changes.</param>
    /// <param name="applyChange">Function to apply a single change. Returns true on success, false on FK violation.</param>
    /// <returns>Result of applying the batch.</returns>
    public static BatchApplyResultResult ApplyBatch(
        SyncBatch batch,
        string myOriginId,
        int maxRetryPasses,
        Func<SyncLogEntry, BoolSyncResult> applyChange
    )
    {
        var deferred = new List<SyncLogEntry>();
        var appliedCount = 0;

        // First pass: apply all changes, deferring FK violations
        foreach (var entry in batch.Changes)
        {
            // Echo prevention: skip changes from self
            if (entry.Origin == myOriginId)
            {
                continue;
            }

            var result = applyChange(entry);

            switch (result)
            {
                case BoolSyncOk s when s.Value:
                    appliedCount++;
                    break;
                case BoolSyncOk s when !s.Value:
                    // FK violation - defer for retry
                    deferred.Add(entry);
                    break;
                case BoolSyncError f:
                    return new BatchApplyResultError(f.Value);
            }
        }

        // Retry passes for deferred changes
        for (var pass = 0; pass < maxRetryPasses && deferred.Count > 0; pass++)
        {
            var stillDeferred = new List<SyncLogEntry>();

            foreach (var entry in deferred)
            {
                var result = applyChange(entry);

                switch (result)
                {
                    case BoolSyncOk s when s.Value:
                        appliedCount++;
                        break;
                    case BoolSyncOk s when !s.Value:
                        stillDeferred.Add(entry);
                        break;
                    case BoolSyncError f:
                        return new BatchApplyResultError(f.Value);
                }
            }

            deferred = stillDeferred;
        }

        // If any changes still deferred after all retries, fail
        if (deferred.Count > 0)
        {
            return new BatchApplyResultError(
                new SyncErrorDeferredChangeFailed(
                    deferred[0],
                    $"{deferred.Count} changes could not be applied after {maxRetryPasses} retry passes"
                )
            );
        }

        return new BatchApplyResultOk(new BatchApplyResult(appliedCount, 0, batch.ToVersion));
    }

    /// <summary>
    /// Determines if an exception represents an FK constraint violation.
    /// Platform-specific implementations should override detection logic.
    /// </summary>
    /// <param name="message">Exception message to check.</param>
    /// <returns>True if this is an FK violation.</returns>
    public static bool IsForeignKeyViolation(string message) =>
        message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
        || message.Contains("FK_", StringComparison.OrdinalIgnoreCase)
        || message.Contains("foreign key constraint", StringComparison.OrdinalIgnoreCase);
}
