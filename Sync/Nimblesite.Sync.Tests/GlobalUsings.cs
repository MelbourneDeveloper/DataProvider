global using Xunit;
// Type aliases for Outcome Result types to simplify test assertions
global using BatchApplyResultError = Outcome.Result<Nimblesite.Sync.Core.BatchApplyResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.BatchApplyResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using BatchApplyResultOk = Outcome.Result<Nimblesite.Sync.Core.BatchApplyResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.BatchApplyResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
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
global using IntSyncOk = Outcome.Result<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using PullResultError = Outcome.Result<Nimblesite.Sync.Core.PullResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.PullResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
// Nimblesite.Sync.CoreCoordinator result types
global using PullResultOk = Outcome.Result<Nimblesite.Sync.Core.PullResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.PullResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using PushResultError = Outcome.Result<Nimblesite.Sync.Core.PushResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.PushResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using PushResultOk = Outcome.Result<Nimblesite.Sync.Core.PushResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.PushResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreLogEntryError = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreLogEntry, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreLogEntry,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreLogEntryOk = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreLogEntry, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreLogEntry,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
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
