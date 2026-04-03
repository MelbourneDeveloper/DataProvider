namespace Nimblesite.Sync.Core;

/// <summary>
/// Represents the type of change operation tracked in the sync log.
/// </summary>
public enum Nimblesite.Sync.CoreOperation
{
    /// <summary>
    /// A new row was inserted.
    /// </summary>
    Insert,

    /// <summary>
    /// An existing row was updated.
    /// </summary>
    Update,

    /// <summary>
    /// A row was deleted (tombstone).
    /// </summary>
    Delete,
}
