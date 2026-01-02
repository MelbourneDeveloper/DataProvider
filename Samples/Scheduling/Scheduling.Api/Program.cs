#pragma warning disable IDE0037 // Use inferred member name - prefer explicit for clarity in API responses

using System.Collections.Immutable;
using System.Globalization;
using Microsoft.AspNetCore.Http.Json;
using Samples.Authorization;
using Scheduling.Api;
// Conduit result type aliases for dispatch results
using GetAllPractitionersConduitResult = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.GetAllPractitioners>,
    Conduit.ConduitError
>;
using GetPractitionerByIdConduitResult = Outcome.Result<
    Scheduling.Api.GetPractitionerByIdResult,
    Conduit.ConduitError
>;

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
    builder.Configuration["DbPath"] ?? Path.Combine(AppContext.BaseDirectory, "scheduling.db");
var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    ForeignKeys = true, // ENFORCE REFERENTIAL INTEGRITY
}.ToString();

// Register a FACTORY that creates new connections - NOT a singleton connection
Func<SqliteConnection> connectionFactory = () =>
{
    var conn = new SqliteConnection(connectionString);
    conn.Open();
    return conn;
};
builder.Services.AddSingleton(connectionFactory);

// Register Conduit registry with global behaviors + handlers
builder.Services.AddConduit(registry =>
    registry
        // Global behaviors - apply to ALL handlers automatically
        .WithGlobalLogging()
        .WithGlobalExceptionHandling()
        .WithGlobalTimeout(TimeSpan.FromSeconds(30))
        // Handlers
        .AddHandler<GetAllPractitionersQuery, ImmutableList<GetAllPractitioners>>(
            (req, ct) => SchedulingHandlers.HandleGetAllPractitioners(req, connectionFactory, ct)
        )
        .AddHandler<GetPractitionerByIdQuery, GetPractitionerByIdResult>(
            (req, ct) => SchedulingHandlers.HandleGetPractitionerById(req, connectionFactory, ct)
        )
);

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

// Health endpoint for sync service startup checks
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Scheduling.Api" }));

// Get Conduit registry and logger for dispatch
var conduitRegistry = app.Services.GetRequiredService<ConduitRegistry>();
var conduitLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Conduit");

// === CONDUIT-BASED ENDPOINTS ===
// These demonstrate the Conduit pattern - request/response through a pipeline

app.MapGet(
    "/conduit/Practitioner",
    async (CancellationToken ct) =>
    {
        var result = await Dispatcher
            .Send<GetAllPractitionersQuery, ImmutableList<GetAllPractitioners>>(
                request: new GetAllPractitionersQuery(),
                registry: conduitRegistry,
                logger: conduitLogger,
                cancellationToken: ct
            )
            .ConfigureAwait(false);

        return result switch
        {
            GetAllPractitionersConduitResult.Ok<
                ImmutableList<GetAllPractitioners>,
                ConduitError
            > ok => Results.Ok(ok.Value),
            GetAllPractitionersConduitResult.Error<
                ImmutableList<GetAllPractitioners>,
                ConduitError
            > err => Results.Problem(err.Value.ToString()),
        };
    }
);

app.MapGet(
    "/conduit/Practitioner/{id}",
    async (string id, CancellationToken ct) =>
    {
        var result = await Dispatcher
            .Send<GetPractitionerByIdQuery, GetPractitionerByIdResult>(
                request: new GetPractitionerByIdQuery(id),
                registry: conduitRegistry,
                logger: conduitLogger,
                cancellationToken: ct
            )
            .ConfigureAwait(false);

        return result switch
        {
            GetPractitionerByIdConduitResult.Ok<GetPractitionerByIdResult, ConduitError> ok
                when ok.Value.Found => Results.Ok(ok.Value.Practitioner),
            GetPractitionerByIdConduitResult.Ok<GetPractitionerByIdResult, ConduitError> =>
                Results.NotFound(),
            GetPractitionerByIdConduitResult.Error<GetPractitionerByIdResult, ConduitError> err =>
                Results.Problem(err.Value.ToString()),
        };
    }
);

// Get HttpClientFactory for auth filters
var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
Func<HttpClient> getGatekeeperClient = () => httpClientFactory.CreateClient("Gatekeeper");

// === FHIR PRACTITIONER ENDPOINTS ===

app.MapGet(
        "/Practitioner",
        async (Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var result = await conn.GetAllPractitionersAsync().ConfigureAwait(false);
            return result switch
            {
                GetAllPractitionersOk ok => Results.Ok(ok.Value),
                GetAllPractitionersError err => Results.Problem(err.Value.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.PractitionerRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

app.MapGet(
        "/Practitioner/{id}",
        async (string id, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var result = await conn.GetPractitionerByIdAsync(id).ConfigureAwait(false);
            return result switch
            {
                GetPractitionerByIdOk ok when ok.Value.Count > 0 => Results.Ok(ok.Value[0]),
                GetPractitionerByIdOk => Results.NotFound(),
                GetPractitionerByIdError err => Results.Problem(err.Value.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.PractitionerRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
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

            if (result is InsertOk)
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
                        TelecomPhone = request.TelecomPhone,
                    }
                );
            }

            return result switch
            {
                InsertOk => Results.Problem("Unexpected success after handling"),
                InsertError err => Results.Problem(err.Value.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.PractitionerCreate,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

app.MapPut(
        "/Practitioner/{id}",
        async (string id, UpdatePractitionerRequest request, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var transaction = (SqliteTransaction)
                await conn.BeginTransactionAsync().ConfigureAwait(false);
            await using var _ = transaction.ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
            UPDATE fhir_Practitioner
            SET NameFamily = @nameFamily,
                NameGiven = @nameGiven,
                Qualification = @qualification,
                Specialty = @specialty,
                TelecomEmail = @telecomEmail,
                TelecomPhone = @telecomPhone,
                Active = @active
            WHERE Id = @id
            """;
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@nameFamily", request.NameFamily);
            cmd.Parameters.AddWithValue("@nameGiven", request.NameGiven);
            cmd.Parameters.AddWithValue("@qualification", request.Qualification ?? string.Empty);
            cmd.Parameters.AddWithValue("@specialty", request.Specialty ?? string.Empty);
            cmd.Parameters.AddWithValue("@telecomEmail", request.TelecomEmail ?? string.Empty);
            cmd.Parameters.AddWithValue("@telecomPhone", request.TelecomPhone ?? string.Empty);
            cmd.Parameters.AddWithValue("@active", request.Active ? 1 : 0);

            var rowsAffected = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (rowsAffected > 0)
            {
                await transaction.CommitAsync().ConfigureAwait(false);
                return Results.Ok(
                    new
                    {
                        Id = id,
                        Identifier = request.Identifier,
                        Active = request.Active,
                        NameFamily = request.NameFamily,
                        NameGiven = request.NameGiven,
                        Qualification = request.Qualification,
                        Specialty = request.Specialty,
                        TelecomEmail = request.TelecomEmail,
                        TelecomPhone = request.TelecomPhone,
                    }
                );
            }

            return Results.NotFound();
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.PractitionerUpdate,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
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
                    SearchPractitionersOk ok => Results.Ok(ok.Value),
                    SearchPractitionersError err => Results.Problem(err.Value.Message),
                };
            }
            else
            {
                var result = await conn.GetAllPractitionersAsync().ConfigureAwait(false);
                return result switch
                {
                    GetAllPractitionersOk ok => Results.Ok(ok.Value),
                    GetAllPractitionersError err => Results.Problem(err.Value.Message),
                };
            }
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.PractitionerRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
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
                GetUpcomingAppointmentsOk ok => Results.Ok(ok.Value),
                GetUpcomingAppointmentsError err => Results.Problem(err.Value.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.AppointmentRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

app.MapGet(
        "/Appointment/{id}",
        async (string id, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var result = await conn.GetAppointmentByIdAsync(id).ConfigureAwait(false);
            return result switch
            {
                GetAppointmentByIdOk ok when ok.Value.Count > 0 => Results.Ok(ok.Value[0]),
                GetAppointmentByIdOk => Results.NotFound(),
                GetAppointmentByIdError err => Results.Problem(err.Value.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequireResourcePermission(
            FhirPermissions.AppointmentRead,
            signingKey,
            getGatekeeperClient,
            app.Logger,
            "id"
        )
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

            if (result is InsertOk)
            {
                await transaction.CommitAsync().ConfigureAwait(false);
                return Results.Created(
                    $"/Appointment/{id}",
                    new
                    {
                        Id = id,
                        Status = "booked",
                        ServiceCategory = request.ServiceCategory,
                        ServiceType = request.ServiceType,
                        ReasonCode = request.ReasonCode,
                        Priority = request.Priority,
                        Description = request.Description,
                        Start = request.Start,
                        End = request.End,
                        MinutesDuration = durationMinutes,
                        PatientReference = request.PatientReference,
                        PractitionerReference = request.PractitionerReference,
                        Created = now,
                        Comment = request.Comment,
                    }
                );
            }

            return result switch
            {
                InsertOk => Results.Problem("Unexpected success after handling"),
                InsertError err => Results.Problem(err.Value.DetailedMessage),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.AppointmentCreate,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

app.MapPut(
        "/Appointment/{id}",
        async (string id, UpdateAppointmentRequest request, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var transaction = (SqliteTransaction)
                await conn.BeginTransactionAsync().ConfigureAwait(false);
            await using var _ = transaction.ConfigureAwait(false);

            var start = DateTime.Parse(request.Start, CultureInfo.InvariantCulture);
            var end = DateTime.Parse(request.End, CultureInfo.InvariantCulture);
            var durationMinutes = (int)(end - start).TotalMinutes;

            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
            UPDATE fhir_Appointment
            SET ServiceCategory = @serviceCategory,
                ServiceType = @serviceType,
                ReasonCode = @reasonCode,
                Priority = @priority,
                Description = @description,
                StartTime = @start,
                EndTime = @end,
                MinutesDuration = @duration,
                PatientReference = @patientRef,
                PractitionerReference = @practitionerRef,
                Comment = @comment,
                Status = @status
            WHERE Id = @id
            """;
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue(
                "@serviceCategory",
                request.ServiceCategory ?? string.Empty
            );
            cmd.Parameters.AddWithValue("@serviceType", request.ServiceType ?? string.Empty);
            cmd.Parameters.AddWithValue("@reasonCode", request.ReasonCode ?? string.Empty);
            cmd.Parameters.AddWithValue("@priority", request.Priority);
            cmd.Parameters.AddWithValue("@description", request.Description ?? string.Empty);
            cmd.Parameters.AddWithValue("@start", request.Start);
            cmd.Parameters.AddWithValue("@end", request.End);
            cmd.Parameters.AddWithValue("@duration", durationMinutes);
            cmd.Parameters.AddWithValue("@patientRef", request.PatientReference);
            cmd.Parameters.AddWithValue("@practitionerRef", request.PractitionerReference);
            cmd.Parameters.AddWithValue("@comment", request.Comment ?? string.Empty);
            cmd.Parameters.AddWithValue("@status", request.Status);

            var rowsAffected = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (rowsAffected > 0)
            {
                await transaction.CommitAsync().ConfigureAwait(false);
                return Results.Ok(
                    new
                    {
                        Id = id,
                        Status = request.Status,
                        ServiceCategory = request.ServiceCategory,
                        ServiceType = request.ServiceType,
                        ReasonCode = request.ReasonCode,
                        Priority = request.Priority,
                        Description = request.Description,
                        Start = request.Start,
                        End = request.End,
                        MinutesDuration = durationMinutes,
                        PatientReference = request.PatientReference,
                        PractitionerReference = request.PractitionerReference,
                        Comment = request.Comment,
                    }
                );
            }

            return Results.NotFound();
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequireResourcePermission(
            FhirPermissions.AppointmentUpdate,
            signingKey,
            getGatekeeperClient,
            app.Logger,
            "id"
        )
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
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequireResourcePermission(
            FhirPermissions.AppointmentUpdate,
            signingKey,
            getGatekeeperClient,
            app.Logger,
            "id"
        )
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
                GetAppointmentsByPatientOk ok => Results.Ok(ok.Value),
                GetAppointmentsByPatientError err => Results.Problem(err.Value.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePatientPermission(
            FhirPermissions.AppointmentRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
    );

app.MapGet(
        "/Practitioner/{practitionerId}/Appointment",
        async (string practitionerId, Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            var result = await conn.GetAppointmentsByPractitionerAsync(
                    $"Practitioner/{practitionerId}"
                )
                .ConfigureAwait(false);
            return result switch
            {
                GetAppointmentsByPractitionerOk ok => Results.Ok(ok.Value),
                GetAppointmentsByPractitionerError err => Results.Problem(err.Value.Message),
            };
        }
    )
    .AddEndpointFilterFactory(
        EndpointFilterFactories.RequirePermission(
            FhirPermissions.AppointmentRead,
            signingKey,
            getGatekeeperClient,
            app.Logger
        )
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
                StringSyncOk ok => Results.Ok(new { originId = ok.Value }),
                StringSyncError err => Results.Problem(SyncHelpers.ToMessage(err.Value)),
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
            var (pendingCount, failedCount, lastSyncTime) = changesResult switch
            {
                SyncLogListOk(var logs) => (
                    logs.Count(l => l.Operation == SyncOperation.Insert),
                    0,
                    logs.Count > 0
                        ? logs.Max(l => l.Timestamp)
                        : DateTime.UtcNow.ToString(
                            "yyyy-MM-ddTHH:mm:ss.fffZ",
                            CultureInfo.InvariantCulture
                        )
                ),
                SyncLogListError => (
                    0,
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
                    service = "Scheduling.Api",
                    status = "healthy",
                    lastSyncTime,
                    pendingCount,
                    failedCount,
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
        (
            string? status,
            string? search,
            int? page,
            int? pageSize,
            Func<SqliteConnection> getConn
        ) =>
        {
            using var conn = getConn();
            var currentPage = page ?? 1;
            var size = pageSize ?? 50;
            var changesResult = SyncLogRepository.FetchChanges(conn, 0, 1000);

            return changesResult switch
            {
                SyncLogListOk(var logs) => Results.Ok(
                    BuildSyncRecordsResponse(logs, status, search, currentPage, size)
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

// Query synced patients from Clinical domain
app.MapGet(
        "/sync/patients",
        (Func<SqliteConnection> getConn) =>
        {
            using var conn = getConn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT PatientId, DisplayName, ContactPhone, ContactEmail, SyncedAt FROM sync_ScheduledPatient";
            using var reader = cmd.ExecuteReader();
            var patients = new List<object>();
            while (reader.Read())
            {
                patients.Add(
                    new
                    {
                        PatientId = reader.GetString(0),
                        DisplayName = reader.GetString(1),
                        ContactPhone = reader.IsDBNull(2) ? null : reader.GetString(2),
                        ContactEmail = reader.IsDBNull(3) ? null : reader.GetString(3),
                        SyncedAt = reader.GetString(4),
                    }
                );
            }
            return Results.Ok(patients);
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
    string? statusFilter,
    string? search,
    int page,
    int pageSize
)
{
    var records = logs.Select(l => new
    {
        id = l.Version.ToString(CultureInfo.InvariantCulture),
        entityType = l.TableName,
        entityId = l.PkValue,
        status = "pending",
        lastAttempt = l.Timestamp,
        operation = l.Operation,
    });

    if (!string.IsNullOrEmpty(statusFilter))
    {
        records = records.Where(r => r.status == statusFilter);
    }

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

namespace Scheduling.Api
{
    /// <summary>
    /// Program entry point marker for WebApplicationFactory.
    /// </summary>
    public partial class Program { }
}
