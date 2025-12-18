using Results;

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
    /// <param name="fromVersion">Fetch changes with version greater than this.</param>
    /// <param name="batchSize">Maximum number of changes to fetch.</param>
    /// <param name="fetchChanges">Function to fetch changes from database.</param>
    /// <returns>A batch of changes or an error.</returns>
    public static Result<SyncBatch, SyncError> FetchBatch(
        long fromVersion,
        int batchSize,
        Func<long, int, Result<IReadOnlyList<SyncLogEntry>, SyncError>> fetchChanges
    )
    {
        var fetchResult = fetchChanges(fromVersion, batchSize + 1);

        return fetchResult switch
        {
            Result<IReadOnlyList<SyncLogEntry>, SyncError>.Success s => CreateBatchFromChanges(
                s.Value,
                fromVersion,
                batchSize
            ),
            Result<IReadOnlyList<SyncLogEntry>, SyncError>.Failure f => new Result<
                SyncBatch,
                SyncError
            >.Failure(f.ErrorValue),
            _ => new Result<SyncBatch, SyncError>.Failure(
                new SyncErrorDatabase("Unexpected result type")
            ),
        };
    }

    /// <summary>
    /// Processes all batches in a pull phase until no more changes are available.
    /// </summary>
    /// <param name="startVersion">Starting version for the pull.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchChanges">Function to fetch changes from server.</param>
    /// <param name="applyBatch">Function to apply a batch of changes.</param>
    /// <param name="updateVersion">Function to persist the last applied version.</param>
    /// <returns>Total number of changes applied or an error.</returns>
    public static Result<int, SyncError> ProcessAllBatches(
        long startVersion,
        BatchConfig config,
        Func<long, int, Result<IReadOnlyList<SyncLogEntry>, SyncError>> fetchChanges,
        Func<SyncBatch, Result<BatchApplyResult, SyncError>> applyBatch,
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
                case Result<SyncBatch, SyncError>.Success batchSuccess:
                    var batch = batchSuccess.Value;

                    if (batch.Changes.Count == 0)
                    {
                        return new Result<int, SyncError>.Success(totalApplied);
                    }

                    var applyResult = applyBatch(batch);

                    switch (applyResult)
                    {
                        case Result<BatchApplyResult, SyncError>.Success applySuccess:
                            totalApplied += applySuccess.Value.AppliedCount;
                            currentVersion = applySuccess.Value.ToVersion;
                            updateVersion(currentVersion);

                            if (!batch.HasMore)
                            {
                                return new Result<int, SyncError>.Success(totalApplied);
                            }

                            break;

                        case Result<BatchApplyResult, SyncError>.Failure applyFailure:
                            return new Result<int, SyncError>.Failure(applyFailure.ErrorValue);

                        default:
                            return new Result<int, SyncError>.Failure(
                                new SyncErrorDatabase("Unexpected apply result type")
                            );
                    }

                    break;

                case Result<SyncBatch, SyncError>.Failure batchFailure:
                    return new Result<int, SyncError>.Failure(batchFailure.ErrorValue);

                default:
                    return new Result<int, SyncError>.Failure(
                        new SyncErrorDatabase("Unexpected batch result type")
                    );
            }
        }
    }

    private static Result<SyncBatch, SyncError> CreateBatchFromChanges(
        IReadOnlyList<SyncLogEntry> changes,
        long fromVersion,
        int batchSize
    )
    {
        var hasMore = changes.Count > batchSize;
        var batchChanges = hasMore ? changes.Take(batchSize).ToList() : changes.ToList();
        var toVersion = batchChanges.Count > 0 ? batchChanges[^1].Version : fromVersion;

        return new Result<SyncBatch, SyncError>.Success(
            new SyncBatch(batchChanges, fromVersion, toVersion, hasMore)
        );
    }
}
