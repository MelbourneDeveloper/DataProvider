namespace Scheduling.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// E2E tests for Sync endpoints - REAL database, NO mocks.
/// Tests sync log generation and origin tracking.
/// </summary>
public sealed class SyncEndpointTests : IDisposable
{
    private readonly SchedulingApiFactory _factory;
    private readonly HttpClient _client;

    public SyncEndpointTests()
    {
        _factory = new SchedulingApiFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetSyncOrigin_ReturnsOriginId()
    {
        var response = await _client.GetAsync("/sync/origin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var originId = result.GetProperty("originId").GetString();
        Assert.NotNull(originId);
        Assert.NotEmpty(originId);
    }

    [Fact]
    public async Task GetSyncChanges_ReturnsEmptyList_WhenNoChanges()
    {
        var response = await _client.GetAsync("/sync/changes?fromVersion=999999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", content);
    }

    [Fact]
    public async Task GetSyncChanges_ReturnChanges_AfterPractitionerCreated()
    {
        var practitionerRequest = new
        {
            Identifier = "NPI-Sync",
            NameFamily = "SyncTest",
            NameGiven = "Doctor",
            Specialty = "Internal Medicine",
        };

        await _client.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await _client.GetAsync("/sync/changes?fromVersion=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(changes);
        Assert.True(changes.Length > 0);
    }

    [Fact]
    public async Task GetSyncChanges_RespectsLimitParameter()
    {
        for (var i = 0; i < 5; i++)
        {
            var practitionerRequest = new
            {
                Identifier = $"NPI-Limit{i}",
                NameFamily = $"LimitTest{i}",
                NameGiven = "Doctor",
            };
            await _client.PostAsJsonAsync("/Practitioner", practitionerRequest);
        }

        var response = await _client.GetAsync("/sync/changes?fromVersion=0&limit=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(changes);
        Assert.True(changes.Length <= 2);
    }

    [Fact]
    public async Task GetSyncChanges_TracksPractitionerChanges()
    {
        var practitionerRequest = new
        {
            Identifier = "NPI-TrackPrac",
            NameFamily = "TrackTest",
            NameGiven = "Doctor",
        };
        await _client.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await _client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(
            changes,
            c => c.GetProperty("TableName").GetString() == "fhir_Practitioner"
        );
    }

    [Fact]
    public async Task GetSyncChanges_TracksAppointmentChanges()
    {
        var practitionerRequest = new
        {
            Identifier = "NPI-TrackAppt",
            NameFamily = "TrackApptTest",
            NameGiven = "Doctor",
        };
        var pracResponse = await _client.PostAsJsonAsync("/Practitioner", practitionerRequest);
        var practitioner = await pracResponse.Content.ReadFromJsonAsync<JsonElement>();
        var practitionerId = practitioner.GetProperty("Id").GetString();

        var appointmentRequest = new
        {
            ServiceCategory = "Test",
            ServiceType = "Sync Track Test",
            Priority = "routine",
            Start = "2025-07-01T09:00:00Z",
            End = "2025-07-01T09:30:00Z",
            PatientReference = "Patient/patient-sync",
            PractitionerReference = $"Practitioner/{practitionerId}",
        };
        await _client.PostAsJsonAsync("/Appointment", appointmentRequest);

        var response = await _client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(changes, c => c.GetProperty("TableName").GetString() == "fhir_Appointment");
    }

    [Fact]
    public async Task GetSyncChanges_ContainsOperation()
    {
        var practitionerRequest = new
        {
            Identifier = "NPI-Op",
            NameFamily = "OperationTest",
            NameGiven = "Doctor",
        };
        await _client.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await _client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(
            changes,
            c =>
            {
                var op = c.GetProperty("Operation").GetString();
                return op == "INSERT" || op == "UPDATE" || op == "DELETE";
            }
        );
    }
}
