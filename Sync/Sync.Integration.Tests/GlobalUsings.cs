#pragma warning disable IDE0005 // Using directive is unnecessary - these are used for pattern matching

global using System.Net.Http.Json;
global using System.Text.Json;
global using Microsoft.AspNetCore.Mvc.Testing;
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
global using BoolSyncOk = Outcome.Result<bool, Sync.SyncError>.Ok<bool, Sync.SyncError>;
global using StringSyncOk = Outcome.Result<string, Sync.SyncError>.Ok<string, Sync.SyncError>;
global using SyncLogListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>,
    Sync.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>, Sync.SyncError>;
