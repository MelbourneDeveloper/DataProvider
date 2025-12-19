using Healthcare.AppointmentService;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL connection from environment or default
var connectionString =
    Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
    ?? "Host=localhost;Database=clinic_appointments;Username=clinic;Password=clinic123";

builder.Services.AddSingleton<Func<NpgsqlConnection>>(() =>
{
    var conn = new NpgsqlConnection(connectionString);
    conn.Open();
    return conn;
});

var app = builder.Build();

// Initialize database (requires running PostgreSQL)
try
{
    using var conn = new NpgsqlConnection(connectionString);
    conn.Open();
    DatabaseSetup.Initialize(conn, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning("Could not initialize DB (PostgreSQL may not be running): {Error}", ex.Message);
}

// === FHIR PRACTITIONER ENDPOINTS (using LQL via DataProvider) ===

app.MapGet(
    "/Practitioner",
    (Func<NpgsqlConnection> getConn) =>
    {
        using var conn = getConn();
        var result = PractitionerRepository.GetAll(conn);
        return result switch
        {
            Result<IReadOnlyList<Practitioner>, SqlError>.Ok<IReadOnlyList<Practitioner>, SqlError>
                ok => Results.Ok(ok.Value),
            Result<IReadOnlyList<Practitioner>, SqlError>.Error<
                IReadOnlyList<Practitioner>,
                SqlError
            > err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapGet(
    "/Practitioner/{id}",
    (string id, Func<NpgsqlConnection> getConn) =>
    {
        using var conn = getConn();
        var result = PractitionerRepository.GetById(conn, id);
        return result switch
        {
            Result<Practitioner?, SqlError>.Ok<Practitioner?, SqlError> ok when ok.Value is not null
                => Results.Ok(ok.Value),
            Result<Practitioner?, SqlError>.Ok<Practitioner?, SqlError> => Results.NotFound(),
            Result<Practitioner?, SqlError>.Error<Practitioner?, SqlError> err =>
                Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapPost(
    "/Practitioner",
    (CreatePractitionerRequest request, Func<NpgsqlConnection> getConn) =>
    {
        using var conn = getConn();
        var result = PractitionerRepository.Create(conn, request);
        return result switch
        {
            Result<Practitioner, SqlError>.Ok<Practitioner, SqlError> ok =>
                Results.Created($"/Practitioner/{ok.Value.Id}", ok.Value),
            Result<Practitioner, SqlError>.Error<Practitioner, SqlError> err =>
                Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapGet(
    "/Practitioner/_search",
    (string? specialty, Func<NpgsqlConnection> getConn) =>
    {
        using var conn = getConn();
        var result = specialty is not null
            ? PractitionerRepository.SearchBySpecialty(conn, specialty)
            : PractitionerRepository.GetAll(conn);
        return result switch
        {
            Result<IReadOnlyList<Practitioner>, SqlError>.Ok<IReadOnlyList<Practitioner>, SqlError>
                ok => Results.Ok(ok.Value),
            Result<IReadOnlyList<Practitioner>, SqlError>.Error<
                IReadOnlyList<Practitioner>,
                SqlError
            > err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

// === FHIR APPOINTMENT ENDPOINTS (using LQL via DataProvider) ===

app.MapGet(
    "/Appointment",
    (Func<NpgsqlConnection> getConn) =>
    {
        using var conn = getConn();
        var result = AppointmentRepository.GetUpcoming(conn);
        return result switch
        {
            Result<IReadOnlyList<Appointment>, SqlError>.Ok<IReadOnlyList<Appointment>, SqlError>
                ok => Results.Ok(ok.Value),
            Result<IReadOnlyList<Appointment>, SqlError>.Error<
                IReadOnlyList<Appointment>,
                SqlError
            > err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapGet(
    "/Appointment/{id}",
    (string id, Func<NpgsqlConnection> getConn) =>
    {
        using var conn = getConn();
        var result = AppointmentRepository.GetById(conn, id);
        return result switch
        {
            Result<Appointment?, SqlError>.Ok<Appointment?, SqlError> ok when ok.Value is not null
                => Results.Ok(ok.Value),
            Result<Appointment?, SqlError>.Ok<Appointment?, SqlError> => Results.NotFound(),
            Result<Appointment?, SqlError>.Error<Appointment?, SqlError> err =>
                Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapPost(
    "/Appointment",
    (CreateAppointmentRequest request, Func<NpgsqlConnection> getConn) =>
    {
        using var conn = getConn();
        var result = AppointmentRepository.Create(conn, request);
        return result switch
        {
            Result<Appointment, SqlError>.Ok<Appointment, SqlError> ok =>
                Results.Created($"/Appointment/{ok.Value.Id}", ok.Value),
            Result<Appointment, SqlError>.Error<Appointment, SqlError> err =>
                Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

// FHIR Appointment status workflow
app.MapPatch(
    "/Appointment/{id}/status",
    (string id, string status, Func<NpgsqlConnection> getConn) =>
    {
        using var conn = getConn();
        var result = AppointmentRepository.UpdateStatus(conn, id, status);
        return result switch
        {
            Result<int, SqlError>.Ok<int, SqlError> ok when ok.Value > 0 =>
                Results.Ok(new { id, status }),
            Result<int, SqlError>.Ok<int, SqlError> => Results.NotFound(),
            Result<int, SqlError>.Error<int, SqlError> err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

// Patient's appointments (LQL query)
app.MapGet(
    "/Patient/{patientId}/Appointment",
    (string patientId, Func<NpgsqlConnection> getConn) =>
    {
        using var conn = getConn();
        var result = AppointmentRepository.GetByPatient(conn, patientId);
        return result switch
        {
            Result<IReadOnlyList<Appointment>, SqlError>.Ok<IReadOnlyList<Appointment>, SqlError>
                ok => Results.Ok(ok.Value),
            Result<IReadOnlyList<Appointment>, SqlError>.Error<
                IReadOnlyList<Appointment>,
                SqlError
            > err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

// Practitioner's appointments (LQL query)
app.MapGet(
    "/Practitioner/{practitionerId}/Appointment",
    (string practitionerId, Func<NpgsqlConnection> getConn) =>
    {
        using var conn = getConn();
        var result = AppointmentRepository.GetByPractitioner(conn, practitionerId);
        return result switch
        {
            Result<IReadOnlyList<Appointment>, SqlError>.Ok<IReadOnlyList<Appointment>, SqlError>
                ok => Results.Ok(ok.Value),
            Result<IReadOnlyList<Appointment>, SqlError>.Error<
                IReadOnlyList<Appointment>,
                SqlError
            > err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

// === SYNC ENDPOINTS ===

app.MapGet(
    "/sync/changes",
    (long? fromVersion, int? limit, Func<NpgsqlConnection> getConn) =>
    {
        using var conn = getConn();
        var result = PostgresSyncLogRepository.FetchChanges(conn, fromVersion ?? 0, limit ?? 100);
        return result switch
        {
            SyncLogListOk ok => Results.Ok(ok.Value),
            SyncLogListError err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.MapGet(
    "/sync/origin",
    (Func<NpgsqlConnection> getConn) =>
    {
        using var conn = getConn();
        var result = PostgresSyncSchema.GetOriginId(conn);
        return result switch
        {
            StringSyncOk ok => Results.Ok(new { originId = ok.Value }),
            StringSyncError err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

app.Run();
