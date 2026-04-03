global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using Npgsql;
global using Nimblesite.Sync.SQLite;
global using Testcontainers.PostgreSql;
global using Xunit;
// Type aliases for Result types - matching Nimblesite.Sync.Core patterns using Outcome package
global using BatchApplyResultOk = Outcome.Result<Nimblesite.Sync.Core.BatchApplyResult, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<
    Nimblesite.Sync.Core.BatchApplyResult,
    Nimblesite.Sync.Core.Nimblesite.Sync.CoreError
>;
global using BoolSyncOk = Outcome.Result<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<bool, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using LongSyncOk = Outcome.Result<long, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<long, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
global using StringSyncOk = Outcome.Result<string, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>.Ok<string, Nimblesite.Sync.Core.Nimblesite.Sync.CoreError>;
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
