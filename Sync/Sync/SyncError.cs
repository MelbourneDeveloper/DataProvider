namespace Sync;

/// <summary>
/// Base type for sync errors. Use pattern matching on derived types.
/// </summary>
public abstract record SyncError;

/// <summary>
/// A foreign key constraint was violated.
/// </summary>
/// <param name="TableName">Table where FK violation occurred.</param>
/// <param name="PkValue">Primary key value of the affected row.</param>
/// <param name="Details">Additional details about the violation.</param>
public sealed record SyncErrorForeignKeyViolation(string TableName, string PkValue, string Details)
    : SyncError;

/// <summary>
/// A change could not be applied after all retry attempts.
/// </summary>
/// <param name="Entry">The change entry that failed.</param>
/// <param name="Reason">Reason for failure.</param>
public sealed record SyncErrorDeferredChangeFailed(SyncLogEntry Entry, string Reason) : SyncError;

/// <summary>
/// The client has fallen too far behind and requires full resync.
/// </summary>
/// <param name="ClientVersion">Client's last known version.</param>
/// <param name="OldestAvailableVersion">Oldest version available on server.</param>
public sealed record SyncErrorFullResyncRequired(long ClientVersion, long OldestAvailableVersion)
    : SyncError;

/// <summary>
/// Hash verification failed - data inconsistency detected.
/// </summary>
/// <param name="ExpectedHash">Expected hash value.</param>
/// <param name="ActualHash">Actual computed hash value.</param>
public sealed record SyncErrorHashMismatch(string ExpectedHash, string ActualHash) : SyncError;

/// <summary>
/// A database operation failed.
/// </summary>
/// <param name="Message">Error message.</param>
public sealed record SyncErrorDatabase(string Message) : SyncError;

/// <summary>
/// A conflict was detected that could not be auto-resolved.
/// </summary>
/// <param name="LocalChange">The local change.</param>
/// <param name="RemoteChange">The conflicting remote change.</param>
public sealed record SyncErrorUnresolvedConflict(
    SyncLogEntry LocalChange,
    SyncLogEntry RemoteChange
) : SyncError;
