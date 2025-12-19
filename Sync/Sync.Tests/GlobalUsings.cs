#pragma warning disable IDE0005 // Using directive is unnecessary - these are used for pattern matching

global using Xunit;
// Type aliases for Outcome Result types to simplify test assertions
global using BatchApplyResultError = Outcome.Result<Sync.BatchApplyResult, Sync.SyncError>.Error<
    Sync.BatchApplyResult,
    Sync.SyncError
>;
global using BatchApplyResultOk = Outcome.Result<Sync.BatchApplyResult, Sync.SyncError>.Ok<
    Sync.BatchApplyResult,
    Sync.SyncError
>;
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
global using IntSyncOk = Outcome.Result<int, Sync.SyncError>.Ok<int, Sync.SyncError>;
global using PullResultError = Outcome.Result<Sync.PullResult, Sync.SyncError>.Error<
    Sync.PullResult,
    Sync.SyncError
>;
// SyncCoordinator result types
global using PullResultOk = Outcome.Result<Sync.PullResult, Sync.SyncError>.Ok<
    Sync.PullResult,
    Sync.SyncError
>;
global using PushResultError = Outcome.Result<Sync.PushResult, Sync.SyncError>.Error<
    Sync.PushResult,
    Sync.SyncError
>;
global using PushResultOk = Outcome.Result<Sync.PushResult, Sync.SyncError>.Ok<
    Sync.PushResult,
    Sync.SyncError
>;
global using SyncLogEntryError = Outcome.Result<Sync.SyncLogEntry, Sync.SyncError>.Error<
    Sync.SyncLogEntry,
    Sync.SyncError
>;
global using SyncLogEntryOk = Outcome.Result<Sync.SyncLogEntry, Sync.SyncError>.Ok<
    Sync.SyncLogEntry,
    Sync.SyncError
>;
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
global using SyncResultError = Outcome.Result<Sync.SyncResult, Sync.SyncError>.Error<
    Sync.SyncResult,
    Sync.SyncError
>;
global using SyncResultOk = Outcome.Result<Sync.SyncResult, Sync.SyncError>.Ok<
    Sync.SyncResult,
    Sync.SyncError
>;
