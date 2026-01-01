using System.Collections.Immutable;
using System.Globalization;

namespace Scheduling.Api;

/// <summary>
/// Conduit handlers for Scheduling API endpoints.
/// </summary>
public static class SchedulingHandlers
{
    /// <summary>
    /// Handles GetAllPractitioners request.
    /// </summary>
    public static async Task<
        Result<ImmutableList<GetAllPractitioners>, ConduitError>
    > HandleGetAllPractitioners(
        GetAllPractitionersQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.GetAllPractitionersAsync().ConfigureAwait(false);

            return result switch
            {
                GetAllPractitionersOk ok => new Result<
                    ImmutableList<GetAllPractitioners>,
                    ConduitError
                >.Ok<ImmutableList<GetAllPractitioners>, ConduitError>(ok.Value),
                GetAllPractitionersError err => new Result<
                    ImmutableList<GetAllPractitioners>,
                    ConduitError
                >.Error<ImmutableList<GetAllPractitioners>, ConduitError>(
                    new ConduitErrorHandlerFailed("GetAllPractitioners", err.Value.Message, null)
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<ImmutableList<GetAllPractitioners>, ConduitError>.Error<
                ImmutableList<GetAllPractitioners>,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("GetAllPractitioners", ex));
        }
    }

    /// <summary>
    /// Handles GetPractitionerById request.
    /// </summary>
    public static async Task<
        Result<GetPractitionerByIdResult, ConduitError>
    > HandleGetPractitionerById(
        GetPractitionerByIdQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.GetPractitionerByIdAsync(id: request.Id).ConfigureAwait(false);

            return result switch
            {
                GetPractitionerByIdOk ok when ok.Value.Count > 0 => new Result<
                    GetPractitionerByIdResult,
                    ConduitError
                >.Ok<GetPractitionerByIdResult, ConduitError>(
                    new GetPractitionerByIdResult(Practitioner: ok.Value[0], Found: true)
                ),
                GetPractitionerByIdOk => new Result<GetPractitionerByIdResult, ConduitError>.Ok<
                    GetPractitionerByIdResult,
                    ConduitError
                >(new GetPractitionerByIdResult(Practitioner: null, Found: false)),
                GetPractitionerByIdError err => new Result<
                    GetPractitionerByIdResult,
                    ConduitError
                >.Error<GetPractitionerByIdResult, ConduitError>(
                    new ConduitErrorHandlerFailed("GetPractitionerById", err.Value.Message, null)
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<GetPractitionerByIdResult, ConduitError>.Error<
                GetPractitionerByIdResult,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("GetPractitionerById", ex));
        }
    }

    /// <summary>
    /// Handles CreatePractitioner request.
    /// </summary>
    public static async Task<Result<CreatedPractitioner, ConduitError>> HandleCreatePractitioner(
        CreatePractitionerCommand request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var transaction = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using var _ = transaction.ConfigureAwait(false);

            var id = Guid.NewGuid().ToString();

            var result = await transaction
                .Insertfhir_PractitionerAsync(
                    id: id,
                    identifier: request.Request.Identifier,
                    active: 1L,
                    namefamily: request.Request.NameFamily,
                    namegiven: request.Request.NameGiven,
                    qualification: request.Request.Qualification ?? string.Empty,
                    specialty: request.Request.Specialty ?? string.Empty,
                    telecomemail: request.Request.TelecomEmail ?? string.Empty,
                    telecomphone: request.Request.TelecomPhone ?? string.Empty
                )
                .ConfigureAwait(false);

            return result switch
            {
                InsertOk => await CommitAndReturnPractitioner(transaction, id, request.Request),
                InsertError err => new Result<CreatedPractitioner, ConduitError>.Error<
                    CreatedPractitioner,
                    ConduitError
                >(new ConduitErrorHandlerFailed("CreatePractitioner", err.Value.Message, null)),
            };
        }
        catch (Exception ex)
        {
            return new Result<CreatedPractitioner, ConduitError>.Error<
                CreatedPractitioner,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("CreatePractitioner", ex));
        }
    }

    /// <summary>
    /// Handles SearchPractitionersBySpecialty request.
    /// </summary>
    public static async Task<
        Result<ImmutableList<SearchPractitionersBySpecialty>, ConduitError>
    > HandleSearchPractitioners(
        SearchPractitionersQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.SearchPractitionersBySpecialtyAsync(
                    specialty: request.Specialty
                )
                .ConfigureAwait(false);

            return result switch
            {
                SearchPractitionersOk ok => new Result<
                    ImmutableList<SearchPractitionersBySpecialty>,
                    ConduitError
                >.Ok<ImmutableList<SearchPractitionersBySpecialty>, ConduitError>(ok.Value),
                SearchPractitionersError err => new Result<
                    ImmutableList<SearchPractitionersBySpecialty>,
                    ConduitError
                >.Error<ImmutableList<SearchPractitionersBySpecialty>, ConduitError>(
                    new ConduitErrorHandlerFailed("SearchPractitioners", err.Value.Message, null)
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<ImmutableList<SearchPractitionersBySpecialty>, ConduitError>.Error<
                ImmutableList<SearchPractitionersBySpecialty>,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("SearchPractitioners", ex));
        }
    }

    /// <summary>
    /// Handles GetUpcomingAppointments request.
    /// </summary>
    public static async Task<
        Result<ImmutableList<GetUpcomingAppointments>, ConduitError>
    > HandleGetUpcomingAppointments(
        GetUpcomingAppointmentsQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.GetUpcomingAppointmentsAsync().ConfigureAwait(false);

            return result switch
            {
                GetUpcomingAppointmentsOk ok => new Result<
                    ImmutableList<GetUpcomingAppointments>,
                    ConduitError
                >.Ok<ImmutableList<GetUpcomingAppointments>, ConduitError>(ok.Value),
                GetUpcomingAppointmentsError err => new Result<
                    ImmutableList<GetUpcomingAppointments>,
                    ConduitError
                >.Error<ImmutableList<GetUpcomingAppointments>, ConduitError>(
                    new ConduitErrorHandlerFailed(
                        "GetUpcomingAppointments",
                        err.Value.Message,
                        null
                    )
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<ImmutableList<GetUpcomingAppointments>, ConduitError>.Error<
                ImmutableList<GetUpcomingAppointments>,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("GetUpcomingAppointments", ex));
        }
    }

    /// <summary>
    /// Handles GetAppointmentById request.
    /// </summary>
    public static async Task<
        Result<GetAppointmentByIdResult, ConduitError>
    > HandleGetAppointmentById(
        GetAppointmentByIdQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.GetAppointmentByIdAsync(id: request.Id).ConfigureAwait(false);

            return result switch
            {
                GetAppointmentByIdOk ok when ok.Value.Count > 0 => new Result<
                    GetAppointmentByIdResult,
                    ConduitError
                >.Ok<GetAppointmentByIdResult, ConduitError>(
                    new GetAppointmentByIdResult(Appointment: ok.Value[0], Found: true)
                ),
                GetAppointmentByIdOk => new Result<GetAppointmentByIdResult, ConduitError>.Ok<
                    GetAppointmentByIdResult,
                    ConduitError
                >(new GetAppointmentByIdResult(Appointment: null, Found: false)),
                GetAppointmentByIdError err => new Result<
                    GetAppointmentByIdResult,
                    ConduitError
                >.Error<GetAppointmentByIdResult, ConduitError>(
                    new ConduitErrorHandlerFailed("GetAppointmentById", err.Value.Message, null)
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<GetAppointmentByIdResult, ConduitError>.Error<
                GetAppointmentByIdResult,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("GetAppointmentById", ex));
        }
    }

    /// <summary>
    /// Handles CreateAppointment request.
    /// </summary>
    public static async Task<Result<CreatedAppointment, ConduitError>> HandleCreateAppointment(
        CreateAppointmentCommand request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var transaction = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using var _ = transaction.ConfigureAwait(false);

            var id = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                CultureInfo.InvariantCulture
            );
            var start = DateTime.Parse(request.Request.Start, CultureInfo.InvariantCulture);
            var end = DateTime.Parse(request.Request.End, CultureInfo.InvariantCulture);
            var durationMinutes = (int)(end - start).TotalMinutes;

            var result = await transaction
                .Insertfhir_AppointmentAsync(
                    id: id,
                    status: "booked",
                    servicecategory: request.Request.ServiceCategory ?? string.Empty,
                    servicetype: request.Request.ServiceType ?? string.Empty,
                    reasoncode: request.Request.ReasonCode ?? string.Empty,
                    priority: request.Request.Priority,
                    description: request.Request.Description ?? string.Empty,
                    starttime: request.Request.Start,
                    endtime: request.Request.End,
                    minutesduration: durationMinutes,
                    patientreference: request.Request.PatientReference,
                    practitionerreference: request.Request.PractitionerReference,
                    created: now,
                    comment: request.Request.Comment ?? string.Empty
                )
                .ConfigureAwait(false);

            return result switch
            {
                InsertOk => await CommitAndReturnAppointment(
                    transaction,
                    id,
                    request.Request,
                    now,
                    durationMinutes
                ),
                InsertError err => new Result<CreatedAppointment, ConduitError>.Error<
                    CreatedAppointment,
                    ConduitError
                >(
                    new ConduitErrorHandlerFailed(
                        "CreateAppointment",
                        err.Value.DetailedMessage,
                        null
                    )
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<CreatedAppointment, ConduitError>.Error<
                CreatedAppointment,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("CreateAppointment", ex));
        }
    }

    /// <summary>
    /// Handles GetAppointmentsByPatient request.
    /// </summary>
    public static async Task<
        Result<ImmutableList<GetAppointmentsByPatient>, ConduitError>
    > HandleGetAppointmentsByPatient(
        GetAppointmentsByPatientQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.GetAppointmentsByPatientAsync(
                    patientReference: $"Patient/{request.PatientId}"
                )
                .ConfigureAwait(false);

            return result switch
            {
                GetAppointmentsByPatientOk ok => new Result<
                    ImmutableList<GetAppointmentsByPatient>,
                    ConduitError
                >.Ok<ImmutableList<GetAppointmentsByPatient>, ConduitError>(ok.Value),
                GetAppointmentsByPatientError err => new Result<
                    ImmutableList<GetAppointmentsByPatient>,
                    ConduitError
                >.Error<ImmutableList<GetAppointmentsByPatient>, ConduitError>(
                    new ConduitErrorHandlerFailed(
                        "GetAppointmentsByPatient",
                        err.Value.Message,
                        null
                    )
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<ImmutableList<GetAppointmentsByPatient>, ConduitError>.Error<
                ImmutableList<GetAppointmentsByPatient>,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("GetAppointmentsByPatient", ex));
        }
    }

    /// <summary>
    /// Handles GetAppointmentsByPractitioner request.
    /// </summary>
    public static async Task<
        Result<ImmutableList<GetAppointmentsByPractitioner>, ConduitError>
    > HandleGetAppointmentsByPractitioner(
        GetAppointmentsByPractitionerQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var result = await conn.GetAppointmentsByPractitionerAsync(
                    practitionerReference: $"Practitioner/{request.PractitionerId}"
                )
                .ConfigureAwait(false);

            return result switch
            {
                GetAppointmentsByPractitionerOk ok => new Result<
                    ImmutableList<GetAppointmentsByPractitioner>,
                    ConduitError
                >.Ok<ImmutableList<GetAppointmentsByPractitioner>, ConduitError>(ok.Value),
                GetAppointmentsByPractitionerError err => new Result<
                    ImmutableList<GetAppointmentsByPractitioner>,
                    ConduitError
                >.Error<ImmutableList<GetAppointmentsByPractitioner>, ConduitError>(
                    new ConduitErrorHandlerFailed(
                        "GetAppointmentsByPractitioner",
                        err.Value.Message,
                        null
                    )
                ),
            };
        }
        catch (Exception ex)
        {
            return new Result<ImmutableList<GetAppointmentsByPractitioner>, ConduitError>.Error<
                ImmutableList<GetAppointmentsByPractitioner>,
                ConduitError
            >(ConduitErrorHandlerFailed.FromException("GetAppointmentsByPractitioner", ex));
        }
    }

    /// <summary>
    /// Handles GetSyncChanges request.
    /// </summary>
    public static Task<Result<SyncChangesResult, ConduitError>> HandleGetSyncChanges(
        GetSyncChangesQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var result = SyncLogRepository.FetchChanges(
                conn,
                fromVersion: request.FromVersion,
                limit: request.Limit
            );

            return Task.FromResult<Result<SyncChangesResult, ConduitError>>(
                result switch
                {
                    SyncLogListOk ok => new Result<SyncChangesResult, ConduitError>.Ok<
                        SyncChangesResult,
                        ConduitError
                    >(new SyncChangesResult(Changes: ok.Value)),
                    SyncLogListError err => new Result<SyncChangesResult, ConduitError>.Error<
                        SyncChangesResult,
                        ConduitError
                    >(
                        new ConduitErrorHandlerFailed(
                            "GetSyncChanges",
                            SyncHelpers.ToMessage(err.Value),
                            null
                        )
                    ),
                }
            );
        }
        catch (Exception ex)
        {
            return Task.FromResult<Result<SyncChangesResult, ConduitError>>(
                new Result<SyncChangesResult, ConduitError>.Error<SyncChangesResult, ConduitError>(
                    ConduitErrorHandlerFailed.FromException("GetSyncChanges", ex)
                )
            );
        }
    }

    /// <summary>
    /// Handles GetSyncOrigin request.
    /// </summary>
    public static Task<Result<SyncOriginResult, ConduitError>> HandleGetSyncOrigin(
        GetSyncOriginQuery request,
        Func<SqliteConnection> getConn,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = getConn();
            var result = SyncSchema.GetOriginId(conn);

            return Task.FromResult<Result<SyncOriginResult, ConduitError>>(
                result switch
                {
                    StringSyncOk ok => new Result<SyncOriginResult, ConduitError>.Ok<
                        SyncOriginResult,
                        ConduitError
                    >(new SyncOriginResult(OriginId: ok.Value)),
                    StringSyncError err => new Result<SyncOriginResult, ConduitError>.Error<
                        SyncOriginResult,
                        ConduitError
                    >(
                        new ConduitErrorHandlerFailed(
                            "GetSyncOrigin",
                            SyncHelpers.ToMessage(err.Value),
                            null
                        )
                    ),
                }
            );
        }
        catch (Exception ex)
        {
            return Task.FromResult<Result<SyncOriginResult, ConduitError>>(
                new Result<SyncOriginResult, ConduitError>.Error<SyncOriginResult, ConduitError>(
                    ConduitErrorHandlerFailed.FromException("GetSyncOrigin", ex)
                )
            );
        }
    }

    private static async Task<
        Result<CreatedPractitioner, ConduitError>
    > CommitAndReturnPractitioner(
        System.Data.Common.DbTransaction transaction,
        string id,
        CreatePractitionerRequest request
    )
    {
        await transaction.CommitAsync().ConfigureAwait(false);
        return new Result<CreatedPractitioner, ConduitError>.Ok<CreatedPractitioner, ConduitError>(
            new CreatedPractitioner(
                Id: id,
                Identifier: request.Identifier,
                Active: true,
                NameFamily: request.NameFamily,
                NameGiven: request.NameGiven,
                Qualification: request.Qualification,
                Specialty: request.Specialty,
                TelecomEmail: request.TelecomEmail,
                TelecomPhone: request.TelecomPhone
            )
        );
    }

    private static async Task<Result<CreatedAppointment, ConduitError>> CommitAndReturnAppointment(
        System.Data.Common.DbTransaction transaction,
        string id,
        CreateAppointmentRequest request,
        string now,
        int durationMinutes
    )
    {
        await transaction.CommitAsync().ConfigureAwait(false);
        return new Result<CreatedAppointment, ConduitError>.Ok<CreatedAppointment, ConduitError>(
            new CreatedAppointment(
                Id: id,
                Status: "booked",
                ServiceCategory: request.ServiceCategory,
                ServiceType: request.ServiceType,
                ReasonCode: request.ReasonCode,
                Priority: request.Priority,
                Description: request.Description,
                Start: request.Start,
                End: request.End,
                MinutesDuration: durationMinutes,
                PatientReference: request.PatientReference,
                PractitionerReference: request.PractitionerReference,
                Created: now,
                Comment: request.Comment
            )
        );
    }
}

// ============================================================================
// CONDUIT QUERY/COMMAND RECORDS
// ============================================================================

/// <summary>Query to get all practitioners.</summary>
public sealed record GetAllPractitionersQuery;

/// <summary>Query to get practitioner by ID.</summary>
public sealed record GetPractitionerByIdQuery(string Id);

/// <summary>Result of GetPractitionerById.</summary>
public sealed record GetPractitionerByIdResult(GetPractitionerById? Practitioner, bool Found);

/// <summary>Command to create a practitioner.</summary>
public sealed record CreatePractitionerCommand(CreatePractitionerRequest Request);

/// <summary>Result of created practitioner.</summary>
public sealed record CreatedPractitioner(
    string Id,
    string Identifier,
    bool Active,
    string NameFamily,
    string NameGiven,
    string? Qualification,
    string? Specialty,
    string? TelecomEmail,
    string? TelecomPhone
);

/// <summary>Query to search practitioners by specialty.</summary>
public sealed record SearchPractitionersQuery(string Specialty);

/// <summary>Query to get upcoming appointments.</summary>
public sealed record GetUpcomingAppointmentsQuery;

/// <summary>Query to get appointment by ID.</summary>
public sealed record GetAppointmentByIdQuery(string Id);

/// <summary>Result of GetAppointmentById.</summary>
public sealed record GetAppointmentByIdResult(GetAppointmentById? Appointment, bool Found);

/// <summary>Command to create an appointment.</summary>
public sealed record CreateAppointmentCommand(CreateAppointmentRequest Request);

/// <summary>Result of created appointment.</summary>
public sealed record CreatedAppointment(
    string Id,
    string Status,
    string? ServiceCategory,
    string? ServiceType,
    string? ReasonCode,
    string Priority,
    string? Description,
    string Start,
    string End,
    int MinutesDuration,
    string PatientReference,
    string PractitionerReference,
    string Created,
    string? Comment
);

/// <summary>Query to get appointments by patient.</summary>
public sealed record GetAppointmentsByPatientQuery(string PatientId);

/// <summary>Query to get appointments by practitioner.</summary>
public sealed record GetAppointmentsByPractitionerQuery(string PractitionerId);

/// <summary>Query to get sync changes.</summary>
public sealed record GetSyncChangesQuery(long FromVersion, int Limit);

/// <summary>Result of sync changes.</summary>
public sealed record SyncChangesResult(ImmutableList<SyncLogEntry> Changes);

/// <summary>Query to get sync origin.</summary>
public sealed record GetSyncOriginQuery;

/// <summary>Result of sync origin.</summary>
public sealed record SyncOriginResult(string OriginId);
