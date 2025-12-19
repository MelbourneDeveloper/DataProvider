using System.Globalization;
using Clinical.Api;

var builder = WebApplication.CreateBuilder(args);

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
        return result switch
        {
            GetPatientsOk ok => Results.Ok(ok.Value),
            GetPatientsError err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

patientGroup.MapGet(
    "/{id}",
    async (string id, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetPatientByIdAsync(id).ConfigureAwait(false);
        return result switch
        {
            GetPatientByIdOk ok when ok.Value.Count > 0 => Results.Ok(ok.Value[0]),
            GetPatientByIdOk => Results.NotFound(),
            GetPatientByIdError err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
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

        return result switch
        {
            InsertError err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

patientGroup.MapGet(
    "/_search",
    async (string q, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.SearchPatientsAsync($"%{q}%").ConfigureAwait(false);
        return result switch
        {
            SearchPatientsOk ok => Results.Ok(ok.Value),
            SearchPatientsError err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

var encounterGroup = patientGroup.MapGroup("/{patientId}/Encounter").WithTags("Encounter");

encounterGroup.MapGet(
    "/",
    async (string patientId, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetEncountersByPatientAsync(patientId).ConfigureAwait(false);
        return result switch
        {
            GetEncountersOk ok => Results.Ok(ok.Value),
            GetEncountersError err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
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

        return result switch
        {
            InsertError err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

var conditionGroup = patientGroup.MapGroup("/{patientId}/Condition").WithTags("Condition");

conditionGroup.MapGet(
    "/",
    async (string patientId, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetConditionsByPatientAsync(patientId).ConfigureAwait(false);
        return result switch
        {
            GetConditionsOk ok => Results.Ok(ok.Value),
            GetConditionsError err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
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

        return result switch
        {
            InsertError err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
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
        return result switch
        {
            GetMedicationsOk ok => Results.Ok(ok.Value),
            GetMedicationsError err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
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

        return result switch
        {
            InsertError err => Results.Problem(err.Value.Message),
            _ => Results.Problem("Unknown error"),
        };
    }
);

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
