using Results;

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
    /// <param name="lastSyncedVersion">Last version successfully synced.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchRemoteChanges">Function to fetch changes from remote.</param>
    /// <param name="applyLocalChange">Function to apply a change locally.</param>
    /// <param name="enableTriggerSuppression">Function to suppress triggers during apply.</param>
    /// <param name="disableTriggerSuppression">Function to re-enable triggers after apply.</param>
    /// <param name="updateLastSyncedVersion">Function to persist the last synced version.</param>
    /// <returns>Pull result or error.</returns>
    public static Result<PullResult, SyncError> Pull(
        string myOriginId,
        long lastSyncedVersion,
        BatchConfig config,
        Func<long, int, Result<IReadOnlyList<SyncLogEntry>, SyncError>> fetchRemoteChanges,
        Func<SyncLogEntry, Result<bool, SyncError>> applyLocalChange,
        Func<Result<bool, SyncError>> enableTriggerSuppression,
        Func<Result<bool, SyncError>> disableTriggerSuppression,
        Action<long> updateLastSyncedVersion)
    {
        // Enable trigger suppression to prevent echo
        var suppressResult = enableTriggerSuppression();
        if (suppressResult is Result<bool, SyncError>.Failure suppressFailure)
        {
            return new Result<PullResult, SyncError>.Failure(suppressFailure.ErrorValue);
        }

        try
        {
            var startVersion = lastSyncedVersion;
            var totalApplied = 0;
            var currentVersion = startVersion;

            while (true)
            {
                var batchResult = BatchManager.FetchBatch(currentVersion, config.BatchSize, fetchRemoteChanges);

                if (batchResult is Result<SyncBatch, SyncError>.Failure batchFailure)
                {
                    return new Result<PullResult, SyncError>.Failure(batchFailure.ErrorValue);
                }

                var batch = ((Result<SyncBatch, SyncError>.Success)batchResult).Value;

                if (batch.Changes.Count == 0)
                {
                    break;
                }

                var applyResult = ChangeApplier.ApplyBatch(
                    batch,
                    myOriginId,
                    config.MaxRetryPasses,
                    applyLocalChange);

                if (applyResult is Result<BatchApplyResult, SyncError>.Failure applyFailure)
                {
                    return new Result<PullResult, SyncError>.Failure(applyFailure.ErrorValue);
                }

                var applied = ((Result<BatchApplyResult, SyncError>.Success)applyResult).Value;
                totalApplied += applied.AppliedCount;
                currentVersion = applied.ToVersion;
                updateLastSyncedVersion(currentVersion);

                if (!batch.HasMore)
                {
                    break;
                }
            }

            return new Result<PullResult, SyncError>.Success(
                new PullResult(totalApplied, startVersion, currentVersion));
        }
        finally
        {
            _ = disableTriggerSuppression();
        }
    }

    /// <summary>
    /// Pushes local changes to a remote destination.
    /// </summary>
    /// <param name="lastPushedVersion">Last version successfully pushed.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchLocalChanges">Function to fetch local changes.</param>
    /// <param name="sendToRemote">Function to send changes to remote.</param>
    /// <param name="updateLastPushedVersion">Function to persist the last pushed version.</param>
    /// <returns>Push result or error.</returns>
    public static Result<PushResult, SyncError> Push(
        long lastPushedVersion,
        BatchConfig config,
        Func<long, int, Result<IReadOnlyList<SyncLogEntry>, SyncError>> fetchLocalChanges,
        Func<IReadOnlyList<SyncLogEntry>, Result<bool, SyncError>> sendToRemote,
        Action<long> updateLastPushedVersion)
    {
        var startVersion = lastPushedVersion;
        var totalPushed = 0;
        var currentVersion = startVersion;

        while (true)
        {
            var batchResult = BatchManager.FetchBatch(currentVersion, config.BatchSize, fetchLocalChanges);

            if (batchResult is Result<SyncBatch, SyncError>.Failure batchFailure)
            {
                return new Result<PushResult, SyncError>.Failure(batchFailure.ErrorValue);
            }

            var batch = ((Result<SyncBatch, SyncError>.Success)batchResult).Value;

            if (batch.Changes.Count == 0)
            {
                break;
            }

            var sendResult = sendToRemote(batch.Changes.ToList());

            if (sendResult is Result<bool, SyncError>.Failure sendFailure)
            {
                return new Result<PushResult, SyncError>.Failure(sendFailure.ErrorValue);
            }

            totalPushed += batch.Changes.Count;
            currentVersion = batch.ToVersion;
            updateLastPushedVersion(currentVersion);

            if (!batch.HasMore)
            {
                break;
            }
        }

        return new Result<PushResult, SyncError>.Success(
            new PushResult(totalPushed, startVersion, currentVersion));
    }

    /// <summary>
    /// Performs a full bidirectional sync: pull then push.
    /// </summary>
    /// <param name="myOriginId">This replica's origin ID.</param>
    /// <param name="lastServerVersion">Last version pulled from server.</param>
    /// <param name="lastPushVersion">Last version pushed to server.</param>
    /// <param name="config">Batch configuration.</param>
    /// <param name="fetchRemoteChanges">Function to fetch changes from remote.</param>
    /// <param name="applyLocalChange">Function to apply a change locally.</param>
    /// <param name="enableTriggerSuppression">Function to suppress triggers.</param>
    /// <param name="disableTriggerSuppression">Function to re-enable triggers.</param>
    /// <param name="updateLastServerVersion">Function to persist last server version.</param>
    /// <param name="fetchLocalChanges">Function to fetch local changes.</param>
    /// <param name="sendToRemote">Function to send changes to remote.</param>
    /// <param name="updateLastPushVersion">Function to persist last push version.</param>
    /// <returns>Full sync result or error.</returns>
    public static Result<SyncResult, SyncError> Sync(
        string myOriginId,
        long lastServerVersion,
        long lastPushVersion,
        BatchConfig config,
        Func<long, int, Result<IReadOnlyList<SyncLogEntry>, SyncError>> fetchRemoteChanges,
        Func<SyncLogEntry, Result<bool, SyncError>> applyLocalChange,
        Func<Result<bool, SyncError>> enableTriggerSuppression,
        Func<Result<bool, SyncError>> disableTriggerSuppression,
        Action<long> updateLastServerVersion,
        Func<long, int, Result<IReadOnlyList<SyncLogEntry>, SyncError>> fetchLocalChanges,
        Func<IReadOnlyList<SyncLogEntry>, Result<bool, SyncError>> sendToRemote,
        Action<long> updateLastPushVersion)
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
            updateLastServerVersion);

        if (pullResult is Result<PullResult, SyncError>.Failure pullFailure)
        {
            return new Result<SyncResult, SyncError>.Failure(pullFailure.ErrorValue);
        }

        var pull = ((Result<PullResult, SyncError>.Success)pullResult).Value;

        // Push second (send local changes)
        var pushResult = Push(
            lastPushVersion,
            config,
            fetchLocalChanges,
            sendToRemote,
            updateLastPushVersion);

        if (pushResult is Result<PushResult, SyncError>.Failure pushFailure)
        {
            return new Result<SyncResult, SyncError>.Failure(pushFailure.ErrorValue);
        }

        var push = ((Result<PushResult, SyncError>.Success)pushResult).Value;

        return new Result<SyncResult, SyncError>.Success(new SyncResult(pull, push));
    }
}
