#pragma warning disable IDE0005 // Using directive is unnecessary - these are used for pattern matching

// Type aliases for Result types in tests
global using BatchApplyResultError = Outcome.Result<Sync.BatchApplyResult, Sync.SyncError>.Error<
    Sync.BatchApplyResult,
    Sync.SyncError
>;
global using BatchApplyResultOk = Outcome.Result<Sync.BatchApplyResult, Sync.SyncError>.Ok<
    Sync.BatchApplyResult,
    Sync.SyncError
>;
global using BatchApplyResultResult = Outcome.Result<Sync.BatchApplyResult, Sync.SyncError>;
global using BoolSyncError = Outcome.Result<bool, Sync.SyncError>.Error<bool, Sync.SyncError>;
global using BoolSyncOk = Outcome.Result<bool, Sync.SyncError>.Ok<bool, Sync.SyncError>;
global using BoolSyncResult = Outcome.Result<bool, Sync.SyncError>;
global using ConflictResolutionError = Outcome.Result<
    Sync.ConflictResolution,
    Sync.SyncError
>.Error<Sync.ConflictResolution, Sync.SyncError>;
global using ConflictResolutionOk = Outcome.Result<Sync.ConflictResolution, Sync.SyncError>.Ok<
    Sync.ConflictResolution,
    Sync.SyncError
>;
global using ConflictResolutionResult = Outcome.Result<Sync.ConflictResolution, Sync.SyncError>;
global using IntSyncError = Outcome.Result<int, Sync.SyncError>.Error<int, Sync.SyncError>;
global using IntSyncOk = Outcome.Result<int, Sync.SyncError>.Ok<int, Sync.SyncError>;
global using IntSyncResult = Outcome.Result<int, Sync.SyncError>;
global using SyncBatchError = Outcome.Result<Sync.SyncBatch, Sync.SyncError>.Error<
    Sync.SyncBatch,
    Sync.SyncError
>;
global using SyncBatchOk = Outcome.Result<Sync.SyncBatch, Sync.SyncError>.Ok<
    Sync.SyncBatch,
    Sync.SyncError
>;
global using SyncBatchResult = Outcome.Result<Sync.SyncBatch, Sync.SyncError>;
global using SyncLogEntryError = Outcome.Result<Sync.SyncLogEntry, Sync.SyncError>.Error<
    Sync.SyncLogEntry,
    Sync.SyncError
>;
global using SyncLogEntryOk = Outcome.Result<Sync.SyncLogEntry, Sync.SyncError>.Ok<
    Sync.SyncLogEntry,
    Sync.SyncError
>;
global using SyncLogEntryResult = Outcome.Result<Sync.SyncLogEntry, Sync.SyncError>;
global using SyncLogListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>,
    Sync.SyncError
>.Error<System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>, Sync.SyncError>;
global using SyncLogListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>,
    Sync.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>, Sync.SyncError>;
global using SyncLogListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>,
    Sync.SyncError
>;
