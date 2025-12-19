// Healthcare API - ASP.NET Minimal API with SQLite and DataProvider SQL
// FHIR R4 aligned resources: Patient, Encounter, MedicationRequest, Practitioner, Organization
// Data access via DataProvider-generated extension methods from SQL files

using System.Collections.Immutable;
using Microsoft.Data.Sqlite;
using Healthcare.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register SQLite connection factory
builder.Services.AddScoped<SqliteConnection>(_ =>
{
    var conn = new SqliteConnection("Data Source=healthcare.db");
    conn.Open();
    return conn;
});

var app = builder.Build();

// Initialize database on startup
await using (var initConn = new SqliteConnection("Data Source=healthcare.db"))
{
    await initConn.OpenAsync();
    var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema.sql");
    if (File.Exists(schemaPath))
    {
        var schemaSql = await File.ReadAllTextAsync(schemaPath);
        await using var cmd = initConn.CreateCommand();
        cmd.CommandText = schemaSql;
        await cmd.ExecuteNonQueryAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Patient endpoints
app.MapGet("/Patient", async (SqliteConnection conn) =>
{
    // DataProvider generates: conn.GetPatientsForSync(lastSyncDate)
    // For now, return empty until code gen runs
    return Results.Ok(ImmutableArray<object>.Empty);
})
.WithName("GetPatients")
.WithTags("Patient");

app.MapGet("/Patient/{id}", async (string id, SqliteConnection conn) =>
{
    // DataProvider generates: conn.GetPatientWithEncounters(patientId)
    // Returns parent-child grouped result
    return Results.Ok(new { Id = id, Message = "DataProvider code generation required" });
})
.WithName("GetPatient")
.WithTags("Patient");

app.MapPost("/Patient", async (PatientInput patient, SqliteConnection conn) =>
{
    // DataProvider generates: conn.InsertPatient(...)
    var id = Guid.NewGuid().ToString();
    return Results.Created($"/Patient/{id}", new { Id = id });
})
.WithName("CreatePatient")
.WithTags("Patient");

// Encounter endpoints
app.MapGet("/Encounter", async (string? claimStatus, string? startDate, string? endDate, SqliteConnection conn) =>
{
    // DataProvider generates: conn.GetEncountersForClaims(claimStatus, startDate, endDate)
    return Results.Ok(ImmutableArray<object>.Empty);
})
.WithName("GetEncounters")
.WithTags("Encounter");

app.MapGet("/Encounter/{id}", async (string id, SqliteConnection conn) =>
{
    return Results.Ok(new { Id = id, Message = "DataProvider code generation required" });
})
.WithName("GetEncounter")
.WithTags("Encounter");

app.MapPost("/Encounter", async (EncounterInput encounter, SqliteConnection conn) =>
{
    // DataProvider generates: conn.InsertEncounter(...)
    var id = Guid.NewGuid().ToString();
    return Results.Created($"/Encounter/{id}", new { Id = id });
})
.WithName("CreateEncounter")
.WithTags("Encounter");

// Sync endpoints - expose data for Insurance API to pull
app.MapGet("/sync/patients", async (DateTime? lastSyncDate, SqliteConnection conn) =>
{
    // DataProvider generates: conn.GetPatientsForSync(lastSyncDate)
    // Returns patients with insurance info ready for sync to Insurance.Member
    return Results.Ok(ImmutableArray<object>.Empty);
})
.WithName("GetPatientsForSync")
.WithTags("Sync");

app.MapGet("/sync/encounters", async (string? claimStatus, SqliteConnection conn) =>
{
    // DataProvider generates: conn.GetEncountersForClaims(claimStatus, startDate, endDate)
    // Returns encounters ready for sync to Insurance.Claim
    return Results.Ok(ImmutableArray<object>.Empty);
})
.WithName("GetEncountersForSync")
.WithTags("Sync");

app.Run();

namespace Healthcare.Api
{
    /// <summary>
    /// FHIR Patient input for creation.
    /// </summary>
    public sealed record PatientInput(
        string Identifier,
        string FamilyName,
        string GivenName,
        string BirthDate,
        string Gender,
        string? TelecomEmail,
        string? TelecomPhone,
        string? AddressLine,
        string? AddressCity,
        string? AddressState,
        string? AddressPostalCode,
        string? ExtInsurancePolicyNumber,
        string? ExtInsuranceGroupNumber,
        string? ExtInsurancePayerId);

    /// <summary>
    /// FHIR Encounter input for creation.
    /// </summary>
    public sealed record EncounterInput(
        string Status,
        string Class,
        string Type,
        string TypeDisplay,
        string SubjectId,
        string ParticipantId,
        string ParticipantName,
        string PeriodStart,
        string? PeriodEnd,
        string? ReasonCode,
        string? ReasonDisplay,
        string? DiagnosisCode,
        string? DiagnosisDisplay,
        string ServiceProviderId,
        string ServiceProviderName,
        decimal ExtTotalCharge);
}
