#pragma warning disable CS8509 // Exhaustive switch - Exhaustion analyzer handles this
#pragma warning disable IDE0037 // Use inferred member name - prefer explicit for clarity in API responses

using System.Globalization;
using Microsoft.AspNetCore.Http.Json;
using Scheduling.Api;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON to use PascalCase property names
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});

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

var dbPath =
    builder.Configuration["DbPath"] ?? Path.Combine(AppContext.BaseDirectory, "scheduling.db");
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
            GetAppointmentsByPatientOk ok => Results.Ok(ok.Value),
            GetAppointmentsByPatientError err => Results.Problem(err.Value.Message),
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
            GetAppointmentsByPractitionerOk ok => Results.Ok(ok.Value),
            GetAppointmentsByPractitionerError err => Results.Problem(err.Value.Message),
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
        };
    }
);

app.Run();

namespace Scheduling.Api
{
    /// <summary>
    /// Program entry point marker for WebApplicationFactory.
    /// </summary>
    public partial class Program { }
}
