namespace Sync.Api;

/// <summary>
/// Request to push changes.
/// </summary>
public sealed record PushChangesRequest(string OriginId, List<SyncLogEntryDto> Changes);

/// <summary>
/// Request to register a client.
/// </summary>
public sealed record RegisterClientRequest(string OriginId, long LastSyncVersion);

/// <summary>
/// DTO for sync log entry (JSON serialization).
/// </summary>
public sealed record SyncLogEntryDto(
    long Version,
    string TableName,
    string PkValue,
    string Operation,
    string? Payload,
    string Origin,
    string Timestamp
);
