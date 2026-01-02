namespace Sync;

/// <summary>
/// Represents the type of change operation tracked in the sync log.
/// </summary>
public enum SyncOperation
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
