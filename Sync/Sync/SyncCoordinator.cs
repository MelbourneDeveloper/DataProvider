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
    public static PullResultResult Pull(
        string myOriginId,
        long lastSyncedVersion,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchRemoteChanges,
        Func<SyncLogEntry, BoolSyncResult> applyLocalChange,
        Func<BoolSyncResult> enableTriggerSuppression,
        Func<BoolSyncResult> disableTriggerSuppression,
        Action<long> updateLastSyncedVersion
    )
    {
        // Enable trigger suppression to prevent echo
        var suppressResult = enableTriggerSuppression();
        if (suppressResult is BoolSyncError suppressFailure)
        {
            return new PullResultError(suppressFailure.Value);
        }

        try
        {
            var startVersion = lastSyncedVersion;
            var totalApplied = 0;
            var currentVersion = startVersion;

            while (true)
            {
                var batchResult = BatchManager.FetchBatch(
                    currentVersion,
                    config.BatchSize,
                    fetchRemoteChanges
                );

                if (batchResult is SyncBatchError batchFailure)
                {
                    return new PullResultError(batchFailure.Value);
                }

                var batch = ((SyncBatchOk)batchResult).Value;

                if (batch.Changes.Count == 0)
                {
                    break;
                }

                var applyResult = ChangeApplier.ApplyBatch(
                    batch,
                    myOriginId,
                    config.MaxRetryPasses,
                    applyLocalChange
                );

                if (applyResult is BatchApplyResultError applyFailure)
                {
                    return new PullResultError(applyFailure.Value);
                }

                var applied = ((BatchApplyResultOk)applyResult).Value;
                totalApplied += applied.AppliedCount;
                currentVersion = applied.ToVersion;
                updateLastSyncedVersion(currentVersion);

                if (!batch.HasMore)
                {
                    break;
                }
            }

            return new PullResultOk(new PullResult(totalApplied, startVersion, currentVersion));
        }
        finally
        {
            _ = disableTriggerSuppression();
        }
    }

    /// <summary>
    /// Pushes local changes to a remote destination.
    /// </summary>
    public static PushResultResult Push(
        long lastPushedVersion,
        BatchConfig config,
        Func<long, int, SyncLogListResult> fetchLocalChanges,
        Func<IReadOnlyList<SyncLogEntry>, BoolSyncResult> sendToRemote,
        Action<long> updateLastPushedVersion
    )
    {
        var startVersion = lastPushedVersion;
        var totalPushed = 0;
        var currentVersion = startVersion;

        while (true)
        {
            var batchResult = BatchManager.FetchBatch(
                currentVersion,
                config.BatchSize,
                fetchLocalChanges
            );

            if (batchResult is SyncBatchError batchFailure)
            {
                return new PushResultError(batchFailure.Value);
            }

            var batch = ((SyncBatchOk)batchResult).Value;

            if (batch.Changes.Count == 0)
            {
                break;
            }

            var sendResult = sendToRemote([.. batch.Changes]);

            if (sendResult is BoolSyncError sendFailure)
            {
                return new PushResultError(sendFailure.Value);
            }

            totalPushed += batch.Changes.Count;
            currentVersion = batch.ToVersion;
            updateLastPushedVersion(currentVersion);

            if (!batch.HasMore)
            {
                break;
            }
        }

        return new PushResultOk(new PushResult(totalPushed, startVersion, currentVersion));
    }

    /// <summary>
    /// Performs a full bidirectional sync: pull then push.
    /// </summary>
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
        Action<long> updateLastPushVersion
    )
    {
        // Pull first (get remote changes)
        var pullResult = Pull(
            myOriginId,
            lastServerVersion,
            config,
            fetchRemoteChanges,
            applyLocalChange,
            enableTriggerSuppression,
            disableTriggerSuppression,
            updateLastServerVersion
        );

        if (pullResult is PullResultError pullFailure)
        {
            return new SyncResultError(pullFailure.Value);
        }

        var pull = ((PullResultOk)pullResult).Value;

        // Push second (send local changes)
        var pushResult = Push(
            lastPushVersion,
            config,
            fetchLocalChanges,
            sendToRemote,
            updateLastPushVersion
        );

        if (pushResult is PushResultError pushFailure)
        {
            return new SyncResultError(pushFailure.Value);
        }

        var push = ((PushResultOk)pushResult).Value;

        return new SyncResultOk(new SyncResult(pull, push));
    }
}
