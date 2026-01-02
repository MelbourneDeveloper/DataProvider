#pragma warning disable IDE0037 // Use inferred member name - prefer explicit for clarity in API responses

using System.Collections.Immutable;
using System.Globalization;
using Clinical.Api;
using Conduit;
using Microsoft.AspNetCore.Http.Json;
using Samples.Authorization;

var builder = WebApplication.CreateBuilder(args);

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
var getConn = app.Services.GetRequiredService<Func<SqliteConnection>>();
var logger = app.Logger;

// Build Conduit registry with global behaviors + handlers
var registry = ConduitRegistry
    .Empty
    // Global behaviors - apply to ALL handlers automatically
    .WithGlobalLogging()
    .WithGlobalExceptionHandling()
    .WithGlobalTimeout(TimeSpan.FromSeconds(30))
    // Handlers
    .AddHandler<GetPatientsQuery, ImmutableList<GetPatients>>(
        (req, ct) => ClinicalHandlers.HandleGetPatients(req, getConn, ct)
    )
    .AddHandler<GetPatientByIdQuery, GetPatientByIdResult>(
        (req, ct) => ClinicalHandlers.HandleGetPatientById(req, getConn, ct)
    )
    .AddHandler<CreatePatientCommand, CreatedPatient>(
        (req, ct) => ClinicalHandlers.HandleCreatePatient(req, getConn, ct)
    )
    .AddHandler<UpdatePatientCommand, UpdatedPatient>(
        (req, ct) => ClinicalHandlers.HandleUpdatePatient(req, getConn, ct)
    )
    .AddHandler<SearchPatientsQuery, ImmutableList<SearchPatients>>(
        (req, ct) => ClinicalHandlers.HandleSearchPatients(req, getConn, ct)
    )
    .AddHandler<GetEncountersQuery, ImmutableList<GetEncountersByPatient>>(
        (req, ct) => ClinicalHandlers.HandleGetEncounters(req, getConn, ct)
    )
    .AddHandler<CreateEncounterCommand, CreatedEncounter>(
        (req, ct) => ClinicalHandlers.HandleCreateEncounter(req, getConn, ct)
    )
    .AddHandler<GetConditionsQuery, ImmutableList<GetConditionsByPatient>>(
        (req, ct) => ClinicalHandlers.HandleGetConditions(req, getConn, ct)
    )
    .AddHandler<CreateConditionCommand, CreatedCondition>(
        (req, ct) => ClinicalHandlers.HandleCreateCondition(req, getConn, ct)
    )
    .AddHandler<GetMedicationsQuery, ImmutableList<GetMedicationsByPatient>>(
        (req, ct) => ClinicalHandlers.HandleGetMedications(req, getConn, ct)
    )
    .AddHandler<CreateMedicationCommand, CreatedMedication>(
        (req, ct) => ClinicalHandlers.HandleCreateMedication(req, getConn, ct)
    )
    .AddHandler<GetSyncChangesQuery, SyncChangesResult>(
        (req, ct) => ClinicalHandlers.HandleGetSyncChanges(req, getConn, ct)
    )
    .AddHandler<GetSyncOriginQuery, SyncOriginResult>(
        (req, ct) => ClinicalHandlers.HandleGetSyncOrigin(req, getConn, ct)
    );

var patientGroup = app.MapGroup("/fhir/Patient").WithTags("Patient");

patientGroup
    .MapGet(
        "/",
        async (bool? active, string? familyName, string? givenName, string? gender) =>
        {
            var result = await Dispatcher
                .Send<GetPatientsQuery, ImmutableList<GetPatients>>(
                    request: new GetPatientsQuery(
                        Active: active,
                        FamilyName: familyName,
                        GivenName: givenName,
                        Gender: gender
                    ),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<ImmutableList<GetPatients>, ConduitError>.Ok<
                    ImmutableList<GetPatients>,
                    ConduitError
                > ok => Results.Ok(ok.Value),
                Result<ImmutableList<GetPatients>, ConduitError>.Error<
                    ImmutableList<GetPatients>,
                    ConduitError
                > err => Results.Problem(err.Value.ToString()),
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
        async (string id) =>
        {
            var result = await Dispatcher
                .Send<GetPatientByIdQuery, GetPatientByIdResult>(
                    request: new GetPatientByIdQuery(Id: id),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<GetPatientByIdResult, ConduitError>.Ok<GetPatientByIdResult, ConduitError> ok
                    when ok.Value.Found => Results.Ok(ok.Value.Patient),
                Result<GetPatientByIdResult, ConduitError>.Ok<GetPatientByIdResult, ConduitError> =>
                    Results.NotFound(),
                Result<GetPatientByIdResult, ConduitError>.Error<
                    GetPatientByIdResult,
                    ConduitError
                > err => Results.Problem(err.Value.ToString()),
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
        async (CreatePatientRequest request) =>
        {
            var result = await Dispatcher
                .Send<CreatePatientCommand, CreatedPatient>(
                    request: new CreatePatientCommand(Request: request),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<CreatedPatient, ConduitError>.Ok<CreatedPatient, ConduitError> ok =>
                    Results.Created(
                        $"/fhir/Patient/{ok.Value.Id}",
                        new
                        {
                            Id = ok.Value.Id,
                            Active = ok.Value.Active,
                            GivenName = ok.Value.GivenName,
                            FamilyName = ok.Value.FamilyName,
                            BirthDate = ok.Value.BirthDate,
                            Gender = ok.Value.Gender,
                            Phone = ok.Value.Phone,
                            Email = ok.Value.Email,
                            AddressLine = ok.Value.AddressLine,
                            City = ok.Value.City,
                            State = ok.Value.State,
                            PostalCode = ok.Value.PostalCode,
                            Country = ok.Value.Country,
                            LastUpdated = ok.Value.LastUpdated,
                            VersionId = ok.Value.VersionId,
                        }
                    ),
                Result<CreatedPatient, ConduitError>.Error<CreatedPatient, ConduitError> err =>
                    Results.Problem(err.Value.ToString()),
            };
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
        async (string id, UpdatePatientRequest request) =>
        {
            var result = await Dispatcher
                .Send<UpdatePatientCommand, UpdatedPatient>(
                    request: new UpdatePatientCommand(Id: id, Request: request),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<UpdatedPatient, ConduitError>.Ok<UpdatedPatient, ConduitError> ok
                    when ok.Value.Found => Results.Ok(
                    new
                    {
                        Id = id,
                        Active = ok.Value.Patient!.Active,
                        GivenName = ok.Value.Patient.GivenName,
                        FamilyName = ok.Value.Patient.FamilyName,
                        BirthDate = ok.Value.Patient.BirthDate,
                        Gender = ok.Value.Patient.Gender,
                        Phone = ok.Value.Patient.Phone,
                        Email = ok.Value.Patient.Email,
                        AddressLine = ok.Value.Patient.AddressLine,
                        City = ok.Value.Patient.City,
                        State = ok.Value.Patient.State,
                        PostalCode = ok.Value.Patient.PostalCode,
                        Country = ok.Value.Patient.Country,
                        LastUpdated = ok.Value.Patient.LastUpdated,
                        VersionId = ok.Value.Patient.VersionId,
                    }
                ),
                Result<UpdatedPatient, ConduitError>.Ok<UpdatedPatient, ConduitError> =>
                    Results.NotFound(),
                Result<UpdatedPatient, ConduitError>.Error<UpdatedPatient, ConduitError> err =>
                    Results.Problem(err.Value.ToString()),
            };
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
        async (string q) =>
        {
            var result = await Dispatcher
                .Send<SearchPatientsQuery, ImmutableList<SearchPatients>>(
                    request: new SearchPatientsQuery(Query: q),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<ImmutableList<SearchPatients>, ConduitError>.Ok<
                    ImmutableList<SearchPatients>,
                    ConduitError
                > ok => Results.Ok(ok.Value),
                Result<ImmutableList<SearchPatients>, ConduitError>.Error<
                    ImmutableList<SearchPatients>,
                    ConduitError
                > err => Results.Problem(err.Value.ToString()),
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
        async (string patientId) =>
        {
            var result = await Dispatcher
                .Send<GetEncountersQuery, ImmutableList<GetEncountersByPatient>>(
                    request: new GetEncountersQuery(PatientId: patientId),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<ImmutableList<GetEncountersByPatient>, ConduitError>.Ok<
                    ImmutableList<GetEncountersByPatient>,
                    ConduitError
                > ok => Results.Ok(ok.Value),
                Result<ImmutableList<GetEncountersByPatient>, ConduitError>.Error<
                    ImmutableList<GetEncountersByPatient>,
                    ConduitError
                > err => Results.Problem(err.Value.ToString()),
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
        async (string patientId, CreateEncounterRequest request) =>
        {
            var result = await Dispatcher
                .Send<CreateEncounterCommand, CreatedEncounter>(
                    request: new CreateEncounterCommand(PatientId: patientId, Request: request),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<CreatedEncounter, ConduitError>.Ok<CreatedEncounter, ConduitError> ok =>
                    Results.Created(
                        $"/fhir/Patient/{patientId}/Encounter/{ok.Value.Id}",
                        new
                        {
                            Id = ok.Value.Id,
                            Status = ok.Value.Status,
                            Class = ok.Value.Class,
                            PatientId = patientId,
                            PractitionerId = ok.Value.PractitionerId,
                            ServiceType = ok.Value.ServiceType,
                            ReasonCode = ok.Value.ReasonCode,
                            PeriodStart = ok.Value.PeriodStart,
                            PeriodEnd = ok.Value.PeriodEnd,
                            Notes = ok.Value.Notes,
                            LastUpdated = ok.Value.LastUpdated,
                            VersionId = ok.Value.VersionId,
                        }
                    ),
                Result<CreatedEncounter, ConduitError>.Error<CreatedEncounter, ConduitError> err =>
                    Results.Problem(err.Value.ToString()),
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
        async (string patientId) =>
        {
            var result = await Dispatcher
                .Send<GetConditionsQuery, ImmutableList<GetConditionsByPatient>>(
                    request: new GetConditionsQuery(PatientId: patientId),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<ImmutableList<GetConditionsByPatient>, ConduitError>.Ok<
                    ImmutableList<GetConditionsByPatient>,
                    ConduitError
                > ok => Results.Ok(ok.Value),
                Result<ImmutableList<GetConditionsByPatient>, ConduitError>.Error<
                    ImmutableList<GetConditionsByPatient>,
                    ConduitError
                > err => Results.Problem(err.Value.ToString()),
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
        async (string patientId, CreateConditionRequest request) =>
        {
            var result = await Dispatcher
                .Send<CreateConditionCommand, CreatedCondition>(
                    request: new CreateConditionCommand(PatientId: patientId, Request: request),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<CreatedCondition, ConduitError>.Ok<CreatedCondition, ConduitError> ok =>
                    Results.Created(
                        $"/fhir/Patient/{patientId}/Condition/{ok.Value.Id}",
                        new
                        {
                            Id = ok.Value.Id,
                            ClinicalStatus = ok.Value.ClinicalStatus,
                            VerificationStatus = ok.Value.VerificationStatus,
                            Category = ok.Value.Category,
                            Severity = ok.Value.Severity,
                            CodeSystem = ok.Value.CodeSystem,
                            CodeValue = ok.Value.CodeValue,
                            CodeDisplay = ok.Value.CodeDisplay,
                            SubjectReference = patientId,
                            EncounterReference = ok.Value.EncounterReference,
                            OnsetDateTime = ok.Value.OnsetDateTime,
                            RecordedDate = ok.Value.RecordedDate,
                            RecorderReference = ok.Value.RecorderReference,
                            NoteText = ok.Value.NoteText,
                            LastUpdated = ok.Value.LastUpdated,
                            VersionId = ok.Value.VersionId,
                        }
                    ),
                Result<CreatedCondition, ConduitError>.Error<CreatedCondition, ConduitError> err =>
                    Results.Problem(err.Value.ToString()),
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
        async (string patientId) =>
        {
            var result = await Dispatcher
                .Send<GetMedicationsQuery, ImmutableList<GetMedicationsByPatient>>(
                    request: new GetMedicationsQuery(PatientId: patientId),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<ImmutableList<GetMedicationsByPatient>, ConduitError>.Ok<
                    ImmutableList<GetMedicationsByPatient>,
                    ConduitError
                > ok => Results.Ok(ok.Value),
                Result<ImmutableList<GetMedicationsByPatient>, ConduitError>.Error<
                    ImmutableList<GetMedicationsByPatient>,
                    ConduitError
                > err => Results.Problem(err.Value.ToString()),
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
        async (string patientId, CreateMedicationRequestRequest request) =>
        {
            var result = await Dispatcher
                .Send<CreateMedicationCommand, CreatedMedication>(
                    request: new CreateMedicationCommand(PatientId: patientId, Request: request),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<CreatedMedication, ConduitError>.Ok<CreatedMedication, ConduitError> ok =>
                    Results.Created(
                        $"/fhir/Patient/{patientId}/MedicationRequest/{ok.Value.Id}",
                        new
                        {
                            Id = ok.Value.Id,
                            Status = ok.Value.Status,
                            Intent = ok.Value.Intent,
                            PatientId = patientId,
                            PractitionerId = ok.Value.PractitionerId,
                            EncounterId = ok.Value.EncounterId,
                            MedicationCode = ok.Value.MedicationCode,
                            MedicationDisplay = ok.Value.MedicationDisplay,
                            DosageInstruction = ok.Value.DosageInstruction,
                            Quantity = ok.Value.Quantity,
                            Unit = ok.Value.Unit,
                            Refills = ok.Value.Refills,
                            AuthoredOn = ok.Value.AuthoredOn,
                            LastUpdated = ok.Value.LastUpdated,
                            VersionId = ok.Value.VersionId,
                        }
                    ),
                Result<CreatedMedication, ConduitError>.Error<
                    CreatedMedication,
                    ConduitError
                > err => Results.Problem(err.Value.ToString()),
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
        async (long? fromVersion, int? limit) =>
        {
            var result = await Dispatcher
                .Send<GetSyncChangesQuery, SyncChangesResult>(
                    request: new GetSyncChangesQuery(
                        FromVersion: fromVersion ?? 0,
                        Limit: limit ?? 100
                    ),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<SyncChangesResult, ConduitError>.Ok<SyncChangesResult, ConduitError> ok =>
                    Results.Ok(ok.Value.Changes),
                Result<SyncChangesResult, ConduitError>.Error<
                    SyncChangesResult,
                    ConduitError
                > err => Results.Problem(err.Value.ToString()),
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
        async () =>
        {
            var result = await Dispatcher
                .Send<GetSyncOriginQuery, SyncOriginResult>(
                    request: new GetSyncOriginQuery(),
                    registry: registry,
                    logger: logger
                )
                .ConfigureAwait(false);

            return result switch
            {
                Result<SyncOriginResult, ConduitError>.Ok<SyncOriginResult, ConduitError> ok =>
                    Results.Ok(new { originId = ok.Value.OriginId }),
                Result<SyncOriginResult, ConduitError>.Error<SyncOriginResult, ConduitError> err =>
                    Results.Problem(err.Value.ToString()),
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
        (Func<SqliteConnection> getConnLocal) =>
        {
            using var conn = getConnLocal();
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
        (string? search, int? page, int? pageSize, Func<SqliteConnection> getConnLocal) =>
        {
            using var conn = getConnLocal();
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
        (Func<SqliteConnection> getConnLocal) =>
        {
            using var conn = getConnLocal();
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
