using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Scheduling.Api.Tests;

/// <summary>
/// Sync tests for Scheduling domain.
/// </summary>
public sealed class SchedulingSyncTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _schedulingFactory;
    private readonly HttpClient _schedulingClient;
    private readonly string _schedulingDbPath;
    private static readonly string AuthToken = TestTokenHelper.GenerateSchedulerToken();

    /// <summary>
    /// Creates test instance with Scheduling API running.
    /// </summary>
    public SchedulingSyncTests()
    {
        _schedulingDbPath = Path.Combine(
            Path.GetTempPath(),
            $"scheduling_sync_test_{Guid.NewGuid()}.db"
        );

        _schedulingFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("DbPath", _schedulingDbPath)
        );

        _schedulingClient = _schedulingFactory.CreateClient();
        _schedulingClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AuthToken
        );
    }

    /// <summary>
    /// Creating a practitioner in Scheduling.Api creates a sync log entry.
    /// </summary>
    [Fact]
    public async Task CreatePractitionerInScheduling_GeneratesSyncLogEntry()
    {
        var practitionerRequest = new
        {
            Identifier = "NPI-SYNC-FULL",
            NameFamily = "SyncDoctor",
            NameGiven = "Full",
            Specialty = "General Practice",
        };

        await _schedulingClient.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(
            changes,
            c => c.GetProperty("TableName").GetString() == "fhir_Practitioner"
        );
    }

    /// <summary>
    /// Sync log contains practitioner data with proper payload.
    /// </summary>
    [Fact]
    public async Task SchedulingSyncLog_ContainsPractitionerPayload()
    {
        var practitionerRequest = new
        {
            Identifier = "NPI-DATA-SYNC",
            NameFamily = "DataDoctor",
            NameGiven = "Sync",
            Specialty = "Cardiology",
        };

        await _schedulingClient.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);

        var practitionerChange = changes.FirstOrDefault(c =>
            c.GetProperty("TableName").GetString() == "fhir_Practitioner"
        );

        Assert.True(practitionerChange.ValueKind != JsonValueKind.Undefined);
        Assert.True(practitionerChange.TryGetProperty("Payload", out _));
    }

    /// <summary>
    /// Scheduling domain has a unique origin ID.
    /// </summary>
    [Fact]
    public async Task SchedulingDomain_HasUniqueOriginId()
    {
        var response = await _schedulingClient.GetAsync("/sync/origin");
        var origin = await response.Content.ReadFromJsonAsync<JsonElement>();
        var originId = origin.GetProperty("originId").GetString();

        Assert.NotNull(originId);
        Assert.NotEmpty(originId);
        Assert.Matches(@"^[0-9a-fA-F-]{36}$", originId);
    }

    /// <summary>
    /// Sync log versions increment correctly across multiple changes.
    /// </summary>
    [Fact]
    public async Task SyncLogVersions_IncrementCorrectly()
    {
        for (var i = 0; i < 5; i++)
        {
            var request = new
            {
                Identifier = $"NPI-VERSION-{i}",
                NameFamily = $"VersionDoc{i}",
                NameGiven = "Test",
            };
            await _schedulingClient.PostAsJsonAsync("/Practitioner", request);
        }

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.True(changes.Length >= 5);

        long previousVersion = 0;
        foreach (var change in changes)
        {
            var currentVersion = change.GetProperty("Version").GetInt64();
            Assert.True(currentVersion > previousVersion);
            previousVersion = currentVersion;
        }
    }

    /// <summary>
    /// Sync log fromVersion parameter filters correctly.
    /// </summary>
    [Fact]
    public async Task SyncLogFromVersion_FiltersCorrectly()
    {
        var request1 = new
        {
            Identifier = "NPI-FIRST",
            NameFamily = "FirstDoc",
            NameGiven = "Test",
        };
        await _schedulingClient.PostAsJsonAsync("/Practitioner", request1);

        var initialResponse = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var initialChanges = await initialResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(initialChanges);
        Assert.True(initialChanges.Length > 0);
        var lastVersion = initialChanges.Max(c => c.GetProperty("Version").GetInt64());

        var request2 = new
        {
            Identifier = "NPI-SECOND",
            NameFamily = "SecondDoc",
            NameGiven = "Test",
        };
        await _schedulingClient.PostAsJsonAsync("/Practitioner", request2);

        var filteredResponse = await _schedulingClient.GetAsync(
            $"/sync/changes?fromVersion={lastVersion}"
        );
        var filteredChanges = await filteredResponse.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(filteredChanges);
        Assert.All(
            filteredChanges,
            c => Assert.True(c.GetProperty("Version").GetInt64() > lastVersion)
        );
    }

    /// <summary>
    /// Creating an appointment in Scheduling creates a sync log entry.
    /// </summary>
    [Fact]
    public async Task CreateAppointment_InScheduling_GeneratesSyncLogEntry()
    {
        var practitionerRequest = new
        {
            Identifier = $"NPI-APPT-{Guid.NewGuid():N}",
            NameFamily = "AppointmentDoc",
            NameGiven = "Test",
        };
        var practitionerResponse = await _schedulingClient.PostAsJsonAsync(
            "/Practitioner",
            practitionerRequest
        );
        var practitioner = await practitionerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var practitionerId = practitioner.GetProperty("Id").GetString();

        var appointmentRequest = new
        {
            ServiceCategory = "Test",
            ServiceType = "Sync Test",
            Priority = "routine",
            Start = "2025-08-01T10:00:00Z",
            End = "2025-08-01T10:30:00Z",
            PatientReference = "Patient/test-patient",
            PractitionerReference = $"Practitioner/{practitionerId}",
        };
        var appointmentResponse = await _schedulingClient.PostAsJsonAsync(
            "/Appointment",
            appointmentRequest
        );
        Assert.True(appointmentResponse.IsSuccessStatusCode);

        var changesResponse = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await changesResponse.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(changes, c => c.GetProperty("TableName").GetString() == "fhir_Appointment");
    }

    /// <summary>
    /// Sync log limit parameter correctly restricts result count.
    /// </summary>
    [Fact]
    public async Task SyncLogLimit_RestrictsResultCount()
    {
        for (var i = 0; i < 5; i++)
        {
            var request = new
            {
                Identifier = $"NPI-LIMIT-{Guid.NewGuid():N}",
                NameFamily = $"LimitDoc{i}",
                NameGiven = "Test",
            };
            await _schedulingClient.PostAsJsonAsync("/Practitioner", request);
        }

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0&limit=3");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Equal(3, changes.Length);
    }

    /// <summary>
    /// Sync changes include INSERT operation type.
    /// </summary>
    [Fact]
    public async Task SyncChanges_IncludeOperationType()
    {
        var request = new
        {
            Identifier = $"NPI-OP-{Guid.NewGuid():N}",
            NameFamily = "OperationDoc",
            NameGiven = "Test",
        };
        await _schedulingClient.PostAsJsonAsync("/Practitioner", request);

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.All(changes, c => Assert.True(c.TryGetProperty("Operation", out _)));

        var practitionerChange = changes.First(c =>
            c.GetProperty("TableName").GetString() == "fhir_Practitioner"
        );
        // Operation is serialized as integer (0=Insert, 1=Update, 2=Delete)
        Assert.Equal(0, practitionerChange.GetProperty("Operation").GetInt32());
    }

    /// <summary>
    /// Sync changes include timestamp.
    /// </summary>
    [Fact]
    public async Task SyncChanges_IncludeTimestamp()
    {
        var request = new
        {
            Identifier = $"NPI-TS-{Guid.NewGuid():N}",
            NameFamily = "TimestampDoc",
            NameGiven = "Test",
        };
        await _schedulingClient.PostAsJsonAsync("/Practitioner", request);

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.All(changes, c => Assert.True(c.TryGetProperty("Timestamp", out _)));
    }

    /// <summary>
    /// Sync data contains expected practitioner fields.
    /// </summary>
    [Fact]
    public async Task SyncData_ContainsExpectedPractitionerFields()
    {
        var request = new
        {
            Identifier = "NPI-FIELDS-123",
            NameFamily = "FieldsDoctor",
            NameGiven = "John",
            Specialty = "Neurology",
            Qualification = "MD, PhD",
            TelecomEmail = "doctor@fields.com",
            TelecomPhone = "555-DOCS",
        };
        await _schedulingClient.PostAsJsonAsync("/Practitioner", request);

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        var practitionerChange = changes.First(c =>
            c.GetProperty("TableName").GetString() == "fhir_Practitioner"
        );

        var payloadStr = practitionerChange.GetProperty("Payload").GetString();
        Assert.NotNull(payloadStr);

        var payload = JsonSerializer.Deserialize<JsonElement>(payloadStr);
        Assert.Equal("NPI-FIELDS-123", payload.GetProperty("Identifier").GetString());
        Assert.Equal("FieldsDoctor", payload.GetProperty("NameFamily").GetString());
        Assert.Equal("John", payload.GetProperty("NameGiven").GetString());
        Assert.Equal("Neurology", payload.GetProperty("Specialty").GetString());
    }

    /// <summary>
    /// Multiple resource types are tracked in sync log (Practitioner and Appointment).
    /// </summary>
    [Fact]
    public async Task MultipleResourceTypes_TrackedInSchedulingSyncLog()
    {
        var practitionerRequest = new
        {
            Identifier = $"NPI-MULTI-{Guid.NewGuid():N}",
            NameFamily = "MultiDoc",
            NameGiven = "Test",
        };
        var practitionerResponse = await _schedulingClient.PostAsJsonAsync(
            "/Practitioner",
            practitionerRequest
        );
        var practitioner = await practitionerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var practitionerId = practitioner.GetProperty("Id").GetString();

        var appointmentRequest = new
        {
            ServiceCategory = "Test",
            ServiceType = "Multi Test",
            Priority = "routine",
            Start = "2025-09-01T10:00:00Z",
            End = "2025-09-01T10:30:00Z",
            PatientReference = "Patient/multi-patient",
            PractitionerReference = $"Practitioner/{practitionerId}",
        };
        await _schedulingClient.PostAsJsonAsync("/Appointment", appointmentRequest);

        var response = await _schedulingClient.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);

        var tableNames = changes
            .Select(c => c.GetProperty("TableName").GetString())
            .Distinct()
            .ToList();
        Assert.Contains("fhir_Practitioner", tableNames);
        Assert.Contains("fhir_Appointment", tableNames);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _schedulingClient.Dispose();
        _schedulingFactory.Dispose();

        try
        {
            if (File.Exists(_schedulingDbPath))
            {
                File.Delete(_schedulingDbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
