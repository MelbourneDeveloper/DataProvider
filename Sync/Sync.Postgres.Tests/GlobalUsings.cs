#pragma warning disable IDE0005 // Using directive is unnecessary - these are used for pattern matching

global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using Npgsql;
global using Sync;
global using Sync.Postgres;
global using Sync.SQLite;
global using Testcontainers.PostgreSql;
global using Xunit;

// Type aliases for Result types - matching Sync patterns using Outcome package
global using BatchApplyResultOk = Outcome.Result<Sync.BatchApplyResult, Sync.SyncError>.Ok<
    Sync.BatchApplyResult,
    Sync.SyncError
>;
global using BoolSyncError = Outcome.Result<bool, Sync.SyncError>.Error<bool, Sync.SyncError>;
global using BoolSyncOk = Outcome.Result<bool, Sync.SyncError>.Ok<bool, Sync.SyncError>;
global using BoolSyncResult = Outcome.Result<bool, Sync.SyncError>;
global using LongSyncOk = Outcome.Result<long, Sync.SyncError>.Ok<long, Sync.SyncError>;
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
