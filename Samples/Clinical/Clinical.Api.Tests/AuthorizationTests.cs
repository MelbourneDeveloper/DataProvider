using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Clinical.Api.Tests;
/// <summary>
/// Authorization tests for Clinical.Api endpoints.
/// Tests that endpoints require proper authentication and permissions.
/// </summary>
public sealed class AuthorizationTests : IClassFixture<ClinicalApiFactory>
{
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationTests"/> class.
    /// </summary>
    /// <param name="factory">Shared factory instance.</param>
    public AuthorizationTests(ClinicalApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task GetPatients_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/fhir/Patient/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPatients_WithInvalidToken_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/Patient/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPatients_WithExpiredToken_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/Patient/");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokenHelper.GenerateExpiredToken()
        );

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPatients_WithValidToken_SucceedsInDevMode()
    {
        // In dev mode (default signing key is all zeros), Gatekeeper permission checks
        // are bypassed to allow E2E testing without requiring Gatekeeper setup.
        // Valid tokens pass through after local JWT validation.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/Patient/");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokenHelper.GenerateNoRoleToken()
        );

        var response = await _client.SendAsync(request);

        // In dev mode, valid tokens succeed without permission checks
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreatePatient_WithoutToken_ReturnsUnauthorized()
    {
        var patient = new
        {
            Active = true,
            GivenName = "Test",
            FamilyName = "Patient",
            Gender = "male",
        };

        var response = await _client.PostAsJsonAsync("/fhir/Patient/", patient);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEncounters_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/fhir/Patient/test-patient/Encounter/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetConditions_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/fhir/Patient/test-patient/Condition/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMedicationRequests_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/fhir/Patient/test-patient/MedicationRequest/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SyncChanges_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/sync/changes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SyncOrigin_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/sync/origin");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SyncStatus_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/sync/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SyncRecords_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/sync/records");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SyncRetry_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync("/sync/records/test-id/retry", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatientSearch_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/fhir/Patient/_search?q=test");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPatientById_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/fhir/Patient/test-patient-id");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePatient_WithoutToken_ReturnsUnauthorized()
    {
        var patient = new
        {
            Active = true,
            GivenName = "Updated",
            FamilyName = "Patient",
            Gender = "male",
        };

        var response = await _client.PutAsJsonAsync("/fhir/Patient/test-id", patient);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEncounter_WithoutToken_ReturnsUnauthorized()
    {
        var encounter = new
        {
            Status = "planned",
            Class = "outpatient",
            PractitionerId = "pract-1",
            ServiceType = "General",
            ReasonCode = "Checkup",
            PeriodStart = "2024-01-01T10:00:00Z",
            PeriodEnd = "2024-01-01T11:00:00Z",
            Notes = "Test",
        };

        var response = await _client.PostAsJsonAsync(
            "/fhir/Patient/test-patient/Encounter/",
            encounter
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateCondition_WithoutToken_ReturnsUnauthorized()
    {
        var condition = new
        {
            ClinicalStatus = "active",
            VerificationStatus = "confirmed",
            Category = "encounter-diagnosis",
            Severity = "moderate",
            CodeSystem = "http://snomed.info/sct",
            CodeValue = "123456",
            CodeDisplay = "Test Condition",
        };

        var response = await _client.PostAsJsonAsync(
            "/fhir/Patient/test-patient/Condition/",
            condition
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMedicationRequest_WithoutToken_ReturnsUnauthorized()
    {
        var medication = new
        {
            Status = "active",
            Intent = "order",
            PractitionerId = "pract-1",
            EncounterId = "enc-1",
            MedicationCode = "12345",
            MedicationDisplay = "Test Medication",
            DosageInstruction = "Take once daily",
            Quantity = 30,
            Unit = "tablets",
            Refills = 2,
        };

        var response = await _client.PostAsJsonAsync(
            "/fhir/Patient/test-patient/MedicationRequest/",
            medication
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
