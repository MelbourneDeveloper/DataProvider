using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Scheduling.Api.Tests;

/// <summary>
/// Authorization tests for Scheduling.Api endpoints.
/// Tests that endpoints require proper authentication and permissions.
/// </summary>
public sealed class AuthorizationTests : IClassFixture<SchedulingApiFactory>
{
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationTests"/> class.
    /// </summary>
    /// <param name="factory">Shared factory instance.</param>
    public AuthorizationTests(SchedulingApiFactory factory) => _client = factory.CreateClient();

    // === PRACTITIONER ENDPOINTS ===

    [Fact]
    public async Task GetPractitioners_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/Practitioner");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPractitioners_WithInvalidToken_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Practitioner");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPractitioners_WithExpiredToken_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Practitioner");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokenHelper.GenerateExpiredToken()
        );

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPractitionerById_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/Practitioner/test-id");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatePractitioner_WithoutToken_ReturnsUnauthorized()
    {
        var practitioner = new
        {
            Identifier = "PRACT-001",
            NameFamily = "Smith",
            NameGiven = "John",
            Specialty = "General Practice",
        };

        var response = await _client.PostAsJsonAsync("/Practitioner", practitioner);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePractitioner_WithoutToken_ReturnsUnauthorized()
    {
        var practitioner = new
        {
            Identifier = "PRACT-001",
            NameFamily = "Smith",
            NameGiven = "John",
            Active = true,
        };

        var response = await _client.PutAsJsonAsync("/Practitioner/test-id", practitioner);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SearchPractitioners_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/Practitioner/_search?specialty=Cardiology");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // === APPOINTMENT ENDPOINTS ===

    [Fact]
    public async Task GetAppointments_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/Appointment");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAppointmentById_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/Appointment/test-id");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAppointment_WithoutToken_ReturnsUnauthorized()
    {
        // Note: Must match CreateAppointmentRequest record structure exactly
        // JSON deserialization happens before endpoint filters in Minimal APIs
        var appointment = new
        {
            ServiceCategory = "general",
            ServiceType = "checkup",
            ReasonCode = "routine",
            Priority = "routine",
            Description = "Test appointment",
            Start = "2024-01-15T10:00:00Z",
            End = "2024-01-15T11:00:00Z",
            PatientReference = "Patient/test-patient",
            PractitionerReference = "Practitioner/test-practitioner",
            Comment = "test",
        };

        var response = await _client.PostAsJsonAsync("/Appointment", appointment);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAppointment_WithoutToken_ReturnsUnauthorized()
    {
        // Note: Must match UpdateAppointmentRequest record structure exactly
        var appointment = new
        {
            ServiceCategory = "general",
            ServiceType = "checkup",
            ReasonCode = "routine",
            Priority = "routine",
            Description = "Test appointment",
            Start = "2024-01-15T10:00:00Z",
            End = "2024-01-15T11:00:00Z",
            PatientReference = "Patient/test-patient",
            PractitionerReference = "Practitioner/test-practitioner",
            Comment = "test",
            Status = "booked",
        };

        var response = await _client.PutAsJsonAsync("/Appointment/test-id", appointment);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchAppointmentStatus_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.PatchAsync(
            "/Appointment/test-id/status?status=cancelled",
            null
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPatientAppointments_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/Patient/test-patient/Appointment");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPractitionerAppointments_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/Practitioner/test-practitioner/Appointment");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // === SYNC ENDPOINTS ===

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

    // === TOKEN VALIDATION TESTS ===

    [Fact]
    public async Task GetAppointments_WithValidToken_SucceedsInDevMode()
    {
        // In dev mode (default signing key is all zeros), Gatekeeper permission checks
        // are bypassed to allow E2E testing without requiring Gatekeeper setup.
        // Valid tokens pass through after local JWT validation.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Appointment");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokenHelper.GenerateNoRoleToken()
        );

        var response = await _client.SendAsync(request);

        // In dev mode, valid tokens succeed without permission checks
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
