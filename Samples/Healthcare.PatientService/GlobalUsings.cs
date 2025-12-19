global using System.Collections.Generic;
global using System.Data;
global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Sync;
global using Sync.SQLite;
global using DataProvider;
global using Outcome;
global using Selecta;
global using SyncLogListOk = Outcome.Result<
    IReadOnlyList<SyncLogEntry>,
    SyncError
>.Ok<IReadOnlyList<SyncLogEntry>, SyncError>;
global using SyncLogListError = Outcome.Result<
    IReadOnlyList<SyncLogEntry>,
    SyncError
>.Error<IReadOnlyList<SyncLogEntry>, SyncError>;
global using StringSyncOk = Outcome.Result<string, SyncError>.Ok<string, SyncError>;
global using StringSyncError = Outcome.Result<string, SyncError>.Error<string, SyncError>;
