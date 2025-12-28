using Microsoft.Extensions.Logging;

namespace Sync;

/// <summary>
/// Result of a sync pull operation.
/// </summary>
/// <param name="ChangesApplied">Number of changes pulled and applied.</param>
/// <param name="FromVersion">Starting version of the pull.</param>
/// <param name="ToVersion">Ending version after the pull.</param>
public sealed record PullResult(int ChangesApplied, long FromVersion, long ToVersion);

/// <summary>
/// Result of a sync push operation.
/// </summary>
/// <param name="ChangesPushed">Number of changes pushed.</param>
/// <param name="FromVersion">Starting version of the push.</param>
/// <param name="ToVersion">Ending version after the push.</param>
public sealed record PushResult(int ChangesPushed, long FromVersion, long ToVersion);

/// <summary>
/// Result of a full bidirectional sync operation.
/// </summary>
/// <param name="Pull">Result of pulling changes from remote.</param>
/// <param name="Push">Result of pushing changes to remote.</param>
public sealed record SyncResult(PullResult Pull, PushResult Push);

/// <summary>
/// Coordinates sync operations between replicas.
/// Main entry point for pull/push sync operations.
/// Implements spec Section 11 (Bi-Directional Sync Protocol).
/// </summary>
public static class SyncCoordinator
{
    /// <summary>
    /// Pulls changes from a remote source and applies them locally.
    /// </summary>
    /// <param name="myOriginId">This replica's origin ID.</param>
    /// <param name="lastSyncedVersion">Last synced version to start from.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchRemoteChanges">Function to fetch changes from remote.</param>
    /// <param name="applyLocalChange">Function to apply a change locally.</param>
    /// <param name="enableTriggerSuppression">Function to enable trigger suppression.</param>
    /// <param name="disableTriggerSuppression">Function to disable trigger suppression.</param>
    /// <param name="updateLastSyncedVersion">Action to update last synced version.</param>
    /// <param name="logger">Logger for sync operations.</param>
    /// <returns>Pull result or sync error.</returns>
    public static PullResultResult Pull(
        string myOriginId,
        long lastSyncedVersion,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchRemoteChanges,
        Func<SyncLogEntry, BoolSyncResult> applyLocalChange,
        Func<BoolSyncResult> enableTriggerSuppression,
        Func<BoolSyncResult> disableTriggerSuppression,
        Action<long> updateLastSyncedVersion,
        ILogger logger
    )
    {
        logger.LogInformation(
            "PULL: Starting pull from version {FromVersion}, origin={OriginId}, batchSize={BatchSize}",
            lastSyncedVersion,
            myOriginId,
            config.BatchSize
        );

        // Enable trigger suppression to prevent echo
        var suppressResult = enableTriggerSuppression();
        if (suppressResult is BoolSyncError suppressFailure)
        {
            logger.LogError(
                "PULL: Failed to enable trigger suppression: {Error}",
                suppressFailure.Value
            );
            return new PullResultError(suppressFailure.Value);
        }

        logger.LogDebug("PULL: Trigger suppression enabled");

        try
        {
            var startVersion = lastSyncedVersion;
            var totalApplied = 0;
            var currentVersion = startVersion;
            var batchNumber = 0;

            while (true)
            {
                batchNumber++;
                logger.LogDebug(
                    "PULL: Fetching batch {BatchNumber} from version {Version}",
                    batchNumber,
                    currentVersion
                );

                var batchResult = BatchManager.FetchBatch(
                    currentVersion,
                    config.BatchSize,
                    fetchRemoteChanges,
                    logger
                );

                if (batchResult is SyncBatchError batchFailure)
                {
                    logger.LogError("PULL: Batch fetch failed: {Error}", batchFailure.Value);
                    return new PullResultError(batchFailure.Value);
                }

                var batch = ((SyncBatchOk)batchResult).Value;

                if (batch.Changes.Count == 0)
                {
                    logger.LogDebug("PULL: No more changes to pull");
                    break;
                }

                logger.LogInformation(
                    "PULL: Batch {BatchNumber} contains {Count} changes, versions {From}-{To}, hasMore={HasMore}",
                    batchNumber,
                    batch.Changes.Count,
                    batch.FromVersion,
                    batch.ToVersion,
                    batch.HasMore
                );

                var applyResult = ChangeApplier.ApplyBatch(
                    batch,
                    myOriginId,
                    config.MaxRetryPasses,
                    applyLocalChange,
                    logger
                );

                if (applyResult is BatchApplyResultError applyFailure)
                {
                    logger.LogError("PULL: Batch apply failed: {Error}", applyFailure.Value);
                    return new PullResultError(applyFailure.Value);
                }

                var applied = ((BatchApplyResultOk)applyResult).Value;
                totalApplied += applied.AppliedCount;
                currentVersion = applied.ToVersion;
                updateLastSyncedVersion(currentVersion);

                logger.LogDebug(
                    "PULL: Applied {Count} changes, updated version to {Version}",
                    applied.AppliedCount,
                    currentVersion
                );

                if (!batch.HasMore)
                {
                    break;
                }
            }

            logger.LogInformation(
                "PULL: Complete. Applied {Total} changes, version {From}->{To}",
                totalApplied,
                startVersion,
                currentVersion
            );

            return new PullResultOk(new PullResult(totalApplied, startVersion, currentVersion));
        }
        finally
        {
            logger.LogDebug("PULL: Disabling trigger suppression");
            _ = disableTriggerSuppression();
        }
    }

    /// <summary>
    /// Pushes local changes to a remote destination.
    /// </summary>
    /// <param name="lastPushedVersion">Last pushed version to start from.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchLocalChanges">Function to fetch local changes.</param>
    /// <param name="sendToRemote">Function to send changes to remote.</param>
    /// <param name="updateLastPushedVersion">Action to update last pushed version.</param>
    /// <param name="logger">Logger for sync operations.</param>
    /// <returns>Push result or sync error.</returns>
    public static PushResultResult Push(
        long lastPushedVersion,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchLocalChanges,
        Func<IReadOnlyList<SyncLogEntry>, BoolSyncResult> sendToRemote,
        Action<long> updateLastPushedVersion,
        ILogger logger
    )
    {
        logger.LogInformation(
            "PUSH: Starting push from version {FromVersion}, batchSize={BatchSize}",
            lastPushedVersion,
            config.BatchSize
        );

        var startVersion = lastPushedVersion;
        var totalPushed = 0;
        var currentVersion = startVersion;
        var batchNumber = 0;

        while (true)
        {
            batchNumber++;
            logger.LogDebug(
                "PUSH: Fetching batch {BatchNumber} from version {Version}",
                batchNumber,
                currentVersion
            );

            var batchResult = BatchManager.FetchBatch(
                currentVersion,
                config.BatchSize,
                fetchLocalChanges,
                logger
            );

            if (batchResult is SyncBatchError batchFailure)
            {
                logger.LogError("PUSH: Batch fetch failed: {Error}", batchFailure.Value);
                return new PushResultError(batchFailure.Value);
            }

            var batch = ((SyncBatchOk)batchResult).Value;

            if (batch.Changes.Count == 0)
            {
                logger.LogDebug("PUSH: No more changes to push");
                break;
            }

            logger.LogInformation(
                "PUSH: Batch {BatchNumber} contains {Count} changes, versions {From}-{To}, hasMore={HasMore}",
                batchNumber,
                batch.Changes.Count,
                batch.FromVersion,
                batch.ToVersion,
                batch.HasMore
            );

            logger.LogDebug("PUSH: Sending {Count} changes to remote", batch.Changes.Count);
            var sendResult = sendToRemote([.. batch.Changes]);

            if (sendResult is BoolSyncError sendFailure)
            {
                logger.LogError("PUSH: Send to remote failed: {Error}", sendFailure.Value);
                return new PushResultError(sendFailure.Value);
            }

            totalPushed += batch.Changes.Count;
            currentVersion = batch.ToVersion;
            updateLastPushedVersion(currentVersion);

            logger.LogDebug(
                "PUSH: Sent {Count} changes, updated version to {Version}",
                batch.Changes.Count,
                currentVersion
            );

            if (!batch.HasMore)
            {
                break;
            }
        }

        logger.LogInformation(
            "PUSH: Complete. Pushed {Total} changes, version {From}->{To}",
            totalPushed,
            startVersion,
            currentVersion
        );

        return new PushResultOk(new PushResult(totalPushed, startVersion, currentVersion));
    }

    /// <summary>
    /// Performs a full bidirectional sync: pull then push.
    /// </summary>
    /// <param name="myOriginId">This replica's origin ID.</param>
    /// <param name="lastServerVersion">Last server version to pull from.</param>
    /// <param name="lastPushVersion">Last push version to push from.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchRemoteChanges">Function to fetch remote changes.</param>
    /// <param name="applyLocalChange">Function to apply a change locally.</param>
    /// <param name="enableTriggerSuppression">Function to enable trigger suppression.</param>
    /// <param name="disableTriggerSuppression">Function to disable trigger suppression.</param>
    /// <param name="updateLastServerVersion">Action to update last server version.</param>
    /// <param name="fetchLocalChanges">Function to fetch local changes.</param>
    /// <param name="sendToRemote">Function to send changes to remote.</param>
    /// <param name="updateLastPushVersion">Action to update last push version.</param>
    /// <param name="logger">Logger for sync operations.</param>
    /// <returns>Sync result or sync error.</returns>
    public static SyncResultResult Sync(
        string myOriginId,
        long lastServerVersion,
        long lastPushVersion,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchRemoteChanges,
        Func<SyncLogEntry, BoolSyncResult> applyLocalChange,
        Func<BoolSyncResult> enableTriggerSuppression,
        Func<BoolSyncResult> disableTriggerSuppression,
        Action<long> updateLastServerVersion,
        Func<long, int, SyncLogListResult> fetchLocalChanges,
        Func<IReadOnlyList<SyncLogEntry>, BoolSyncResult> sendToRemote,
        Action<long> updateLastPushVersion,
        ILogger logger
    )
    {
        logger.LogInformation(
            "SYNC: Starting bidirectional sync, origin={OriginId}, serverVersion={ServerVersion}, pushVersion={PushVersion}",
            myOriginId,
            lastServerVersion,
            lastPushVersion
        );

        // Pull first (get remote changes)
        logger.LogDebug("SYNC: Starting PULL phase");
        var pullResult = Pull(
            myOriginId,
            lastServerVersion,
            config,
            fetchRemoteChanges,
            applyLocalChange,
            enableTriggerSuppression,
            disableTriggerSuppression,
            updateLastServerVersion,
            logger
        );

        if (pullResult is PullResultError pullFailure)
        {
            logger.LogError("SYNC: Pull phase failed: {Error}", pullFailure.Value);
            return new SyncResultError(pullFailure.Value);
        }

        var pull = ((PullResultOk)pullResult).Value;
        logger.LogInformation(
            "SYNC: Pull phase complete. Applied {Count} changes",
            pull.ChangesApplied
        );

        // Push second (send local changes)
        logger.LogDebug("SYNC: Starting PUSH phase");
        var pushResult = Push(
            lastPushVersion,
            config,
            fetchLocalChanges,
            sendToRemote,
            updateLastPushVersion,
            logger
        );

        if (pushResult is PushResultError pushFailure)
        {
            logger.LogError("SYNC: Push phase failed: {Error}", pushFailure.Value);
            return new SyncResultError(pushFailure.Value);
        }

        var push = ((PushResultOk)pushResult).Value;
        logger.LogInformation(
            "SYNC: Push phase complete. Pushed {Count} changes",
            push.ChangesPushed
        );

        logger.LogInformation(
            "SYNC: Bidirectional sync complete. Pulled {PullCount}, pushed {PushCount}",
            pull.ChangesApplied,
            push.ChangesPushed
        );

        return new SyncResultOk(new SyncResult(pull, push));
    }
}
