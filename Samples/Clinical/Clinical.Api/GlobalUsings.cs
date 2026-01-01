global using System;
global using Generated;
global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Outcome;
global using Sync;
global using Sync.SQLite;
// GetConditionsByPatient query result type aliases
// GetEncountersByPatient query result type aliases
// GetMedicationsByPatient query result type aliases
// GetPatientById query result type aliases
// GetPatients query result type aliases
global using InsertError = Outcome.Result<int, Selecta.SqlError>.Error<int, Selecta.SqlError>;
// Insert result type aliases
global using InsertOk = Outcome.Result<int, Selecta.SqlError>.Ok<int, Selecta.SqlError>;
// SearchPatients query result type aliases
// Sync result type aliases
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
// Update result type aliases
global using UpdateOk = Outcome.Result<int, Selecta.SqlError>.Ok<int, Selecta.SqlError>;
