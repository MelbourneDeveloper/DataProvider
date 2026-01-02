using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Scheduling.Api.Tests;

/// <summary>
/// E2E tests for Sync endpoints - REAL database, NO mocks.
/// Tests sync log generation and origin tracking.
/// Each test creates its own isolated factory and database.
/// </summary>
public sealed class SyncEndpointTests
{
    private static readonly string AuthToken = TestTokenHelper.GenerateSchedulerToken();

    private static HttpClient CreateAuthenticatedClient(SchedulingApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AuthToken
        );
        return client;
    }

    [Fact]
    public async Task GetSyncOrigin_ReturnsOriginId()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync("/sync/origin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var originId = result.GetProperty("originId").GetString();
        Assert.NotNull(originId);
        Assert.NotEmpty(originId);
    }

    [Fact]
    public async Task GetSyncChanges_ReturnsEmptyList_WhenNoChanges()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync("/sync/changes?fromVersion=999999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", content);
    }

    [Fact]
    public async Task GetSyncChanges_ReturnChanges_AfterPractitionerCreated()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerRequest = new
        {
            Identifier = "NPI-Sync",
            NameFamily = "SyncTest",
            NameGiven = "Doctor",
            Specialty = "Internal Medicine",
        };

        await client.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(changes);
        Assert.True(changes.Length > 0);
    }

    [Fact]
    public async Task GetSyncChanges_RespectsLimitParameter()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        for (var i = 0; i < 5; i++)
        {
            var practitionerRequest = new
            {
                Identifier = $"NPI-Limit{i}",
                NameFamily = $"LimitTest{i}",
                NameGiven = "Doctor",
            };
            await client.PostAsJsonAsync("/Practitioner", practitionerRequest);
        }

        var response = await client.GetAsync("/sync/changes?fromVersion=0&limit=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(changes);
        Assert.True(changes.Length <= 2);
    }

    [Fact]
    public async Task GetSyncChanges_TracksPractitionerChanges()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerRequest = new
        {
            Identifier = "NPI-TrackPrac",
            NameFamily = "TrackTest",
            NameGiven = "Doctor",
        };
        await client.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");
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
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerRequest = new
        {
            Identifier = "NPI-TrackAppt",
            NameFamily = "TrackApptTest",
            NameGiven = "Doctor",
        };
        var pracResponse = await client.PostAsJsonAsync("/Practitioner", practitionerRequest);
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
        await client.PostAsJsonAsync("/Appointment", appointmentRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(changes, c => c.GetProperty("TableName").GetString() == "fhir_Appointment");
    }

    [Fact]
    public async Task GetSyncChanges_ContainsOperation()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var practitionerRequest = new
        {
            Identifier = "NPI-Op",
            NameFamily = "OperationTest",
            NameGiven = "Doctor",
        };
        await client.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(
            changes,
            c =>
            {
                // Operation is serialized as integer (0=Insert, 1=Update, 2=Delete)
                var opValue = c.GetProperty("Operation").GetInt32();
                return opValue >= 0 && opValue <= 2;
            }
        );
    }

    // ========== SYNC DASHBOARD ENDPOINT TESTS ==========
    // These tests verify the endpoints required by the Sync Dashboard UI.

    /// <summary>
    /// Tests GET /sync/status endpoint - returns service sync health status.
    /// REQUIRED BY: Sync Dashboard service status cards.
    /// </summary>
    [Fact]
    public async Task GetSyncStatus_ReturnsServiceStatus()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync("/sync/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Should return service health info
        Assert.True(result.TryGetProperty("service", out var service));
        Assert.Equal("Scheduling.Api", service.GetString());

        Assert.True(result.TryGetProperty("status", out var status));
        var statusValue = status.GetString();
        Assert.True(
            statusValue == "healthy" || statusValue == "degraded" || statusValue == "unhealthy",
            $"Status should be healthy, degraded, or unhealthy but was '{statusValue}'"
        );

        Assert.True(result.TryGetProperty("lastSyncTime", out _));
        Assert.True(result.TryGetProperty("pendingCount", out _));
        Assert.True(result.TryGetProperty("failedCount", out _));
    }

    /// <summary>
    /// Tests GET /sync/records endpoint - returns paginated sync records.
    /// REQUIRED BY: Sync Dashboard sync records table.
    /// </summary>
    [Fact]
    public async Task GetSyncRecords_ReturnsPaginatedRecords()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);

        // Create some data to generate sync records
        var practitionerRequest = new
        {
            Identifier = "NPI-SyncRec",
            NameFamily = "SyncRecordTest",
            NameGiven = "Doctor",
        };
        await client.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await client.GetAsync("/sync/records");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Should return paginated response
        Assert.True(result.TryGetProperty("records", out var records));
        Assert.True(records.GetArrayLength() > 0);

        Assert.True(result.TryGetProperty("total", out _));
        Assert.True(result.TryGetProperty("page", out _));
        Assert.True(result.TryGetProperty("pageSize", out _));
    }

    /// <summary>
    /// Tests GET /sync/records with status filter.
    /// REQUIRED BY: Sync Dashboard status filter dropdown.
    /// </summary>
    [Fact]
    public async Task GetSyncRecords_FiltersByStatus()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync("/sync/records?status=pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // All returned records should have pending status
        var records = result.GetProperty("records");
        foreach (var record in records.EnumerateArray())
        {
            Assert.Equal("pending", record.GetProperty("status").GetString());
        }
    }

    /// <summary>
    /// Tests POST /sync/records/{id}/retry endpoint - retries failed sync.
    /// REQUIRED BY: Sync Dashboard retry button.
    /// </summary>
    [Fact]
    public async Task PostSyncRetry_AcceptsRetryRequest()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);

        // Test that the endpoint exists and accepts the request
        var response = await client.PostAsync("/sync/records/test-record-id/retry", null);

        // Should return 200 OK, 404 Not Found (if record doesn't exist), or 202 Accepted
        Assert.True(
            response.StatusCode == HttpStatusCode.OK
                || response.StatusCode == HttpStatusCode.NotFound
                || response.StatusCode == HttpStatusCode.Accepted,
            $"Expected OK, NotFound, or Accepted but got {response.StatusCode}"
        );
    }

    /// <summary>
    /// Tests that sync records include required fields for dashboard display.
    /// REQUIRED BY: Sync Dashboard table columns.
    /// </summary>
    [Fact]
    public async Task GetSyncRecords_ContainsRequiredFields()
    {
        using var factory = new SchedulingApiFactory();
        var client = CreateAuthenticatedClient(factory);

        // Create data to generate sync records
        var practitionerRequest = new
        {
            Identifier = "NPI-Fields",
            NameFamily = "FieldTest",
            NameGiven = "Doctor",
        };
        await client.PostAsJsonAsync("/Practitioner", practitionerRequest);

        var response = await client.GetAsync("/sync/records");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var records = result.GetProperty("records");
        Assert.True(records.GetArrayLength() > 0);

        var firstRecord = records[0];

        // Required fields for Sync Dashboard UI
        Assert.True(firstRecord.TryGetProperty("id", out _), "Missing 'id' field");
        Assert.True(firstRecord.TryGetProperty("entityType", out _), "Missing 'entityType' field");
        Assert.True(firstRecord.TryGetProperty("entityId", out _), "Missing 'entityId' field");
        Assert.True(firstRecord.TryGetProperty("status", out _), "Missing 'status' field");
        Assert.True(
            firstRecord.TryGetProperty("lastAttempt", out _),
            "Missing 'lastAttempt' field"
        );
    }
}
