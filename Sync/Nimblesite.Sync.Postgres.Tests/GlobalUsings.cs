global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using Nimblesite.Sync.Core;
global using Nimblesite.Sync.SQLite;
global using Npgsql;
global using Testcontainers.PostgreSql;
global using Xunit;
// Type aliases for Result types - matching Sync patterns using Outcome package
global using BatchApplyResultOk = Outcome.Result<
    Nimblesite.Sync.Core.BatchApplyResult,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.BatchApplyResult, Nimblesite.Sync.Core.SyncError>;
global using BoolSyncOk = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Ok<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
global using LongSyncOk = Outcome.Result<long, Nimblesite.Sync.Core.SyncError>.Ok<
    long,
    Nimblesite.Sync.Core.SyncError
>;
global using StringSyncOk = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>.Ok<
    string,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncClientListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncClientOk = Outcome.Result<
    Nimblesite.Sync.Core.SyncClient?,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.SyncClient?, Nimblesite.Sync.Core.SyncError>;
global using SyncLogListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>;
