using Results;

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
public static class ConflictResolver
{
    /// <summary>
    /// Detects if two changes conflict.
    /// Conflicts occur when same table+PK changed by different origins.
    /// </summary>
    /// <param name="local">Local change.</param>
    /// <param name="remote">Remote change.</param>
    /// <returns>True if changes conflict.</returns>
    public static bool IsConflict(SyncLogEntry local, SyncLogEntry remote) =>
        local.TableName == remote.TableName
        && local.PkValue == remote.PkValue
        && local.Origin != remote.Origin;

    /// <summary>
    /// Resolves a conflict using the specified strategy.
    /// </summary>
    /// <param name="local">Local change.</param>
    /// <param name="remote">Remote change.</param>
    /// <param name="strategy">Resolution strategy to use.</param>
    /// <returns>The winning change.</returns>
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
    /// <param name="local">Local change.</param>
    /// <param name="remote">Remote change.</param>
    /// <returns>The winning change.</returns>
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
    /// <param name="local">Local change.</param>
    /// <param name="remote">Remote change.</param>
    /// <param name="resolver">Custom resolver function.</param>
    /// <returns>Resolution result or error if resolver fails.</returns>
    public static Result<ConflictResolution, SyncError> ResolveCustom(
        SyncLogEntry local,
        SyncLogEntry remote,
        Func<SyncLogEntry, SyncLogEntry, Result<SyncLogEntry, SyncError>> resolver
    )
    {
        var result = resolver(local, remote);

        return result switch
        {
            Result<SyncLogEntry, SyncError>.Success s => new Result<
                ConflictResolution,
                SyncError
            >.Success(new ConflictResolution(s.Value, ConflictStrategy.LastWriteWins)),
            Result<SyncLogEntry, SyncError>.Failure f => new Result<
                ConflictResolution,
                SyncError
            >.Failure(f.ErrorValue),
            _ => new Result<ConflictResolution, SyncError>.Failure(
                new SyncErrorUnresolvedConflict(local, remote)
            ),
        };
    }
}
