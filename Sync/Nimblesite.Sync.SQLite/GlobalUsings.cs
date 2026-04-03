// Type aliases for Result types to reduce verbosity in Nimblesite.Sync.SQLite
global using BoolSyncError = Outcome.Result<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using BoolSyncOk = Outcome.Result<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using BoolSyncResult = Outcome.Result<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using ColumnInfoListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Error<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using ColumnInfoListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Ok<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using ColumnInfoListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using IntSyncError = Outcome.Result<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using IntSyncOk = Outcome.Result<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using IntSyncResult = Outcome.Result<int, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using LongSyncError = Outcome.Result<long, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<long, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using LongSyncOk = Outcome.Result<long, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<long, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using LongSyncResult = Outcome.Result<long, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using MappingStateError = Outcome.Result<Nimblesite.Sync.Core.MappingStateEntry?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.MappingStateEntry?,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using MappingStateListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Error<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using MappingStateListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Ok<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using MappingStateListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using MappingStateOk = Outcome.Result<Nimblesite.Sync.Core.MappingStateEntry?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.MappingStateEntry?,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using MappingStateResult = Outcome.Result<Nimblesite.Sync.Core.MappingStateEntry?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using RecordHashError = Outcome.Result<Nimblesite.Sync.Core.RecordHashEntry?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.RecordHashEntry?,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using RecordHashOk = Outcome.Result<Nimblesite.Sync.Core.RecordHashEntry?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.RecordHashEntry?,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using RecordHashResult = Outcome.Result<Nimblesite.Sync.Core.RecordHashEntry?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using StringSyncError = Outcome.Result<string, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<string, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using StringSyncOk = Outcome.Result<string, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<string, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using StringSyncResult = Outcome.Result<string, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using SubscriptionError = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription?,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using SubscriptionListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Error<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using SubscriptionListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Ok<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using SubscriptionListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using SubscriptionOk = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription?,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using SubscriptionResult = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreSubscription?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using Nimblesite.Sync.CoreClientError = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Error<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient?,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreClientListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Error<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using Nimblesite.Sync.CoreClientListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>.Ok<System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient>, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using Nimblesite.Sync.CoreClientListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient>,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreClientOk = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient?,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using Nimblesite.Sync.CoreClientResult = Outcome.Result<Nimblesite.Sync.Core.Nimblesite.Sync.CoreClient?, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
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
