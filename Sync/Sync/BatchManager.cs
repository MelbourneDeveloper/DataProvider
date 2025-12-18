#pragma warning disable CS8509 // Exhaustive switch - Exhaustion analyzer handles this

namespace Sync;

/// <summary>
/// Manages batch fetching and processing for sync operations.
/// Implements spec Section 12 (Batching).
/// </summary>
public static class BatchManager
{
    /// <summary>
    /// Fetches a batch of changes from the sync log.
    /// </summary>
    public static SyncBatchResult FetchBatch(
        long fromVersion,
        int batchSize,
        Func<long, int, SyncLogListResult> fetchChanges
    )
    {
        var fetchResult = fetchChanges(fromVersion, batchSize + 1);

        return fetchResult switch
        {
            SyncLogListOk(var changes) => CreateBatchFromChanges(changes, fromVersion, batchSize),
            SyncLogListError(var error) => new SyncBatchError(error),
        };
    }

    /// <summary>
    /// Processes all batches in a pull phase until no more changes are available.
    /// </summary>
    public static IntSyncResult ProcessAllBatches(
        long startVersion,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchChanges,
        Func<SyncBatch, BatchApplyResultResult> applyBatch,
        Action<long> updateVersion
    )
    {
        var currentVersion = startVersion;
        var totalApplied = 0;

        while (true)
        {
            var batchResult = FetchBatch(currentVersion, config.BatchSize, fetchChanges);

            switch (batchResult)
            {
                case SyncBatchOk(var batch):
                    if (batch.Changes.Count == 0)
                    {
                        return new IntSyncOk(totalApplied);
                    }

                    var applyResult = applyBatch(batch);

                    switch (applyResult)
                    {
                        case BatchApplyResultOk(var applied):
                            totalApplied += applied.AppliedCount;
                            currentVersion = applied.ToVersion;
                            updateVersion(currentVersion);

                            if (!batch.HasMore)
                            {
                                return new IntSyncOk(totalApplied);
                            }

                            break;

                        case BatchApplyResultError(var applyError):
                            return new IntSyncError(applyError);
                    }

                    break;

                case SyncBatchError(var batchError):
                    return new IntSyncError(batchError);
            }
        }
    }

    private static SyncBatchResult CreateBatchFromChanges(
        IReadOnlyList<SyncLogEntry> changes,
        long fromVersion,
        int batchSize
    )
    {
        var hasMore = changes.Count > batchSize;
        var batchChanges = hasMore ? changes.Take(batchSize).ToList() : [.. changes];
        var toVersion = batchChanges.Count > 0 ? batchChanges[^1].Version : fromVersion;

        return new SyncBatchOk(new SyncBatch(batchChanges, fromVersion, toVersion, hasMore));
    }
}
