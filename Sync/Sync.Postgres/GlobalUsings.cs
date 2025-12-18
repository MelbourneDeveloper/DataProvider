#pragma warning disable IDE0005 // Using directive is unnecessary - these are used for pattern matching

global using Microsoft.Extensions.Logging;
global using Npgsql;
// Type aliases for Result types - matching Sync.SQLite patterns using Outcome package
global using BoolSyncError = Outcome.Result<bool, Sync.SyncError>.Error<bool, Sync.SyncError>;
global using BoolSyncOk = Outcome.Result<bool, Sync.SyncError>.Ok<bool, Sync.SyncError>;
global using BoolSyncResult = Outcome.Result<bool, Sync.SyncError>;
global using ColumnInfoListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.Postgres.TriggerColumnInfo>,
    Sync.SyncError
>.Error<System.Collections.Generic.IReadOnlyList<Sync.Postgres.TriggerColumnInfo>, Sync.SyncError>;
global using ColumnInfoListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.Postgres.TriggerColumnInfo>,
    Sync.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<Sync.Postgres.TriggerColumnInfo>, Sync.SyncError>;
global using ColumnInfoListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.Postgres.TriggerColumnInfo>,
    Sync.SyncError
>;
global using IntSyncError = Outcome.Result<int, Sync.SyncError>.Error<int, Sync.SyncError>;
global using IntSyncOk = Outcome.Result<int, Sync.SyncError>.Ok<int, Sync.SyncError>;
global using IntSyncResult = Outcome.Result<int, Sync.SyncError>;
global using LongSyncError = Outcome.Result<long, Sync.SyncError>.Error<long, Sync.SyncError>;
global using LongSyncOk = Outcome.Result<long, Sync.SyncError>.Ok<long, Sync.SyncError>;
global using LongSyncResult = Outcome.Result<long, Sync.SyncError>;
global using StringSyncError = Outcome.Result<string, Sync.SyncError>.Error<string, Sync.SyncError>;
global using StringSyncOk = Outcome.Result<string, Sync.SyncError>.Ok<string, Sync.SyncError>;
global using StringSyncResult = Outcome.Result<string, Sync.SyncError>;
global using SubscriptionError = Outcome.Result<Sync.SyncSubscription?, Sync.SyncError>.Error<
    Sync.SyncSubscription?,
    Sync.SyncError
>;
global using SubscriptionListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncSubscription>,
    Sync.SyncError
>.Error<System.Collections.Generic.IReadOnlyList<Sync.SyncSubscription>, Sync.SyncError>;
global using SubscriptionListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncSubscription>,
    Sync.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<Sync.SyncSubscription>, Sync.SyncError>;
global using SubscriptionListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncSubscription>,
    Sync.SyncError
>;
global using SubscriptionOk = Outcome.Result<Sync.SyncSubscription?, Sync.SyncError>.Ok<
    Sync.SyncSubscription?,
    Sync.SyncError
>;
global using SubscriptionResult = Outcome.Result<Sync.SyncSubscription?, Sync.SyncError>;
global using SyncClientError = Outcome.Result<Sync.SyncClient?, Sync.SyncError>.Error<
    Sync.SyncClient?,
    Sync.SyncError
>;
global using SyncClientListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncClient>,
    Sync.SyncError
>.Error<System.Collections.Generic.IReadOnlyList<Sync.SyncClient>, Sync.SyncError>;
global using SyncClientListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncClient>,
    Sync.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<Sync.SyncClient>, Sync.SyncError>;
global using SyncClientListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncClient>,
    Sync.SyncError
>;
global using SyncClientOk = Outcome.Result<Sync.SyncClient?, Sync.SyncError>.Ok<
    Sync.SyncClient?,
    Sync.SyncError
>;
global using SyncClientResult = Outcome.Result<Sync.SyncClient?, Sync.SyncError>;
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
