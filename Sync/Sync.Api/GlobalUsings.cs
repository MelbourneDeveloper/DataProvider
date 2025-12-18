#pragma warning disable IDE0005 // Using directive is unnecessary - these are used for pattern matching

// Type aliases for Result types to reduce verbosity in Sync.Api
global using BoolSyncError = Outcome.Result<bool, Sync.SyncError>.Error<bool, Sync.SyncError>;
global using BoolSyncOk = Outcome.Result<bool, Sync.SyncError>.Ok<bool, Sync.SyncError>;
global using BoolSyncResult = Outcome.Result<bool, Sync.SyncError>;
global using LongSyncError = Outcome.Result<long, Sync.SyncError>.Error<long, Sync.SyncError>;
global using LongSyncOk = Outcome.Result<long, Sync.SyncError>.Ok<long, Sync.SyncError>;
global using LongSyncResult = Outcome.Result<long, Sync.SyncError>;
global using SyncClientListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncClient>,
    Sync.SyncError
>.Error<System.Collections.Generic.IReadOnlyList<Sync.SyncClient>, Sync.SyncError>;
global using SyncClientListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncClient>,
    Sync.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<Sync.SyncClient>, Sync.SyncError>;
global using SyncLogListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>,
    Sync.SyncError
>.Error<System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>, Sync.SyncError>;
global using SyncLogListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>,
    Sync.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>, Sync.SyncError>;
