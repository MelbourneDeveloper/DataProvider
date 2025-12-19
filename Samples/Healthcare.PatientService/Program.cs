using Healthcare.PatientService;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Configure SQLite connection
var dbPath = Path.Combine(AppContext.BaseDirectory, "patients.db");
var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
builder.Services.AddSingleton<Func<SqliteConnection>>(
    () =>
    {
        var conn = new SqliteConnection(connectionString);
        conn.Open();
        return conn;
    }
);

var app = builder.Build();

// Initialize database
using (var conn = new SqliteConnection(connectionString))
{
    conn.Open();
    DatabaseSetup.Initialize(conn, app.Logger);
}

// === PATIENT ENDPOINTS (FHIR-style) ===

var patientGroup = app.MapGroup("/fhir/Patient").WithTags("Patient");

patientGroup.MapGet(
    "/",
    (
        bool? active,
        string? familyName,
        string? givenName,
        string? gender,
        Func<SqliteConnection> getConn
    ) =>
    {
        using var conn = getConn();
        var result = PatientRepository.GetPatients(conn, active, familyName, givenName, gender);
        return result switch
        {
            Result<IReadOnlyList<Patient>, SqlError>.Ok<IReadOnlyList<Patient>, SqlError> ok =>
                Results.Ok(ok.Value),
            Result<IReadOnlyList<Patient>, SqlError>.Error<IReadOnlyList<Patient>, SqlError> err =>
                Results.Problem(err.Value.Message),
        };
    }
);

patientGroup.MapGet(
    "/{id}",
    (string id, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = PatientRepository.GetById(conn, id);
        return result switch
        {
            Result<Patient?, SqlError>.Ok<Patient?, SqlError>({ } value) => Results.Ok(value),
            Result<Patient?, SqlError>.Ok<Patient?, SqlError> => Results.NotFound(),
            Result<Patient?, SqlError>.Error<Patient?, SqlError> err => Results.Problem(err.Value.Message),
        };
    }
);

patientGroup.MapPost(
    "/",
    (CreatePatientRequest request, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = PatientRepository.Create(conn, request);
        return result switch
        {
            Result<Patient, SqlError>.Ok<Patient, SqlError> ok =>
                Results.Created($"/fhir/Patient/{ok.Value.Id}", ok.Value),
            Result<Patient, SqlError>.Error<Patient, SqlError> err =>
                Results.Problem(err.Value.Message),
        };
    }
);

patientGroup.MapGet(
    "/_search",
    (string q, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = PatientRepository.Search(conn, q);
        return result switch
        {
            Result<IReadOnlyList<Patient>, SqlError>.Ok<IReadOnlyList<Patient>, SqlError> ok =>
                Results.Ok(ok.Value),
            Result<IReadOnlyList<Patient>, SqlError>.Error<IReadOnlyList<Patient>, SqlError> err =>
                Results.Problem(err.Value.Message),
        };
    }
);

var records = patientGroup.MapGroup("/{patientId}/records").WithTags("MedicalRecord");

records.MapGet(
    "/",
    (string patientId, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = MedicalRecordRepository.GetByPatientId(conn, patientId);
        return result switch
        {
            Result<IReadOnlyList<MedicalRecord>, SqlError>.Ok<IReadOnlyList<MedicalRecord>, SqlError>
                ok => Results.Ok(ok.Value),
            Result<IReadOnlyList<MedicalRecord>, SqlError>.Error<
                IReadOnlyList<MedicalRecord>,
                SqlError
            > err => Results.Problem(err.Value.Message),
        };
    }
);

records.MapPost(
    "/",
    (string patientId, CreateMedicalRecordRequest request, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var recordRequest = request with { PatientId = patientId };
        var result = MedicalRecordRepository.Create(conn, recordRequest);
        return result switch
        {
            Result<MedicalRecord, SqlError>.Ok<MedicalRecord, SqlError> ok =>
                Results.Created($"/fhir/Patient/{patientId}/records/{ok.Value.Id}", ok.Value),
            Result<MedicalRecord, SqlError>.Error<MedicalRecord, SqlError> err =>
                Results.Problem(err.Value.Message),
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
            SyncLogListError err => Results.Problem(err.Value.Message),
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
            StringSyncError err => Results.Problem(err.Value.Message),
        };
    }
);

app.Run();
