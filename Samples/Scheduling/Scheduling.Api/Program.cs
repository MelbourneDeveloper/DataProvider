using System.Collections.Immutable;
using System.Globalization;
using Generated;
using Scheduling.Api;
using Selecta;

var builder = WebApplication.CreateBuilder(args);

var dbPath = Path.Combine(AppContext.BaseDirectory, "scheduling.db");
var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
builder.Services.AddSingleton<Func<SqliteConnection>>(() =>
{
    var conn = new SqliteConnection(connectionString);
    conn.Open();
    return conn;
});

var app = builder.Build();

using (var conn = new SqliteConnection(connectionString))
{
    conn.Open();
    DatabaseSetup.Initialize(conn, app.Logger);
}

// === FHIR PRACTITIONER ENDPOINTS ===

app.MapGet(
    "/Practitioner",
    async (Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetAllPractitionersAsync().ConfigureAwait(false);
        return result switch
        {
            Result<ImmutableList<GetAllPractitioners>, SqlError>.Ok<
                ImmutableList<GetAllPractitioners>,
                SqlError
            > ok => Results.Ok(ok.Value),
            Result<ImmutableList<GetAllPractitioners>, SqlError>.Error<
                ImmutableList<GetAllPractitioners>,
                SqlError
            > err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapGet(
    "/Practitioner/{id}",
    async (string id, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetPractitionerByIdAsync(id).ConfigureAwait(false);
        return result switch
        {
            Result<ImmutableList<GetPractitionerById>, SqlError>.Ok<
                ImmutableList<GetPractitionerById>,
                SqlError
            > ok when ok.Value.Count > 0 => Results.Ok(ok.Value[0]),
            Result<ImmutableList<GetPractitionerById>, SqlError>.Ok<
                ImmutableList<GetPractitionerById>,
                SqlError
            > => Results.NotFound(),
            Result<ImmutableList<GetPractitionerById>, SqlError>.Error<
                ImmutableList<GetPractitionerById>,
                SqlError
            > err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapPost(
    "/Practitioner",
    async (CreatePractitionerRequest request, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var transaction = await conn.BeginTransactionAsync().ConfigureAwait(false);
        await using var _ = transaction.ConfigureAwait(false);
        var id = Guid.NewGuid().ToString();

        var result = await transaction
            .Insertfhir_PractitionerAsync(
                id,
                request.Identifier,
                1L,
                request.NameFamily,
                request.NameGiven,
                request.Qualification ?? string.Empty,
                request.Specialty ?? string.Empty,
                request.TelecomEmail ?? string.Empty,
                request.TelecomPhone ?? string.Empty
            )
            .ConfigureAwait(false);

        if (result is Result<long, SqlError>.Ok<long, SqlError>)
        {
            await transaction.CommitAsync().ConfigureAwait(false);
            return Results.Created(
                $"/Practitioner/{id}",
                new
                {
                    Id = id,
                    Identifier = request.Identifier,
                    Active = true,
                    NameFamily = request.NameFamily,
                    NameGiven = request.NameGiven,
                    Qualification = request.Qualification,
                    Specialty = request.Specialty,
                    TelecomEmail = request.TelecomEmail,
                    TelecomPhone = request.TelecomPhone
                }
            );
        }

        return result switch
        {
            Result<long, SqlError>.Error<long, SqlError> err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapGet(
    "/Practitioner/_search",
    async (string? specialty, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();

        if (specialty is not null)
        {
            var result = await conn.SearchPractitionersBySpecialtyAsync(specialty)
                .ConfigureAwait(false);
            return result switch
            {
                Result<ImmutableList<SearchPractitionersBySpecialty>, SqlError>.Ok<
                    ImmutableList<SearchPractitionersBySpecialty>,
                    SqlError
                > ok => Results.Ok(ok.Value),
                Result<ImmutableList<SearchPractitionersBySpecialty>, SqlError>.Error<
                    ImmutableList<SearchPractitionersBySpecialty>,
                    SqlError
                > err => Results.Problem(err.Value.Message),
                _ => Results.Problem("Unknown error"),
            };
        }
        else
        {
            var result = await conn.GetAllPractitionersAsync().ConfigureAwait(false);
            return result switch
            {
                Result<ImmutableList<GetAllPractitioners>, SqlError>.Ok<
                    ImmutableList<GetAllPractitioners>,
                    SqlError
                > ok => Results.Ok(ok.Value),
                Result<ImmutableList<GetAllPractitioners>, SqlError>.Error<
                    ImmutableList<GetAllPractitioners>,
                    SqlError
                > err => Results.Problem(err.Value.Message),
                _ => Results.Problem("Unknown error"),
            };
        }
    }
);

// === FHIR APPOINTMENT ENDPOINTS ===

app.MapGet(
    "/Appointment",
    async (Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetUpcomingAppointmentsAsync().ConfigureAwait(false);
        return result switch
        {
            Result<ImmutableList<GetUpcomingAppointments>, SqlError>.Ok<
                ImmutableList<GetUpcomingAppointments>,
                SqlError
            > ok => Results.Ok(ok.Value),
            Result<ImmutableList<GetUpcomingAppointments>, SqlError>.Error<
                ImmutableList<GetUpcomingAppointments>,
                SqlError
            > err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapGet(
    "/Appointment/{id}",
    async (string id, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetAppointmentByIdAsync(id).ConfigureAwait(false);
        return result switch
        {
            Result<ImmutableList<GetAppointmentById>, SqlError>.Ok<
                ImmutableList<GetAppointmentById>,
                SqlError
            > ok when ok.Value.Count > 0 => Results.Ok(ok.Value[0]),
            Result<ImmutableList<GetAppointmentById>, SqlError>.Ok<
                ImmutableList<GetAppointmentById>,
                SqlError
            > => Results.NotFound(),
            Result<ImmutableList<GetAppointmentById>, SqlError>.Error<
                ImmutableList<GetAppointmentById>,
                SqlError
            > err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapPost(
    "/Appointment",
    async (CreateAppointmentRequest request, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var transaction = await conn.BeginTransactionAsync().ConfigureAwait(false);
        await using var _ = transaction.ConfigureAwait(false);
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString(
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            CultureInfo.InvariantCulture
        );
        var start = DateTime.Parse(request.Start, CultureInfo.InvariantCulture);
        var end = DateTime.Parse(request.End, CultureInfo.InvariantCulture);
        var durationMinutes = (int)(end - start).TotalMinutes;

        var result = await transaction
            .Insertfhir_AppointmentAsync(
                id,
                "booked",
                request.ServiceCategory ?? string.Empty,
                request.ServiceType ?? string.Empty,
                request.ReasonCode ?? string.Empty,
                request.Priority,
                request.Description ?? string.Empty,
                request.Start,
                request.End,
                durationMinutes,
                request.PatientReference,
                request.PractitionerReference,
                now,
                request.Comment ?? string.Empty
            )
            .ConfigureAwait(false);

        if (result is Result<long, SqlError>.Ok<long, SqlError>)
        {
            await transaction.CommitAsync().ConfigureAwait(false);
            return Results.Created(
                $"/Appointment/{id}",
                new Appointment(
                    id,
                    "booked",
                    request.ServiceCategory,
                    request.ServiceType,
                    request.ReasonCode,
                    request.Priority,
                    request.Description,
                    request.Start,
                    request.End,
                    durationMinutes,
                    request.PatientReference,
                    request.PractitionerReference,
                    now,
                    request.Comment
                )
            );
        }

        return result switch
        {
            Result<long, SqlError>.Error<long, SqlError> err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapPatch(
    "/Appointment/{id}/status",
    async (string id, string status, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var transaction = (SqliteTransaction)
            await conn.BeginTransactionAsync().ConfigureAwait(false);
        await using var _ = transaction.ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "UPDATE fhir_Appointment SET Status = @status WHERE Id = @id";
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@id", id);

        var rowsAffected = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        if (rowsAffected > 0)
        {
            await transaction.CommitAsync().ConfigureAwait(false);
            return Results.Ok(new { id, status });
        }

        return Results.NotFound();
    }
);

app.MapGet(
    "/Patient/{patientId}/Appointment",
    async (string patientId, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetAppointmentsByPatientAsync($"Patient/{patientId}")
            .ConfigureAwait(false);
        return result switch
        {
            Result<ImmutableList<GetAppointmentsByPatient>, SqlError>.Ok<
                ImmutableList<GetAppointmentsByPatient>,
                SqlError
            > ok => Results.Ok(ok.Value),
            Result<ImmutableList<GetAppointmentsByPatient>, SqlError>.Error<
                ImmutableList<GetAppointmentsByPatient>,
                SqlError
            > err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapGet(
    "/Practitioner/{practitionerId}/Appointment",
    async (string practitionerId, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetAppointmentsByPractitionerAsync($"Practitioner/{practitionerId}")
            .ConfigureAwait(false);
        return result switch
        {
            Result<ImmutableList<GetAppointmentsByPractitioner>, SqlError>.Ok<
                ImmutableList<GetAppointmentsByPractitioner>,
                SqlError
            > ok => Results.Ok(ok.Value),
            Result<ImmutableList<GetAppointmentsByPractitioner>, SqlError>.Error<
                ImmutableList<GetAppointmentsByPractitioner>,
                SqlError
            > err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

// === SYNC ENDPOINTS ===

app.MapGet(
    "/sync/changes",
    (long? fromVersion, int? limit, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = SyncLogRepository.FetchChanges(conn, fromVersion ?? 0, limit ?? 100);
        return result switch
        {
            SyncLogListOk ok => Results.Ok(ok.Value),
            SyncLogListError err => Results.Problem(SyncHelpers.ToMessage(err.Value)),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapGet(
    "/sync/origin",
    (Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = SyncSchema.GetOriginId(conn);
        return result switch
        {
            StringSyncOk ok => Results.Ok(new { originId = ok.Value }),
            StringSyncError err => Results.Problem(SyncHelpers.ToMessage(err.Value)),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.Run();
