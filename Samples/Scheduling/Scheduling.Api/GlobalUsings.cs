global using System;
global using System.Collections.Immutable;
global using System.Data;
global using Generated;
global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Outcome;
global using Selecta;
global using Sync;
global using Sync.SQLite;
// Sync result type aliases
global using BoolSyncError = Outcome.Result<bool, Sync.SyncError>.Error<bool, Sync.SyncError>;
global using GetAllPractitionersError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAllPractitioners>,
    Selecta.SqlError
>.Error<
    System.Collections.Immutable.ImmutableList<Generated.GetAllPractitioners>,
    Selecta.SqlError
>;
// GetAllPractitioners query result type aliases
global using GetAllPractitionersOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAllPractitioners>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetAllPractitioners>, Selecta.SqlError>;
global using GetAppointmentByIdError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAppointmentById>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.GetAppointmentById>, Selecta.SqlError>;
// GetAppointmentById query result type aliases
global using GetAppointmentByIdOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAppointmentById>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetAppointmentById>, Selecta.SqlError>;
global using GetAppointmentsByPatientError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAppointmentsByPatient>,
    Selecta.SqlError
>.Error<
    System.Collections.Immutable.ImmutableList<Generated.GetAppointmentsByPatient>,
    Selecta.SqlError
>;
// GetAppointmentsByPatient query result type aliases
global using GetAppointmentsByPatientOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAppointmentsByPatient>,
    Selecta.SqlError
>.Ok<
    System.Collections.Immutable.ImmutableList<Generated.GetAppointmentsByPatient>,
    Selecta.SqlError
>;
global using GetAppointmentsByPractitionerError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAppointmentsByPractitioner>,
    Selecta.SqlError
>.Error<
    System.Collections.Immutable.ImmutableList<Generated.GetAppointmentsByPractitioner>,
    Selecta.SqlError
>;
// GetAppointmentsByPractitioner query result type aliases
global using GetAppointmentsByPractitionerOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAppointmentsByPractitioner>,
    Selecta.SqlError
>.Ok<
    System.Collections.Immutable.ImmutableList<Generated.GetAppointmentsByPractitioner>,
    Selecta.SqlError
>;
global using GetPractitionerByIdError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetPractitionerById>,
    Selecta.SqlError
>.Error<
    System.Collections.Immutable.ImmutableList<Generated.GetPractitionerById>,
    Selecta.SqlError
>;
// GetPractitionerById query result type aliases
global using GetPractitionerByIdOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetPractitionerById>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.GetPractitionerById>, Selecta.SqlError>;
global using GetUpcomingAppointmentsError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetUpcomingAppointments>,
    Selecta.SqlError
>.Error<
    System.Collections.Immutable.ImmutableList<Generated.GetUpcomingAppointments>,
    Selecta.SqlError
>;
// GetUpcomingAppointments query result type aliases
global using GetUpcomingAppointmentsOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetUpcomingAppointments>,
    Selecta.SqlError
>.Ok<
    System.Collections.Immutable.ImmutableList<Generated.GetUpcomingAppointments>,
    Selecta.SqlError
>;
global using InsertError = Outcome.Result<long, Selecta.SqlError>.Error<long, Selecta.SqlError>;
// Insert result type aliases
global using InsertOk = Outcome.Result<long, Selecta.SqlError>.Ok<long, Selecta.SqlError>;
global using SearchPractitionersError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.SearchPractitionersBySpecialty>,
    Selecta.SqlError
>.Error<
    System.Collections.Immutable.ImmutableList<Generated.SearchPractitionersBySpecialty>,
    Selecta.SqlError
>;
// SearchPractitionersBySpecialty query result type aliases
global using SearchPractitionersOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.SearchPractitionersBySpecialty>,
    Selecta.SqlError
>.Ok<
    System.Collections.Immutable.ImmutableList<Generated.SearchPractitionersBySpecialty>,
    Selecta.SqlError
>;
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
