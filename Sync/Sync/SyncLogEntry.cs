namespace Sync;

/// <summary>
/// Represents a single entry in the unified change log (_sync_log).
/// Maps to spec Section 7.2 schema.
/// </summary>
/// <param name="Version">Monotonically increasing version number (PRIMARY KEY).</param>
/// <param name="TableName">Name of the table where the change occurred.</param>
/// <param name="PkValue">JSON-serialized primary key value, e.g. {"Id": "uuid-here"}.</param>
/// <param name="Operation">Type of operation: insert, update, or delete.</param>
/// <param name="Payload">JSON-serialized row data. NULL for deletes.</param>
/// <param name="Origin">UUID of the replica that created this change.</param>
/// <param name="Timestamp">ISO 8601 UTC timestamp with milliseconds.</param>
public sealed record SyncLogEntry(
    long Version,
    string TableName,
    string PkValue,
    SyncOperation Operation,
    string? Payload,
    string Origin,
    string Timestamp
);
