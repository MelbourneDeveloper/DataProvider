global using System;
global using System.Collections.Generic;
global using System.Collections.Immutable;
global using System.Data;
global using DataProvider;
global using Generated;
global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Outcome;
global using Selecta;
global using Sync;
global using Sync.SQLite;

#pragma warning disable IDE0005
// Sync result type aliases
global using StringSyncError = Outcome.Result<string, Sync.SyncError>.Error<string, Sync.SyncError>;
global using StringSyncOk = Outcome.Result<string, Sync.SyncError>.Ok<string, Sync.SyncError>;
global using SyncLogListError = Outcome.Result<System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>, Sync.SyncError>.Error<System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>, Sync.SyncError>;
global using SyncLogListOk = Outcome.Result<System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>, Sync.SyncError>.Ok<System.Collections.Generic.IReadOnlyList<Sync.SyncLogEntry>, Sync.SyncError>;

// Insert result type aliases
global using InsertOk = Outcome.Result<long, Selecta.SqlError>.Ok<long, Selecta.SqlError>;
global using InsertError = Outcome.Result<long, Selecta.SqlError>.Error<long, Selecta.SqlError>;

// GetPatients query result type aliases
global using GetPatientsOk = Outcome.Result<System.Collections.Immutable.ImmutableList<Generated.GetPatients>, Selecta.SqlError>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetPatients>, Selecta.SqlError>;
global using GetPatientsError = Outcome.Result<System.Collections.Immutable.ImmutableList<Generated.GetPatients>, Selecta.SqlError>.Error<System.Collections.Immutable.ImmutableList<Generated.GetPatients>, Selecta.SqlError>;

// GetPatientById query result type aliases
global using GetPatientByIdOk = Outcome.Result<System.Collections.Immutable.ImmutableList<Generated.GetPatientById>, Selecta.SqlError>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetPatientById>, Selecta.SqlError>;
global using GetPatientByIdError = Outcome.Result<System.Collections.Immutable.ImmutableList<Generated.GetPatientById>, Selecta.SqlError>.Error<System.Collections.Immutable.ImmutableList<Generated.GetPatientById>, Selecta.SqlError>;

// SearchPatients query result type aliases
global using SearchPatientsOk = Outcome.Result<System.Collections.Immutable.ImmutableList<Generated.SearchPatients>, Selecta.SqlError>.Ok<System.Collections.Immutable.ImmutableList<Generated.SearchPatients>, Selecta.SqlError>;
global using SearchPatientsError = Outcome.Result<System.Collections.Immutable.ImmutableList<Generated.SearchPatients>, Selecta.SqlError>.Error<System.Collections.Immutable.ImmutableList<Generated.SearchPatients>, Selecta.SqlError>;

// GetEncountersByPatient query result type aliases
global using GetEncountersOk = Outcome.Result<System.Collections.Immutable.ImmutableList<Generated.GetEncountersByPatient>, Selecta.SqlError>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetEncountersByPatient>, Selecta.SqlError>;
global using GetEncountersError = Outcome.Result<System.Collections.Immutable.ImmutableList<Generated.GetEncountersByPatient>, Selecta.SqlError>.Error<System.Collections.Immutable.ImmutableList<Generated.GetEncountersByPatient>, Selecta.SqlError>;

// GetConditionsByPatient query result type aliases
global using GetConditionsOk = Outcome.Result<System.Collections.Immutable.ImmutableList<Generated.GetConditionsByPatient>, Selecta.SqlError>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetConditionsByPatient>, Selecta.SqlError>;
global using GetConditionsError = Outcome.Result<System.Collections.Immutable.ImmutableList<Generated.GetConditionsByPatient>, Selecta.SqlError>.Error<System.Collections.Immutable.ImmutableList<Generated.GetConditionsByPatient>, Selecta.SqlError>;

// GetMedicationsByPatient query result type aliases
global using GetMedicationsOk = Outcome.Result<System.Collections.Immutable.ImmutableList<Generated.GetMedicationsByPatient>, Selecta.SqlError>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetMedicationsByPatient>, Selecta.SqlError>;
global using GetMedicationsError = Outcome.Result<System.Collections.Immutable.ImmutableList<Generated.GetMedicationsByPatient>, Selecta.SqlError>.Error<System.Collections.Immutable.ImmutableList<Generated.GetMedicationsByPatient>, Selecta.SqlError>;
#pragma warning restore IDE0005
