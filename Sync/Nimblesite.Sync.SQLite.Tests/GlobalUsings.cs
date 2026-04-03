global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
// Type aliases for Result types in tests
global using BatchApplyResultOk = Outcome.Result<Nimblesite.Sync.Core.BatchApplyResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.BatchApplyResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using BoolSyncError = Outcome.Result<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using BoolSyncOk = Outcome.Result<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using BoolSyncResult = Outcome.Result<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using ColumnInfoListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Ok<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using IntSyncOk = Outcome.Result<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using LongSyncOk = Outcome.Result<long, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<long, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using StringSyncError = Outcome.Result<string, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<string, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using StringSyncOk = Outcome.Result<string, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<string, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using SubscriptionListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Ok<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using SubscriptionOk = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription?,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreBatchOk = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreBatch, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreBatch,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreClientListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Ok<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using Nimblesite.Sync.CoreClientOk = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient?,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreLogListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreLogEntry>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Ok<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreLogEntry>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
