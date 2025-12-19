global using System;
global using System.Collections.Generic;
global using System.Data;
global using DataProvider;
global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Outcome;
global using Selecta;
global using Sync;
global using Sync.SQLite;
global using StringSyncError = Outcome.Result<string, Sync.SyncError>.Error<string, Sync.SyncError>;
global using StringSyncOk = Outcome.Result<string, Sync.SyncError>.Ok<string, Sync.SyncError>;
global using SyncLogListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>,
    Sync.SyncError
>.Error<System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>, Sync.SyncError>;
global using SyncLogListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>,
    Sync.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>, Sync.SyncError>;
