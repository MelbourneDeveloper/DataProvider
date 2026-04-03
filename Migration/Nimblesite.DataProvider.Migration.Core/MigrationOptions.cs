namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Options for migration execution.
/// </summary>
public sealed record Nimblesite.DataProvider.Migration.CoreOptions
{
    /// <summary>
    /// Default migration options (safe, additive only).
    /// </summary>
    public static Nimblesite.DataProvider.Migration.CoreOptions Default => new();

    /// <summary>
    /// Nimblesite.DataProvider.Migration.Core options that allow destructive operations.
    /// USE WITH CAUTION.
    /// </summary>
    public static Nimblesite.DataProvider.Migration.CoreOptions Destructive => new() { AllowDestructive = true };

    /// <summary>
    /// Whether to allow destructive operations (DROP TABLE, DROP COLUMN).
    /// Default: false
    /// </summary>
    public bool AllowDestructive { get; init; }

    /// <summary>
    /// Whether to wrap all operations in a transaction.
    /// Default: true (recommended)
    /// </summary>
    public bool UseTransaction { get; init; } = true;

    /// <summary>
    /// Whether to continue on error (skip failed operations).
    /// Default: false (fail fast)
    /// </summary>
    public bool ContinueOnError { get; init; }

    /// <summary>
    /// Whether to perform a dry run (log DDL without executing).
    /// Default: false
    /// </summary>
    public bool DryRun { get; init; }
}
