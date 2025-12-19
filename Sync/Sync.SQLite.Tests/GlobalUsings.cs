#pragma warning disable IDE0005 // Using directive is unnecessary - these are used for pattern matching

global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
// Type aliases for Result types in tests
global using BatchApplyResultOk = Outcome.Result<Sync.BatchApplyResult, Sync.SyncError>.Ok<
    Sync.BatchApplyResult,
    Sync.SyncError
>;
global using BoolSyncError = Outcome.Result<bool, Sync.SyncError>.Error<bool, Sync.SyncError>;
global using BoolSyncOk = Outcome.Result<bool, Sync.SyncError>.Ok<bool, Sync.SyncError>;
global using BoolSyncResult = Outcome.Result<bool, Sync.SyncError>;
global using ColumnInfoListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SQLite.TriggerColumnInfo>,
    Sync.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<Sync.SQLite.TriggerColumnInfo>, Sync.SyncError>;
global using IntSyncOk = Outcome.Result<int, Sync.SyncError>.Ok<int, Sync.SyncError>;
global using LongSyncOk = Outcome.Result<long, Sync.SyncError>.Ok<long, Sync.SyncError>;
global using StringSyncError = Outcome.Result<string, Sync.SyncError>.Error<string, Sync.SyncError>;
global using StringSyncOk = Outcome.Result<string, Sync.SyncError>.Ok<string, Sync.SyncError>;
global using SubscriptionListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncSubscription>,
    Sync.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<Sync.SyncSubscription>, Sync.SyncError>;
global using SubscriptionOk = Outcome.Result<Sync.SyncSubscription?, Sync.SyncError>.Ok<
    Sync.SyncSubscription?,
    Sync.SyncError
>;
global using SyncBatchOk = Outcome.Result<Sync.SyncBatch, Sync.SyncError>.Ok<
    Sync.SyncBatch,
    Sync.SyncError
>;
global using SyncClientListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncClient>,
    Sync.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<Sync.SyncClient>, Sync.SyncError>;
global using SyncClientOk = Outcome.Result<Sync.SyncClient?, Sync.SyncError>.Ok<
    Sync.SyncClient?,
    Sync.SyncError
>;
global using SyncLogListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>,
    Sync.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>, Sync.SyncError>;
