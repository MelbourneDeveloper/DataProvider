using Microsoft.Extensions.Logging;

namespace Sync;

/// <summary>
/// Manages batch fetching and processing for sync operations.
/// Implements spec Section 12 (Batching).
/// </summary>
internal static class BatchManager
{
    /// <summary>
    /// Fetches a batch of changes from the sync log.
    /// </summary>
    /// <param name="fromVersion">Version to fetch from.</param>
    /// <param name="batchSize">Maximum batch size.</param>
    /// <param name="fetchChanges">Function to fetch changes.</param>
    /// <param name="logger">Logger for batch operations.</param>
    /// <returns>Batch result or sync error.</returns>
    public static SyncBatchResult FetchBatch(
        long fromVersion,
        int batchSize,
        Func<long, int, SyncLogListResult> fetchChanges,
        ILogger logger
    )
    {
        logger.LogDebug(
            "BATCH: Fetching batch from version {FromVersion}, size {BatchSize}",
            fromVersion,
            batchSize
        );

        var fetchResult = fetchChanges(fromVersion, batchSize + 1);

        if (fetchResult is SyncLogListOk(var changes))
        {
            return CreateBatchFromChanges(changes, fromVersion, batchSize, logger);
        }

        var error = ((SyncLogListError)fetchResult).Value;
        logger.LogError("BATCH: Fetch failed: {Error}", error);
        return new SyncBatchError(error);
    }

    /// <summary>
    /// Processes all batches in a pull phase until no more changes are available.
    /// </summary>
    /// <param name="startVersion">Version to start from.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchChanges">Function to fetch changes.</param>
    /// <param name="applyBatch">Function to apply a batch.</param>
    /// <param name="updateVersion">Action to update version.</param>
    /// <param name="logger">Logger for batch operations.</param>
    /// <returns>Total applied count or sync error.</returns>
    public static IntSyncResult ProcessAllBatches(
        long startVersion,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchChanges,
        Func<SyncBatch, BatchApplyResultResult> applyBatch,
        Action<long> updateVersion,
        ILogger logger
    )
    {
        logger.LogInformation(
            "BATCH: Processing all batches from version {StartVersion}, batchSize={BatchSize}",
            startVersion,
            config.BatchSize
        );

        var currentVersion = startVersion;
        var totalApplied = 0;
        var batchNumber = 0;

        while (true)
        {
            batchNumber++;
            var batchResult = FetchBatch(currentVersion, config.BatchSize, fetchChanges, logger);

            switch (batchResult)
            {
                case SyncBatchOk(var batch):
                    if (batch.Changes.Count == 0)
                    {
                        logger.LogInformation(
                            "BATCH: All batches processed. Total applied: {Total}",
                            totalApplied
                        );
                        return new IntSyncOk(totalApplied);
                    }

                    logger.LogDebug(
                        "BATCH: Processing batch {BatchNumber} with {Count} changes",
                        batchNumber,
                        batch.Changes.Count
                    );

                    var applyResult = applyBatch(batch);

                    switch (applyResult)
                    {
                        case BatchApplyResultOk(var applied):
                            totalApplied += applied.AppliedCount;
                            currentVersion = applied.ToVersion;
                            updateVersion(currentVersion);

                            logger.LogDebug(
                                "BATCH: Batch {BatchNumber} applied {Count} changes, version now {Version}",
                                batchNumber,
                                applied.AppliedCount,
                                currentVersion
                            );

                            if (!batch.HasMore)
                            {
                                logger.LogInformation(
                                    "BATCH: All batches processed. Total applied: {Total}",
                                    totalApplied
                                );
                                return new IntSyncOk(totalApplied);
                            }

                            break;

                        case BatchApplyResultError(var applyError):
                            logger.LogError(
                                "BATCH: Batch {BatchNumber} apply failed: {Error}",
                                batchNumber,
                                applyError
                            );
                            return new IntSyncError(applyError);
                    }

                    break;

                case SyncBatchError(var batchError):
                    logger.LogError(
                        "BATCH: Fetch failed on batch {BatchNumber}: {Error}",
                        batchNumber,
                        batchError
                    );
                    return new IntSyncError(batchError);
            }
        }
    }

    private static SyncBatchResult CreateBatchFromChanges(
        IReadOnlyList<SyncLogEntry> changes,
        long fromVersion,
        int batchSize,
        ILogger logger
    )
    {
        var hasMore = changes.Count > batchSize;
        var batchChanges = hasMore ? changes.Take(batchSize).ToList() : [.. changes];
        var toVersion = batchChanges.Count > 0 ? batchChanges[^1].Version : fromVersion;
        var hash = HashVerifier.ComputeBatchHash(batchChanges);

        logger.LogDebug(
            "BATCH: Created batch with {Count} changes, versions {From}-{To}, hasMore={HasMore}, hash={Hash}",
            batchChanges.Count,
            fromVersion,
            toVersion,
            hasMore,
            hash.Length >= 16 ? hash[..16] : hash
        );

        return new SyncBatchOk(new SyncBatch(batchChanges, fromVersion, toVersion, hasMore, hash));
    }

    /// <summary>
    /// Verifies a batch hash matches its contents.
    /// </summary>
    /// <param name="batch">The batch to verify.</param>
    /// <param name="logger">Logger for hash verification.</param>
    /// <returns>True if hash matches or no hash present, error if mismatch.</returns>
    public static BoolSyncResult VerifyBatchHash(SyncBatch batch, ILogger logger)
    {
        if (batch.Hash is null)
        {
            logger.LogDebug("BATCH: No hash to verify, skipping verification");
            return new BoolSyncOk(true);
        }

        logger.LogDebug(
            "BATCH: Verifying hash for batch with {Count} changes",
            batch.Changes.Count
        );
        var computedHash = HashVerifier.ComputeBatchHash(batch.Changes);
        var result = HashVerifier.VerifyHash(batch.Hash, computedHash);

        if (result is BoolSyncOk ok && ok.Value)
        {
            logger.LogDebug("BATCH: Hash verification passed");
        }
        else
        {
            logger.LogWarning(
                "BATCH: Hash verification FAILED! Expected={Expected}, Computed={Computed}",
                batch.Hash.Length >= 16 ? batch.Hash[..16] : batch.Hash,
                computedHash.Length >= 16 ? computedHash[..16] : computedHash
            );
        }

        return result;
    }
}
