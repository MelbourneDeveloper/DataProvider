namespace Sync;

/// <summary>
/// Represents a batch of changes to be synced. Maps to spec Section 12.
/// </summary>
/// <param name="Changes">The changes in this batch, ordered by version ascending.</param>
/// <param name="FromVersion">The starting version (exclusive) for this batch.</param>
/// <param name="ToVersion">The ending version (inclusive) for this batch.</param>
/// <param name="HasMore">True if more batches are available after this one.</param>
/// <param name="Hash">Optional SHA-256 hash of batch contents for verification (spec S15.4).</param>
public sealed record SyncBatch(
    IReadOnlyList<SyncLogEntry> Changes,
    long FromVersion,
    long ToVersion,
    bool HasMore,
    string? Hash = null
);

/// <summary>
/// Configuration for batch processing.
/// </summary>
/// <param name="BatchSize">Number of records per batch. Default: 1000.</param>
/// <param name="MaxRetryPasses">Max retry passes for deferred FK violations. Default: 3.</param>
public sealed record BatchConfig(int BatchSize = 1000, int MaxRetryPasses = 3);

/// <summary>
/// Result of applying a single batch.
/// </summary>
/// <param name="AppliedCount">Number of changes successfully applied.</param>
/// <param name="DeferredCount">Number of changes deferred due to FK violations.</param>
/// <param name="ToVersion">The max version applied in this batch.</param>
public sealed record BatchApplyResult(int AppliedCount, int DeferredCount, long ToVersion);
