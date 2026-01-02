#pragma warning disable IDE0037 // Use inferred member name - prefer explicit for clarity in API responses

using System.Collections.Immutable;
using System.Globalization;
using Clinical.Api;
using Microsoft.AspNetCore.Http.Json;
using Samples.Authorization;

var builder = WebApplication.CreateBuilder(args);

// File logging
var logPath = Path.Combine(AppContext.BaseDirectory, "clinical.log");
builder.Logging.AddFileLogging(logPath);

// Configure JSON to use PascalCase property names
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});

// Add CORS for dashboard - allow any origin for testing
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Dashboard",
        policy =>
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    );
});

// Always use a real SQLite file - NEVER in-memory
var dbPath =
    builder.Configuration["DbPath"] ?? Path.Combine(AppContext.BaseDirectory, "clinical.db");
var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    ForeignKeys = true, // ENFORCE REFERENTIAL INTEGRITY
}.ToString();

builder.Services.AddSingleton(() =>
{
    var conn = new SqliteConnection(connectionString);
    conn.Open();
    return conn;
});

// Gatekeeper configuration for authorization
var gatekeeperUrl = builder.Configuration["Gatekeeper:BaseUrl"] ?? "http://localhost:5002";
var signingKeyBase64 = builder.Configuration["Jwt:SigningKey"];
var signingKey = string.IsNullOrEmpty(signingKeyBase64)
    ? ImmutableArray.Create(new byte[32]) // Default empty key for development (MUST configure in production)
    : ImmutableArray.Create(Convert.FromBase64String(signingKeyBase64));

builder.Services.AddHttpClient(
    "Gatekeeper",
    client =>
    {
        client.BaseAddress = new Uri(gatekeeperUrl);
        client.Timeout = TimeSpan.FromSeconds(5);
    }
);

var app = builder.Build();

using (var conn = new SqliteConnection(connectionString))
{
    conn.Open();
    DatabaseSetup.Initialize(conn, app.Logger);
}

// Enable CORS
app.UseCors("Dashboard");

// Get HttpClientFactory for auth filters
var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
Func<HttpClient> getGatekeeperClient = () => httpClientFactory.CreateClient("Gatekeeper");

var patientGroup = app.MapGroup("/fhir/Patient").WithTags("Patient");

patientGroup
    .MapGet(
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
                GetPatientsOk(var patients) => Results.Ok(patients),
                GetPatientsError(var err) => Results.Problem(err.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.PatientRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

patientGroup
    .MapGet(
        "/{id}",
        async (string id, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var result = await conn.GetPatientByIdAsync(id).ConfigureAwait(false);
            return result switch
            {
                GetPatientByIdOk(var patients) when patients.Count > 0 => Results.Ok(patients[0]),
                GetPatientByIdOk => Results.NotFound(),
                GetPatientByIdError(var err) => Results.Problem(err.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequireResourcePermission(
            FhirPermissions.PatientRead,
            signingKey,
            getGatekeeperClient,
            app.Logger,
            idParamName: "id"
        )
    );

patientGroup
    .MapPost(
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

            return result.Match(
                _ => Results.Problem("Unexpected state"),
                err => Results.Problem(err.Message)
            );
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.PatientCreate,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

patientGroup
    .MapPut(
        "/{id}",
        async (string id, UpdatePatientRequest request, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();

            // First verify the patient exists
            var existingResult = await conn.GetPatientByIdAsync(id).ConfigureAwait(false);
            if (existingResult is GetPatientByIdOk(var patients) && patients.Count == 0)
            {
                return Results.NotFound();
            }

            if (existingResult is GetPatientByIdError(var fetchErr))
            {
                return Results.Problem(fetchErr.Message);
            }

            var existingPatient = ((GetPatientByIdOk)existingResult).Value[0];
            var newVersionId = existingPatient.VersionId + 1;

            var transaction = await conn.BeginTransactionAsync().ConfigureAwait(false);
            await using var _ = transaction.ConfigureAwait(false);
            var now = DateTime.UtcNow.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                CultureInfo.InvariantCulture
            );

            var result = await transaction
                .Updatefhir_PatientAsync(
                    id,
                    request.Active ? 1L : 0L,
                    request.GivenName,
                    request.FamilyName,
                    request.BirthDate ?? string.Empty,
                    request.Gender ?? string.Empty,
                    request.Phone ?? string.Empty,
                    request.Email ?? string.Empty,
                    request.AddressLine ?? string.Empty,
                    request.City ?? string.Empty,
                    request.State ?? string.Empty,
                    request.PostalCode ?? string.Empty,
                    request.Country ?? string.Empty,
                    now,
                    newVersionId
                )
                .ConfigureAwait(false);

            if (result is UpdateOk)
            {
                await transaction.CommitAsync().ConfigureAwait(false);
                return Results.Ok(
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
                        VersionId = newVersionId,
                    }
                );
            }

            return result.Match(
                _ => Results.Problem("Unexpected state"),
                err => Results.Problem(err.Message)
            );
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequireResourcePermission(
            FhirPermissions.PatientUpdate,
            signingKey,
            getGatekeeperClient,
            app.Logger,
            idParamName: "id"
        )
    );

patientGroup
    .MapGet(
        "/_search",
        async (string q, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var result = await conn.SearchPatientsAsync($"%{q}%").ConfigureAwait(false);
            return result switch
            {
                SearchPatientsOk(var patients) => Results.Ok(patients),
                SearchPatientsError(var err) => Results.Problem(err.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.PatientRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

var encounterGroup = patientGroup.MapGroup("/{patientId}/Encounter").WithTags("Encounter");

encounterGroup
    .MapGet(
        "/",
        async (string patientId, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var result = await conn.GetEncountersByPatientAsync(patientId).ConfigureAwait(false);
            return result switch
            {
                GetEncountersOk(var encounters) => Results.Ok(encounters),
                GetEncountersError(var err) => Results.Problem(err.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePatientPermission(
            FhirPermissions.EncounterRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

encounterGroup
    .MapPost(
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
                InsertOk => Results.Problem("Unexpected state"),
                InsertError(var err) => Results.Problem(err.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePatientPermission(
            FhirPermissions.EncounterCreate,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

var conditionGroup = patientGroup.MapGroup("/{patientId}/Condition").WithTags("Condition");

conditionGroup
    .MapGet(
        "/",
        async (string patientId, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var result = await conn.GetConditionsByPatientAsync(patientId).ConfigureAwait(false);
            return result switch
            {
                GetConditionsOk(var conditions) => Results.Ok(conditions),
                GetConditionsError(var err) => Results.Problem(err.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePatientPermission(
            FhirPermissions.ConditionRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

conditionGroup
    .MapPost(
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
                    id: id,
                    clinicalstatus: request.ClinicalStatus,
                    verificationstatus: request.VerificationStatus,
                    category: request.Category,
                    severity: request.Severity,
                    codesystem: request.CodeSystem,
                    codevalue: request.CodeValue,
                    codedisplay: request.CodeDisplay,
                    subjectreference: patientId,
                    encounterreference: request.EncounterReference,
                    onsetdatetime: request.OnsetDateTime,
                    recordeddate: recordedDate,
                    recorderreference: request.RecorderReference,
                    notetext: request.NoteText,
                    lastupdated: now,
                    versionid: 1L
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
                InsertOk => Results.Problem("Unexpected state"),
                InsertError(var err) => Results.Problem(err.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePatientPermission(
            FhirPermissions.ConditionCreate,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

var medicationGroup = patientGroup
    .MapGroup("/{patientId}/MedicationRequest")
    .WithTags("MedicationRequest");

medicationGroup
    .MapGet(
        "/",
        async (string patientId, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var result = await conn.GetMedicationsByPatientAsync(patientId).ConfigureAwait(false);
            return result switch
            {
                GetMedicationsOk(var medications) => Results.Ok(medications),
                GetMedicationsError(var err) => Results.Problem(err.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePatientPermission(
            FhirPermissions.MedicationRequestRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

medicationGroup
    .MapPost(
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
                InsertOk => Results.Problem("Unexpected state"),
                InsertError(var err) => Results.Problem(err.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePatientPermission(
            FhirPermissions.MedicationRequestCreate,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

app.MapGet(
        "/sync/changes",
        (long? fromVersion, int? limit, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var result = SyncLogRepository.FetchChanges(conn, fromVersion ?? 0, limit ?? 100);
            return result switch
            {
                SyncLogListOk(var logs) => Results.Ok(logs),
                SyncLogListError(var err) => Results.Problem(SyncHelpers.ToMessage(err)),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.SyncRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

app.MapGet(
        "/sync/origin",
        (Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var result = SyncSchema.GetOriginId(conn);
            return result switch
            {
                StringSyncOk(var originId) => Results.Ok(new { originId }),
                StringSyncError(var err) => Results.Problem(SyncHelpers.ToMessage(err)),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.SyncRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

app.MapGet(
        "/sync/status",
        (Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var changesResult = SyncLogRepository.FetchChanges(conn, 0, 1000);

            var (totalCount, lastSyncTime) = changesResult switch
            {
                SyncLogListOk(var logs) => (
                    logs.Count,
                    logs.Count > 0
                        ? logs.Max(l => l.Timestamp)
                        : DateTime.UtcNow.ToString(
                            "yyyy-MM-ddTHH:mm:ss.fffZ",
                            CultureInfo.InvariantCulture
                        )
                ),
                SyncLogListError => (
                    0,
                    DateTime.UtcNow.ToString(
                        "yyyy-MM-ddTHH:mm:ss.fffZ",
                        CultureInfo.InvariantCulture
                    )
                ),
            };

            return Results.Ok(
                new
                {
                    service = "Clinical.Api",
                    status = "healthy",
                    lastSyncTime,
                    totalRecords = totalCount,
                    failedCount = 0,
                }
            );
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.SyncRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

app.MapGet(
        "/sync/records",
        (string? search, int? page, int? pageSize, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var currentPage = page ?? 1;
            var size = pageSize ?? 50;
            var changesResult = SyncLogRepository.FetchChanges(conn, 0, 1000);

            return changesResult switch
            {
                SyncLogListOk(var logs) => Results.Ok(
                    BuildSyncRecordsResponse(logs, search, currentPage, size)
                ),
                SyncLogListError(var err) => Results.Problem(SyncHelpers.ToMessage(err)),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.SyncRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

app.MapPost(
        "/sync/records/{id}/retry",
        (string id) =>
        {
            // For now, just acknowledge the retry request
            // Real implementation would mark the record for re-sync
            return Results.Accepted();
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.SyncWrite,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

app.MapGet(
        "/sync/providers",
        (Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT ProviderId, FirstName, LastName, Specialty, SyncedAt FROM sync_Provider";
            using var reader = cmd.ExecuteReader();
            var providers = new List<object>();
            while (reader.Read())
            {
                providers.Add(
                    new
                    {
                        ProviderId = reader.GetString(0),
                        FirstName = reader.IsDBNull(1) ? null : reader.GetString(1),
                        LastName = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Specialty = reader.IsDBNull(3) ? null : reader.GetString(3),
                        SyncedAt = reader.IsDBNull(4) ? null : reader.GetString(4),
                    }
                );
            }
            return Results.Ok(providers);
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.SyncRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

app.Run();

static object BuildSyncRecordsResponse(
    IReadOnlyList<SyncLogEntry> logs,
    string? search,
    int page,
    int pageSize
)
{
    // Records in _sync_log are captured changes ready for clients to pull.
    // Clients track their own sync position via fromVersion parameter.
    var records = logs.Select(l => new
    {
        id = l.Version.ToString(CultureInfo.InvariantCulture),
        entityType = l.TableName,
        entityId = l.PkValue,
        lastAttempt = l.Timestamp,
        operation = l.Operation,
    });

    if (!string.IsNullOrEmpty(search))
    {
        records = records.Where(r =>
            r.entityId.Contains(search, StringComparison.OrdinalIgnoreCase)
        );
    }

    var recordList = records.ToList();
    var total = recordList.Count;
    var pagedRecords = recordList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

    return new
    {
        records = pagedRecords,
        total,
        page,
        pageSize,
    };
}

namespace Clinical.Api
{
    /// <summary>
    /// Program entry point marker for WebApplicationFactory.
    /// </summary>
    public partial class Program { }
}
