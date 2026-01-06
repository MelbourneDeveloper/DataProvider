namespace Sync;

/// <summary>
/// Conflict resolution strategy type.
/// </summary>
public enum ConflictStrategy
{
    /// <summary>
    /// Highest timestamp wins (default).
    /// </summary>
    LastWriteWins,

    /// <summary>
    /// Server version always wins.
    /// </summary>
    ServerWins,

    /// <summary>
    /// Client version always wins.
    /// </summary>
    ClientWins,
}

/// <summary>
/// Result of conflict resolution.
/// </summary>
/// <param name="Winner">The change that won the conflict.</param>
/// <param name="Strategy">The strategy used to resolve.</param>
public sealed record ConflictResolution(SyncLogEntry Winner, ConflictStrategy Strategy);

/// <summary>
/// Resolves conflicts between local and remote changes.
/// Implements spec Section 14 (Conflict Resolution).
/// </summary>
internal static class ConflictResolver
{
    /// <summary>
    /// Detects if two changes conflict.
    /// Conflicts occur when same table+PK changed by different origins.
    /// </summary>
    public static bool IsConflict(SyncLogEntry local, SyncLogEntry remote) =>
        local.TableName == remote.TableName
        && local.PkValue == remote.PkValue
        && local.Origin != remote.Origin;

    /// <summary>
    /// Resolves a conflict using the specified strategy.
    /// </summary>
    public static ConflictResolution Resolve(
        SyncLogEntry local,
        SyncLogEntry remote,
        ConflictStrategy strategy
    ) =>
        strategy switch
        {
            ConflictStrategy.LastWriteWins => ResolveLastWriteWins(local, remote),
            ConflictStrategy.ServerWins => new ConflictResolution(remote, strategy),
            ConflictStrategy.ClientWins => new ConflictResolution(local, strategy),
            _ => ResolveLastWriteWins(local, remote),
        };

    /// <summary>
    /// Resolves conflict using Last-Write-Wins (timestamp comparison).
    /// On tie, higher version wins for determinism.
    /// </summary>
    public static ConflictResolution ResolveLastWriteWins(SyncLogEntry local, SyncLogEntry remote)
    {
        var comparison = string.Compare(
            local.Timestamp,
            remote.Timestamp,
            StringComparison.Ordinal
        );

        // If timestamps equal, use version as tiebreaker
        if (comparison == 0)
        {
            comparison = local.Version.CompareTo(remote.Version);
        }

        var winner = comparison >= 0 ? local : remote;
        return new ConflictResolution(winner, ConflictStrategy.LastWriteWins);
    }

    /// <summary>
    /// Resolves a conflict using a custom resolution function.
    /// </summary>
    public static ConflictResolutionResult ResolveCustom(
        SyncLogEntry local,
        SyncLogEntry remote,
        Func<SyncLogEntry, SyncLogEntry, SyncLogEntryResult> resolver
    )
    {
        var result = resolver(local, remote);

        return result.Match<ConflictResolutionResult>(
            entry => new ConflictResolutionOk(
                new ConflictResolution(entry, ConflictStrategy.LastWriteWins)
            ),
            error => new ConflictResolutionError(error)
        );
    }
}
