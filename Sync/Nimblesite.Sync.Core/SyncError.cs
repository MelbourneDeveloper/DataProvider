namespace Nimblesite.Sync.Core;

/// <summary>
/// Base type for sync errors. Use pattern matching on derived types.
/// </summary>
public abstract record Nimblesite.Sync.CoreError
{
    /// <summary>
    /// Prevents external inheritance - this makes the type hierarchy "closed".
    /// </summary>
    private protected Nimblesite.Sync.CoreError() { }
}

/// <summary>
/// A foreign key constraint was violated.
/// </summary>
/// <param name="TableName">Table where FK violation occurred.</param>
/// <param name="PkValue">Primary key value of the affected row.</param>
/// <param name="Details">Additional details about the violation.</param>
public sealed record Nimblesite.Sync.CoreErrorForeignKeyViolation(string TableName, string PkValue, string Details)
    : Nimblesite.Sync.CoreError;

/// <summary>
/// A change could not be applied after all retry attempts.
/// </summary>
/// <param name="Entry">The change entry that failed.</param>
/// <param name="Reason">Reason for failure.</param>
public sealed record Nimblesite.Sync.CoreErrorDeferredChangeFailed(Nimblesite.Sync.CoreLogEntry Entry, string Reason) : Nimblesite.Sync.CoreError;

/// <summary>
/// The client has fallen too far behind and requires full resync.
/// </summary>
/// <param name="ClientVersion">Client's last known version.</param>
/// <param name="OldestAvailableVersion">Oldest version available on server.</param>
public sealed record Nimblesite.Sync.CoreErrorFullResyncRequired(long ClientVersion, long OldestAvailableVersion)
    : Nimblesite.Sync.CoreError;

/// <summary>
/// Hash verification failed - data inconsistency detected.
/// </summary>
/// <param name="ExpectedHash">Expected hash value.</param>
/// <param name="ActualHash">Actual computed hash value.</param>
public sealed record Nimblesite.Sync.CoreErrorHashMismatch(string ExpectedHash, string ActualHash) : Nimblesite.Sync.CoreError;

/// <summary>
/// A database operation failed.
/// </summary>
/// <param name="Message">Error message.</param>
public sealed record Nimblesite.Sync.CoreErrorDatabase(string Message) : Nimblesite.Sync.CoreError;

/// <summary>
/// A conflict was detected that could not be auto-resolved.
/// </summary>
/// <param name="LocalChange">The local change.</param>
/// <param name="RemoteChange">The conflicting remote change.</param>
public sealed record Nimblesite.Sync.CoreErrorUnresolvedConflict(
    Nimblesite.Sync.CoreLogEntry LocalChange,
    Nimblesite.Sync.CoreLogEntry RemoteChange
) : Nimblesite.Sync.CoreError;
