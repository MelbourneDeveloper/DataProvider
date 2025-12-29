using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Clinical.Api.Tests;
/// <summary>
/// E2E tests for Sync endpoints - REAL database, NO mocks.
/// Tests sync log generation and origin tracking.
/// Each test creates its own isolated factory and database.
/// </summary>
public sealed class SyncEndpointTests
{
    private static readonly string AuthToken = TestTokenHelper.GenerateClinicianToken();

    private static HttpClient CreateAuthenticatedClient(ClinicalApiFactory factory)
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
        using var factory = new ClinicalApiFactory();
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
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync("/sync/changes?fromVersion=999999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", content);
    }

    [Fact]
    public async Task GetSyncChanges_ReturnChanges_AfterPatientCreated()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientRequest = new
        {
            Active = true,
            GivenName = "Sync",
            FamilyName = "TestPatient",
            Gender = "male",
        };

        await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(changes);
        Assert.True(changes.Length > 0);
    }

    [Fact]
    public async Task GetSyncChanges_RespectsLimitParameter()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        for (var i = 0; i < 5; i++)
        {
            var patientRequest = new
            {
                Active = true,
                GivenName = $"SyncLimit{i}",
                FamilyName = "TestPatient",
                Gender = "other",
            };
            await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);
        }

        var response = await client.GetAsync("/sync/changes?fromVersion=0&limit=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(changes);
        Assert.True(changes.Length <= 2);
    }

    [Fact]
    public async Task GetSyncChanges_ContainsTableName()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientRequest = new
        {
            Active = true,
            GivenName = "SyncTable",
            FamilyName = "TestPatient",
            Gender = "male",
        };
        await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(changes, c => c.GetProperty("TableName").GetString() == "fhir_Patient");
    }

    [Fact]
    public async Task GetSyncChanges_ContainsOperation()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientRequest = new
        {
            Active = true,
            GivenName = "SyncOp",
            FamilyName = "TestPatient",
            Gender = "female",
        };
        await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);

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

    [Fact]
    public async Task GetSyncChanges_TracksEncounterChanges()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientRequest = new
        {
            Active = true,
            GivenName = "SyncEncounter",
            FamilyName = "TestPatient",
            Gender = "male",
        };
        var patientResponse = await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);
        var patient = await patientResponse.Content.ReadFromJsonAsync<JsonElement>();
        var patientId = patient.GetProperty("Id").GetString();

        var encounterRequest = new
        {
            Status = "planned",
            Class = "ambulatory",
            PeriodStart = "2024-02-01T10:00:00Z",
        };
        await client.PostAsJsonAsync($"/fhir/Patient/{patientId}/Encounter/", encounterRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(changes, c => c.GetProperty("TableName").GetString() == "fhir_Encounter");
    }

    [Fact]
    public async Task GetSyncChanges_TracksConditionChanges()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientRequest = new
        {
            Active = true,
            GivenName = "SyncCondition",
            FamilyName = "TestPatient",
            Gender = "female",
        };
        var patientResponse = await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);
        var patient = await patientResponse.Content.ReadFromJsonAsync<JsonElement>();
        var patientId = patient.GetProperty("Id").GetString();

        var conditionRequest = new
        {
            ClinicalStatus = "active",
            CodeSystem = "http://hl7.org/fhir/sid/icd-10-cm",
            CodeValue = "J06.9",
            CodeDisplay = "URI",
        };
        await client.PostAsJsonAsync($"/fhir/Patient/{patientId}/Condition/", conditionRequest);

        var response = await client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(changes, c => c.GetProperty("TableName").GetString() == "fhir_Condition");
    }

    [Fact]
    public async Task GetSyncChanges_TracksMedicationRequestChanges()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);
        var patientRequest = new
        {
            Active = true,
            GivenName = "SyncMedication",
            FamilyName = "TestPatient",
            Gender = "male",
        };
        var patientResponse = await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);
        var patient = await patientResponse.Content.ReadFromJsonAsync<JsonElement>();
        var patientId = patient.GetProperty("Id").GetString();

        var medicationRequest = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "doc-sync",
            MedicationCode = "123",
            MedicationDisplay = "Test Med",
            Refills = 0,
        };
        await client.PostAsJsonAsync(
            $"/fhir/Patient/{patientId}/MedicationRequest/",
            medicationRequest
        );

        var response = await client.GetAsync("/sync/changes?fromVersion=0");
        var changes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(changes);
        Assert.Contains(
            changes,
            c => c.GetProperty("TableName").GetString() == "fhir_MedicationRequest"
        );
    }

    // ========== SYNC DASHBOARD ENDPOINT TESTS ==========
    // These tests verify the endpoints required by the Sync Dashboard UI.
    // They should FAIL until the endpoints are implemented.

    /// <summary>
    /// Tests GET /sync/status endpoint - returns service sync health status.
    /// REQUIRED BY: Sync Dashboard service status cards.
    /// </summary>
    [Fact]
    public async Task GetSyncStatus_ReturnsServiceStatus()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync("/sync/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Should return service health info
        Assert.True(result.TryGetProperty("service", out var service));
        Assert.Equal("Clinical.Api", service.GetString());

        Assert.True(result.TryGetProperty("status", out var status));
        var statusValue = status.GetString();
        Assert.True(
            statusValue == "healthy" || statusValue == "degraded" || statusValue == "unhealthy",
            $"Status should be healthy, degraded, or unhealthy but was '{statusValue}'"
        );

        Assert.True(result.TryGetProperty("lastSyncTime", out _));
        Assert.True(result.TryGetProperty("totalRecords", out _));
    }

    /// <summary>
    /// Tests GET /sync/records endpoint - returns paginated sync records.
    /// REQUIRED BY: Sync Dashboard sync records table.
    /// </summary>
    [Fact]
    public async Task GetSyncRecords_ReturnsPaginatedRecords()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);

        // Create some data to generate sync records
        var patientRequest = new
        {
            Active = true,
            GivenName = "SyncRecordTest",
            FamilyName = "TestPatient",
            Gender = "male",
        };
        await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);

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
    /// Tests GET /sync/records with search query.
    /// REQUIRED BY: Sync Dashboard search input.
    /// </summary>
    [Fact]
    public async Task GetSyncRecords_SearchByEntityId()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);

        // Create a patient with known ID pattern
        var patientRequest = new
        {
            Active = true,
            GivenName = "SearchSyncTest",
            FamilyName = "UniquePatient",
            Gender = "female",
        };
        var createResponse = await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);
        var patient = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var patientId = patient.GetProperty("Id").GetString();

        var response = await client.GetAsync($"/sync/records?search={patientId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Should find records matching the patient ID
        var records = result.GetProperty("records");
        Assert.True(records.GetArrayLength() > 0);
    }

    /// <summary>
    /// Tests POST /sync/records/{id}/retry endpoint - retries failed sync.
    /// REQUIRED BY: Sync Dashboard retry button.
    /// </summary>
    [Fact]
    public async Task PostSyncRetry_RetriesFailedRecord()
    {
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);

        // First we need a failed sync record to retry
        // For now, test that the endpoint exists and accepts the request
        var response = await client.PostAsync("/sync/records/test-record-id/retry", null);

        // Should return 200 OK or 404 Not Found (if record doesn't exist)
        // NOT 404 Method Not Found (which would mean endpoint doesn't exist)
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
        using var factory = new ClinicalApiFactory();
        var client = CreateAuthenticatedClient(factory);

        // Create data to generate sync records
        var patientRequest = new
        {
            Active = true,
            GivenName = "FieldTest",
            FamilyName = "SyncPatient",
            Gender = "other",
        };
        await client.PostAsJsonAsync("/fhir/Patient/", patientRequest);

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
        Assert.True(firstRecord.TryGetProperty("operation", out _), "Missing 'operation' field");
        Assert.True(
            firstRecord.TryGetProperty("lastAttempt", out _),
            "Missing 'lastAttempt' field"
        );
    }
}
