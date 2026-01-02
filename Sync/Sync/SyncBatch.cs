namespace Sync;

/// <summary>
/// Represents a batch of changes to be synced. Maps to spec Section 12.
/// </summary>
/// <param name="Changes">The changes in this batch, ordered by version ascending.</param>
/// <param name="FromVersion">The starting version (exclusive) for this batch.</param>
/// <param name="ToVersion">The ending version (inclusive) for this batch.</param>
/// <param name="HasMore">True if more batches are available after this one.</param>
/// <param name="Hash">Optional SHA-256 hash of batch contents for verification (spec S15.4).</param>
/// <example>
/// <code>
/// // Create a batch with changes
/// var changes = new List&lt;SyncLogEntry&gt;
/// {
///     new SyncLogEntry(Version: 101, TableName: "patients", ...),
///     new SyncLogEntry(Version: 102, TableName: "appointments", ...),
/// };
/// var batch = new SyncBatch(
///     Changes: changes,
///     FromVersion: 100,
///     ToVersion: 102,
///     HasMore: true
/// );
///
/// // Process batch
/// if (batch.HasMore)
/// {
///     // Fetch next batch starting from ToVersion
///     var nextBatch = await FetchBatch(batch.ToVersion);
/// }
/// </code>
/// </example>
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
/// <example>
/// <code>
/// // Use default configuration (1000 records per batch, 3 retry passes)
/// var defaultConfig = new BatchConfig();
///
/// // Custom configuration for large syncs
/// var largeConfig = new BatchConfig(BatchSize: 5000, MaxRetryPasses: 5);
///
/// // Perform sync with custom config
/// var result = SyncCoordinator.Sync(
///     myOriginId: originId,
///     lastServerVersion: 0,
///     lastPushVersion: 0,
///     config: largeConfig,
///     ...
/// );
/// </code>
/// </example>
public sealed record BatchConfig(int BatchSize = 1000, int MaxRetryPasses = 3);

/// <summary>
/// Result of applying a single batch.
/// </summary>
/// <param name="AppliedCount">Number of changes successfully applied.</param>
/// <param name="DeferredCount">Number of changes deferred due to FK violations.</param>
/// <param name="ToVersion">The max version applied in this batch.</param>
/// <example>
/// <code>
/// // After applying a batch
/// var result = new BatchApplyResult(
///     AppliedCount: 95,
///     DeferredCount: 5,
///     ToVersion: 150
/// );
///
/// // Check for deferred changes (FK violations that need retry)
/// if (result.DeferredCount > 0)
/// {
///     Console.WriteLine($"{result.DeferredCount} changes deferred, will retry");
/// }
///
/// Console.WriteLine($"Applied {result.AppliedCount} changes up to version {result.ToVersion}");
/// </code>
/// </example>
public sealed record BatchApplyResult(int AppliedCount, int DeferredCount, long ToVersion);
