// Type aliases for Result types to reduce verbosity
global using BatchApplyResultError = Outcome.Result<
    Nimblesite.Sync.Core.BatchApplyResult,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.BatchApplyResult, Nimblesite.Sync.Core.SyncError>;
global using BatchApplyResultOk = Outcome.Result<
    Nimblesite.Sync.Core.BatchApplyResult,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.BatchApplyResult, Nimblesite.Sync.Core.SyncError>;
global using BatchApplyResultResult = Outcome.Result<
    Nimblesite.Sync.Core.BatchApplyResult,
    Nimblesite.Sync.Core.SyncError
>;
global using BoolSyncError = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Error<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
global using BoolSyncOk = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Ok<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
global using BoolSyncResult = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>;
global using ConflictResolutionError = Outcome.Result<
    Nimblesite.Sync.Core.ConflictResolution,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.ConflictResolution, Nimblesite.Sync.Core.SyncError>;
global using ConflictResolutionOk = Outcome.Result<
    Nimblesite.Sync.Core.ConflictResolution,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.ConflictResolution, Nimblesite.Sync.Core.SyncError>;
global using ConflictResolutionResult = Outcome.Result<
    Nimblesite.Sync.Core.ConflictResolution,
    Nimblesite.Sync.Core.SyncError
>;
global using IntSyncError = Outcome.Result<int, Nimblesite.Sync.Core.SyncError>.Error<
    int,
    Nimblesite.Sync.Core.SyncError
>;
global using IntSyncOk = Outcome.Result<int, Nimblesite.Sync.Core.SyncError>.Ok<
    int,
    Nimblesite.Sync.Core.SyncError
>;
global using IntSyncResult = Outcome.Result<int, Nimblesite.Sync.Core.SyncError>;
global using PullResultError = Outcome.Result<
    Nimblesite.Sync.Core.PullResult,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.PullResult, Nimblesite.Sync.Core.SyncError>;
global using PullResultOk = Outcome.Result<
    Nimblesite.Sync.Core.PullResult,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.PullResult, Nimblesite.Sync.Core.SyncError>;
global using PullResultResult = Outcome.Result<
    Nimblesite.Sync.Core.PullResult,
    Nimblesite.Sync.Core.SyncError
>;
global using PushResultError = Outcome.Result<
    Nimblesite.Sync.Core.PushResult,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.PushResult, Nimblesite.Sync.Core.SyncError>;
global using PushResultOk = Outcome.Result<
    Nimblesite.Sync.Core.PushResult,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.PushResult, Nimblesite.Sync.Core.SyncError>;
global using PushResultResult = Outcome.Result<
    Nimblesite.Sync.Core.PushResult,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncBatchError = Outcome.Result<
    Nimblesite.Sync.Core.SyncBatch,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.SyncBatch, Nimblesite.Sync.Core.SyncError>;
global using SyncBatchOk = Outcome.Result<
    Nimblesite.Sync.Core.SyncBatch,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.SyncBatch, Nimblesite.Sync.Core.SyncError>;
global using SyncBatchResult = Outcome.Result<
    Nimblesite.Sync.Core.SyncBatch,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncLogEntryResult = Outcome.Result<
    Nimblesite.Sync.Core.SyncLogEntry,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncLogListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>.Error<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncLogListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncLogListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncResultError = Outcome.Result<
    Nimblesite.Sync.Core.SyncResult,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.SyncResult, Nimblesite.Sync.Core.SyncError>;
global using SyncResultOk = Outcome.Result<
    Nimblesite.Sync.Core.SyncResult,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.SyncResult, Nimblesite.Sync.Core.SyncError>;
global using SyncResultResult = Outcome.Result<
    Nimblesite.Sync.Core.SyncResult,
    Nimblesite.Sync.Core.SyncError
>;
