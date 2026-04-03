// Type aliases for Result types to reduce verbosity
global using BatchApplyResultError = Outcome.Result<Nimblesite.Sync.Core.BatchApplyResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.BatchApplyResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using BatchApplyResultOk = Outcome.Result<Nimblesite.Sync.Core.BatchApplyResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.BatchApplyResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using BatchApplyResultResult = Outcome.Result<Nimblesite.Sync.Core.BatchApplyResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using BoolSyncError = Outcome.Result<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using BoolSyncOk = Outcome.Result<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using BoolSyncResult = Outcome.Result<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using ConflictResolutionError = Outcome.Result<
    Nimblesite.Sync.Core.ConflictResolution,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Error<Nimblesite.Sync.Core.ConflictResolution, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using ConflictResolutionOk = Outcome.Result<Nimblesite.Sync.Core.ConflictResolution, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.ConflictResolution,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using ConflictResolutionResult = Outcome.Result<Nimblesite.Sync.Core.ConflictResolution, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using IntSyncError = Outcome.Result<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using IntSyncOk = Outcome.Result<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using IntSyncResult = Outcome.Result<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using PullResultError = Outcome.Result<Nimblesite.Sync.Core.PullResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.PullResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using PullResultOk = Outcome.Result<Nimblesite.Sync.Core.PullResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.PullResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using PullResultResult = Outcome.Result<Nimblesite.Sync.Core.PullResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using PushResultError = Outcome.Result<Nimblesite.Sync.Core.PushResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.PushResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using PushResultOk = Outcome.Result<Nimblesite.Sync.Core.PushResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.PushResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using PushResultResult = Outcome.Result<Nimblesite.Sync.Core.PushResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using Nimblesite.Sync.CoreBatchError = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreBatch, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreBatch,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreBatchOk = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreBatch, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreBatch,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreBatchResult = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreBatch, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using Nimblesite.Sync.CoreLogEntryResult = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreLogEntry, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using Nimblesite.Sync.CoreLogListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreLogEntry>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Error<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreLogEntry>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using Nimblesite.Sync.CoreLogListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreLogEntry>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Ok<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreLogEntry>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using Nimblesite.Sync.CoreLogListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreLogEntry>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreResultError = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreResultOk = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreResultResult = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
