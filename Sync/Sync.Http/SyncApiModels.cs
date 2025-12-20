namespace Sync.Http;

/// <summary>
/// Request to push changes to the sync server.
/// </summary>
/// <param name="OriginId">Origin ID of the pushing client.</param>
/// <param name="Changes">List of changes to push.</param>
public sealed record PushChangesRequest(string OriginId, List<SyncLogEntryDto> Changes);

/// <summary>
/// Request to register a sync client.
/// </summary>
/// <param name="OriginId">Origin ID of the client.</param>
/// <param name="LastSyncVersion">Last known sync version.</param>
public sealed record RegisterClientRequest(string OriginId, long LastSyncVersion);

/// <summary>
/// DTO for sync log entry (JSON serialization).
/// </summary>
/// <param name="Version">Sync version number.</param>
/// <param name="TableName">Name of the table.</param>
/// <param name="PkValue">Primary key value (JSON).</param>
/// <param name="Operation">Operation type (INSERT, UPDATE, DELETE).</param>
/// <param name="Payload">JSON payload of the change.</param>
/// <param name="Origin">Origin ID that made the change.</param>
/// <param name="Timestamp">ISO 8601 timestamp.</param>
public sealed record SyncLogEntryDto(
    long Version,
    string TableName,
    string PkValue,
    string Operation,
    string? Payload,
    string Origin,
    string Timestamp
);
