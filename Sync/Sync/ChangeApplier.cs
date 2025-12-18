using Microsoft.Extensions.Logging;

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
    /// <param name="logger">Logger for change application.</param>
    /// <returns>Result of applying the batch.</returns>
    public static BatchApplyResultResult ApplyBatch(
        SyncBatch batch,
        string myOriginId,
        int maxRetryPasses,
        Func<SyncLogEntry, BoolSyncResult> applyChange,
        ILogger logger
    )
    {
        logger.LogInformation(
            "APPLY: Starting batch apply, {Count} changes, versions {From}-{To}, myOrigin={OriginId}",
            batch.Changes.Count,
            batch.FromVersion,
            batch.ToVersion,
            myOriginId
        );

        var deferred = new List<SyncLogEntry>();
        var appliedCount = 0;
        var skippedCount = 0;

        // First pass: apply all changes, deferring FK violations
        logger.LogDebug("APPLY: First pass - applying {Count} changes", batch.Changes.Count);

        foreach (var entry in batch.Changes)
        {
            // Echo prevention: skip changes from self
            if (entry.Origin == myOriginId)
            {
                skippedCount++;
                logger.LogTrace(
                    "APPLY: Skipping own change - table={Table}, pk={PK}, op={Op}, version={Version}",
                    entry.TableName,
                    entry.PkValue,
                    entry.Operation,
                    entry.Version
                );
                continue;
            }

            logger.LogTrace(
                "APPLY: Applying change - table={Table}, pk={PK}, op={Op}, version={Version}, origin={Origin}",
                entry.TableName,
                entry.PkValue,
                entry.Operation,
                entry.Version,
                entry.Origin
            );

            var result = applyChange(entry);

            switch (result)
            {
                case BoolSyncOk s when s.Value:
                    appliedCount++;
                    logger.LogTrace("APPLY: Change applied successfully");
                    break;
                case BoolSyncOk s when !s.Value:
                    // FK violation - defer for retry
                    deferred.Add(entry);
                    logger.LogDebug(
                        "APPLY: FK violation, deferring - table={Table}, pk={PK}, version={Version}",
                        entry.TableName,
                        entry.PkValue,
                        entry.Version
                    );
                    break;
                case BoolSyncError f:
                    logger.LogError(
                        "APPLY: Change failed - table={Table}, pk={PK}, version={Version}, error={Error}",
                        entry.TableName,
                        entry.PkValue,
                        entry.Version,
                        f.Value
                    );
                    return new BatchApplyResultError(f.Value);
            }
        }

        logger.LogDebug(
            "APPLY: First pass complete - applied={Applied}, skipped={Skipped}, deferred={Deferred}",
            appliedCount,
            skippedCount,
            deferred.Count
        );

        // Retry passes for deferred changes
        for (var pass = 0; pass < maxRetryPasses && deferred.Count > 0; pass++)
        {
            logger.LogDebug(
                "APPLY: Retry pass {Pass}/{MaxPasses}, {Count} deferred changes",
                pass + 1,
                maxRetryPasses,
                deferred.Count
            );

            var stillDeferred = new List<SyncLogEntry>();

            foreach (var entry in deferred)
            {
                logger.LogTrace(
                    "APPLY: Retrying - table={Table}, pk={PK}, version={Version}",
                    entry.TableName,
                    entry.PkValue,
                    entry.Version
                );

                var result = applyChange(entry);

                switch (result)
                {
                    case BoolSyncOk s when s.Value:
                        appliedCount++;
                        logger.LogTrace("APPLY: Deferred change applied successfully");
                        break;
                    case BoolSyncOk s when !s.Value:
                        stillDeferred.Add(entry);
                        logger.LogTrace("APPLY: Still FK violation, deferring again");
                        break;
                    case BoolSyncError f:
                        logger.LogError(
                            "APPLY: Deferred change failed - table={Table}, pk={PK}, error={Error}",
                            entry.TableName,
                            entry.PkValue,
                            f.Value
                        );
                        return new BatchApplyResultError(f.Value);
                }
            }

            logger.LogDebug(
                "APPLY: Retry pass {Pass} complete - resolved={Resolved}, stillDeferred={StillDeferred}",
                pass + 1,
                deferred.Count - stillDeferred.Count,
                stillDeferred.Count
            );

            deferred = stillDeferred;
        }

        // If any changes still deferred after all retries, fail
        if (deferred.Count > 0)
        {
            logger.LogError(
                "APPLY: FAILED - {Count} changes could not be applied after {MaxPasses} retry passes. First failed: table={Table}, pk={PK}",
                deferred.Count,
                maxRetryPasses,
                deferred[0].TableName,
                deferred[0].PkValue
            );

            return new BatchApplyResultError(
                new SyncErrorDeferredChangeFailed(
                    deferred[0],
                    $"{deferred.Count} changes could not be applied after {maxRetryPasses} retry passes"
                )
            );
        }

        logger.LogInformation(
            "APPLY: Batch complete - applied={Applied}, skipped={Skipped}, toVersion={ToVersion}",
            appliedCount,
            skippedCount,
            batch.ToVersion
        );

        return new BatchApplyResultOk(new BatchApplyResult(appliedCount, skippedCount, batch.ToVersion));
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
