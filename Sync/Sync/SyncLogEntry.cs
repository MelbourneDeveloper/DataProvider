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
/// <example>
/// <code>
/// // Create an insert entry for a new patient record
/// var insertEntry = new SyncLogEntry(
///     Version: 1,
///     TableName: "patients",
///     PkValue: "{\"Id\": \"550e8400-e29b-41d4-a716-446655440000\"}",
///     Operation: SyncOperation.Insert,
///     Payload: "{\"Id\": \"550e8400-e29b-41d4-a716-446655440000\", \"Name\": \"John Doe\"}",
///     Origin: "client-uuid-123",
///     Timestamp: DateTime.UtcNow.ToString("o")
/// );
///
/// // Create a delete entry (tombstone)
/// var deleteEntry = new SyncLogEntry(
///     Version: 2,
///     TableName: "patients",
///     PkValue: "{\"Id\": \"550e8400-e29b-41d4-a716-446655440000\"}",
///     Operation: SyncOperation.Delete,
///     Payload: null,
///     Origin: "client-uuid-123",
///     Timestamp: DateTime.UtcNow.ToString("o")
/// );
/// </code>
/// </example>
public sealed record SyncLogEntry(
    long Version,
    string TableName,
    string PkValue,
    SyncOperation Operation,
    string? Payload,
    string Origin,
    string Timestamp
);
