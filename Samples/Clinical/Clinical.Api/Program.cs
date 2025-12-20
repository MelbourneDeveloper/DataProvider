using System.Globalization;
using Clinical.Api;

var builder = WebApplication.CreateBuilder(args);

// Add CORS for dashboard
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Dashboard",
        policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:3000",
                    "http://localhost:5173",
                    "http://127.0.0.1:3000",
                    "http://127.0.0.1:5173"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    );
});

var dbPath = Path.Combine(AppContext.BaseDirectory, "clinical.db");
var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
builder.Services.AddSingleton(() =>
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

// Enable CORS
app.UseCors("Dashboard");

var patientGroup = app.MapGroup("/fhir/Patient").WithTags("Patient");

patientGroup.MapGet(
    "/",
    async (
        bool? active,
        string? familyName,
        string? givenName,
        string? gender,
        Func<SqliteConnection> getConn
    ) =>
    {
        using var conn = getConn();
        var result = await conn.GetPatientsAsync(
                active.HasValue ? (active.Value ? 1L : 0L) : DBNull.Value,
                familyName ?? (object)DBNull.Value,
                givenName ?? (object)DBNull.Value,
                gender ?? (object)DBNull.Value
            )
            .ConfigureAwait(false);
        return result.Match(patients => Results.Ok(patients), err => Results.Problem(err.Message));
    }
);

patientGroup.MapGet(
    "/{id}",
    async (string id, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetPatientByIdAsync(id).ConfigureAwait(false);
        return result.Match(
            patients => patients.Count > 0 ? Results.Ok(patients[0]) : Results.NotFound(),
            err => Results.Problem(err.Message)
        );
    }
);

patientGroup.MapPost(
    "/",
    async (CreatePatientRequest request, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var transaction = await conn.BeginTransactionAsync().ConfigureAwait(false);
        await using var _ = transaction.ConfigureAwait(false);
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString(
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            CultureInfo.InvariantCulture
        );

        var result = await transaction
            .Insertfhir_PatientAsync(
                id,
                request.Active ? 1L : 0L,
                request.GivenName,
                request.FamilyName,
                request.BirthDate,
                request.Gender,
                request.Phone,
                request.Email,
                request.AddressLine,
                request.City,
                request.State,
                request.PostalCode,
                request.Country,
                now,
                1L
            )
            .ConfigureAwait(false);

        if (result is InsertOk)
        {
            await transaction.CommitAsync().ConfigureAwait(false);
            return Results.Created(
                $"/fhir/Patient/{id}",
                new
                {
                    Id = id,
                    Active = request.Active,
                    GivenName = request.GivenName,
                    FamilyName = request.FamilyName,
                    BirthDate = request.BirthDate,
                    Gender = request.Gender,
                    Phone = request.Phone,
                    Email = request.Email,
                    AddressLine = request.AddressLine,
                    City = request.City,
                    State = request.State,
                    PostalCode = request.PostalCode,
                    Country = request.Country,
                    LastUpdated = now,
                    VersionId = 1L,
                }
            );
        }

        return result.Match<IResult>(
            _ => Results.Problem("Unexpected state"),
            err => Results.Problem(err.Message)
        );
    }
);

patientGroup.MapGet(
    "/_search",
    async (string q, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.SearchPatientsAsync($"%{q}%").ConfigureAwait(false);
        return result.Match(patients => Results.Ok(patients), err => Results.Problem(err.Message));
    }
);

var encounterGroup = patientGroup.MapGroup("/{patientId}/Encounter").WithTags("Encounter");

encounterGroup.MapGet(
    "/",
    async (string patientId, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetEncountersByPatientAsync(patientId).ConfigureAwait(false);
        return result.Match(
            encounters => Results.Ok(encounters),
            err => Results.Problem(err.Message)
        );
    }
);

encounterGroup.MapPost(
    "/",
    async (string patientId, CreateEncounterRequest request, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var transaction = await conn.BeginTransactionAsync().ConfigureAwait(false);
        await using var _ = transaction.ConfigureAwait(false);
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString(
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            CultureInfo.InvariantCulture
        );

        var result = await transaction
            .Insertfhir_EncounterAsync(
                id,
                request.Status,
                request.Class,
                patientId,
                request.PractitionerId,
                request.ServiceType,
                request.ReasonCode,
                request.PeriodStart,
                request.PeriodEnd,
                request.Notes,
                now,
                1L
            )
            .ConfigureAwait(false);

        if (result is InsertOk)
        {
            await transaction.CommitAsync().ConfigureAwait(false);
            return Results.Created(
                $"/fhir/Patient/{patientId}/Encounter/{id}",
                new
                {
                    Id = id,
                    Status = request.Status,
                    Class = request.Class,
                    PatientId = patientId,
                    PractitionerId = request.PractitionerId,
                    ServiceType = request.ServiceType,
                    ReasonCode = request.ReasonCode,
                    PeriodStart = request.PeriodStart,
                    PeriodEnd = request.PeriodEnd,
                    Notes = request.Notes,
                    LastUpdated = now,
                    VersionId = 1L,
                }
            );
        }

        return result.Match<IResult>(
            _ => Results.Problem("Unexpected state"),
            err => Results.Problem(err.Message)
        );
    }
);

var conditionGroup = patientGroup.MapGroup("/{patientId}/Condition").WithTags("Condition");

conditionGroup.MapGet(
    "/",
    async (string patientId, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetConditionsByPatientAsync(patientId).ConfigureAwait(false);
        return result.Match(
            conditions => Results.Ok(conditions),
            err => Results.Problem(err.Message)
        );
    }
);

conditionGroup.MapPost(
    "/",
    async (string patientId, CreateConditionRequest request, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var transaction = await conn.BeginTransactionAsync().ConfigureAwait(false);
        await using var _ = transaction.ConfigureAwait(false);
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString(
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            CultureInfo.InvariantCulture
        );
        var recordedDate = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var result = await transaction
            .Insertfhir_ConditionAsync(
                id,
                request.ClinicalStatus,
                request.VerificationStatus,
                request.Category,
                request.Severity,
                request.CodeSystem,
                request.CodeValue,
                request.CodeDisplay,
                patientId,
                request.EncounterReference,
                request.OnsetDateTime,
                recordedDate,
                request.RecorderReference,
                request.NoteText,
                now,
                1L
            )
            .ConfigureAwait(false);

        if (result is InsertOk)
        {
            await transaction.CommitAsync().ConfigureAwait(false);
            return Results.Created(
                $"/fhir/Patient/{patientId}/Condition/{id}",
                new
                {
                    Id = id,
                    ClinicalStatus = request.ClinicalStatus,
                    VerificationStatus = request.VerificationStatus,
                    Category = request.Category,
                    Severity = request.Severity,
                    CodeSystem = request.CodeSystem,
                    CodeValue = request.CodeValue,
                    CodeDisplay = request.CodeDisplay,
                    SubjectReference = patientId,
                    EncounterReference = request.EncounterReference,
                    OnsetDateTime = request.OnsetDateTime,
                    RecordedDate = recordedDate,
                    RecorderReference = request.RecorderReference,
                    NoteText = request.NoteText,
                    LastUpdated = now,
                    VersionId = 1L,
                }
            );
        }

        return result.Match<IResult>(
            _ => Results.Problem("Unexpected state"),
            err => Results.Problem(err.Message)
        );
    }
);

var medicationGroup = patientGroup
    .MapGroup("/{patientId}/MedicationRequest")
    .WithTags("MedicationRequest");

medicationGroup.MapGet(
    "/",
    async (string patientId, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetMedicationsByPatientAsync(patientId).ConfigureAwait(false);
        return result.Match(
            medications => Results.Ok(medications),
            err => Results.Problem(err.Message)
        );
    }
);

medicationGroup.MapPost(
    "/",
    async (
        string patientId,
        CreateMedicationRequestRequest request,
        Func<SqliteConnection> getConn
    ) =>
    {
        using var conn = getConn();
        var transaction = await conn.BeginTransactionAsync().ConfigureAwait(false);
        await using var _ = transaction.ConfigureAwait(false);
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString(
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            CultureInfo.InvariantCulture
        );

        var result = await transaction
            .Insertfhir_MedicationRequestAsync(
                id,
                request.Status,
                request.Intent,
                patientId,
                request.PractitionerId,
                request.EncounterId,
                request.MedicationCode,
                request.MedicationDisplay,
                request.DosageInstruction,
                request.Quantity,
                request.Unit,
                request.Refills,
                now,
                now,
                1L
            )
            .ConfigureAwait(false);

        if (result is InsertOk)
        {
            await transaction.CommitAsync().ConfigureAwait(false);
            return Results.Created(
                $"/fhir/Patient/{patientId}/MedicationRequest/{id}",
                new
                {
                    Id = id,
                    Status = request.Status,
                    Intent = request.Intent,
                    PatientId = patientId,
                    PractitionerId = request.PractitionerId,
                    EncounterId = request.EncounterId,
                    MedicationCode = request.MedicationCode,
                    MedicationDisplay = request.MedicationDisplay,
                    DosageInstruction = request.DosageInstruction,
                    Quantity = request.Quantity,
                    Unit = request.Unit,
                    Refills = request.Refills,
                    AuthoredOn = now,
                    LastUpdated = now,
                    VersionId = 1L,
                }
            );
        }

        return result.Match<IResult>(
            _ => Results.Problem("Unexpected state"),
            err => Results.Problem(err.Message)
        );
    }
);

app.MapGet(
    "/sync/changes",
    (long? fromVersion, int? limit, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = SyncLogRepository.FetchChanges(conn, fromVersion ?? 0, limit ?? 100);
        return result.Match(
            logs => Results.Ok(logs),
            err => Results.Problem(SyncHelpers.ToMessage(err))
        );
    }
);

app.MapGet(
    "/sync/origin",
    (Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = SyncSchema.GetOriginId(conn);
        return result.Match(
            originId => Results.Ok(new { originId }),
            err => Results.Problem(SyncHelpers.ToMessage(err))
        );
    }
);

app.Run();

/// <summary>
/// Program entry point marker for WebApplicationFactory.
/// </summary>
public partial class Program { }
