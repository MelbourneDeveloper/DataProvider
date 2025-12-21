using Microsoft.AspNetCore.Hosting;

namespace Dashboard.Integration.Tests;

/// <summary>
/// WebApplicationFactory for Clinical.Api that configures a temp database.
/// </summary>
public sealed class ClinicalApiTestFactory : WebApplicationFactory<Clinical.Api.Program>
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"clinical_cors_test_{Guid.NewGuid()}.db"
    );

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set DbPath for the temp database
        builder.UseSetting("DbPath", _dbPath);

        var clinicalApiAssembly = typeof(Clinical.Api.Program).Assembly;
        var contentRoot = Path.GetDirectoryName(clinicalApiAssembly.Location)!;
        builder.UseContentRoot(contentRoot);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            { /* ignore */
            }
        }
    }
}

/// <summary>
/// WebApplicationFactory for Scheduling.Api that configures a temp database.
/// </summary>
public sealed class SchedulingApiTestFactory : WebApplicationFactory<Scheduling.Api.Program>
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"scheduling_cors_test_{Guid.NewGuid()}.db"
    );

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set DbPath for the temp database
        builder.UseSetting("DbPath", _dbPath);

        var schedulingApiAssembly = typeof(Scheduling.Api.Program).Assembly;
        var contentRoot = Path.GetDirectoryName(schedulingApiAssembly.Location)!;
        builder.UseContentRoot(contentRoot);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            { /* ignore */
            }
        }
    }
}

/// <summary>
/// Tests that verify the Dashboard frontend can communicate with backend APIs.
/// These tests simulate browser requests with CORS headers to ensure the APIs
/// are properly configured for cross-origin requests from the Dashboard.
/// </summary>
public sealed class DashboardApiCorsTests : IAsyncLifetime
{
    private readonly ClinicalApiTestFactory _clinicalFactory;
    private readonly SchedulingApiTestFactory _schedulingFactory;
    private HttpClient _clinicalClient = null!;
    private HttpClient _schedulingClient = null!;

    // Dashboard origin - this is where the frontend runs
    private const string DashboardOrigin = "http://localhost:5173";

    public DashboardApiCorsTests()
    {
        _clinicalFactory = new ClinicalApiTestFactory();
        _schedulingFactory = new SchedulingApiTestFactory();
    }

    public Task InitializeAsync()
    {
        _clinicalClient = _clinicalFactory.CreateClient();
        _schedulingClient = _schedulingFactory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _clinicalClient.Dispose();
        _schedulingClient.Dispose();
        await _clinicalFactory.DisposeAsync();
        await _schedulingFactory.DisposeAsync();
    }

    #region Clinical API CORS Tests

    /// <summary>
    /// CRITICAL: Dashboard at localhost:5173 must be able to fetch patients from Clinical API.
    /// This test verifies CORS is configured to allow the Dashboard origin.
    /// </summary>
    [Fact]
    public async Task ClinicalApi_PatientsEndpoint_AllowsCorsFromDashboard()
    {
        // Arrange - simulate browser preflight request
        var request = new HttpRequestMessage(HttpMethod.Options, "/fhir/Patient");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Accept");

        // Act
        var response = await _clinicalClient.SendAsync(request);

        // Assert - CORS headers must be present
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Clinical API must return Access-Control-Allow-Origin header for Dashboard origin"
        );

        var allowedOrigin = response
            .Headers.GetValues("Access-Control-Allow-Origin")
            .FirstOrDefault();
        Assert.True(
            allowedOrigin == DashboardOrigin || allowedOrigin == "*",
            $"Clinical API must allow Dashboard origin. Got: {allowedOrigin}"
        );
    }

    /// <summary>
    /// CRITICAL: Dashboard must be able to GET /fhir/Patient/ with CORS headers.
    /// Note: Trailing slash is required for the Patient list endpoint.
    /// </summary>
    [Fact]
    public async Task ClinicalApi_GetPatients_ReturnsDataWithCorsHeaders()
    {
        // Arrange - simulate browser request with Origin header
        // Note: Clinical API uses /fhir/Patient/ (with trailing slash) for list
        var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/Patient/");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Headers.Add("Accept", "application/json");

        // Act
        var response = await _clinicalClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert - must succeed AND have CORS header
        Assert.True(
            response.IsSuccessStatusCode,
            $"Clinical API GET /fhir/Patient/ failed with {response.StatusCode}. Body: {body}"
        );

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Clinical API response must include Access-Control-Allow-Origin header"
        );
    }

    /// <summary>
    /// Dashboard fetches encounters for a patient - must work with CORS.
    /// Note: Encounters are nested under Patient: /fhir/Patient/{patientId}/Encounter
    /// </summary>
    [Fact]
    public async Task ClinicalApi_GetEncounters_ReturnsDataWithCorsHeaders()
    {
        // Arrange - First create a patient to get encounters for
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/fhir/Patient/");
        createRequest.Headers.Add("Origin", DashboardOrigin);
        createRequest.Content = new StringContent(
            """{"Active": true, "GivenName": "Test", "FamilyName": "Patient", "Gender": "other"}""",
            System.Text.Encoding.UTF8,
            "application/json"
        );
        var createResponse = await _clinicalClient.SendAsync(createRequest);
        var patientJson = await createResponse.Content.ReadAsStringAsync();
        var patientId = System
            .Text.Json.JsonDocument.Parse(patientJson)
            .RootElement.GetProperty("Id")
            .GetString();

        // Now test the encounters endpoint with CORS
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/fhir/Patient/{patientId}/Encounter"
        );
        request.Headers.Add("Origin", DashboardOrigin);
        request.Headers.Add("Accept", "application/json");

        // Act
        var response = await _clinicalClient.SendAsync(request);

        // Assert
        Assert.True(
            response.IsSuccessStatusCode,
            $"Clinical API GET /fhir/Patient/{{patientId}}/Encounter failed with {response.StatusCode}"
        );

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Clinical API response must include Access-Control-Allow-Origin header"
        );
    }

    #endregion

    #region Scheduling API CORS Tests

    /// <summary>
    /// CRITICAL: Dashboard must be able to fetch appointments from Scheduling API.
    /// Note: Scheduling API uses /Appointment (no /fhir/ prefix).
    /// </summary>
    [Fact]
    public async Task SchedulingApi_AppointmentsEndpoint_AllowsCorsFromDashboard()
    {
        // Arrange - simulate browser preflight request
        // Note: Scheduling API doesn't use /fhir/ prefix
        var request = new HttpRequestMessage(HttpMethod.Options, "/Appointment");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Accept");

        // Act
        var response = await _schedulingClient.SendAsync(request);

        // Assert - CORS headers must be present
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Scheduling API must return Access-Control-Allow-Origin header for Dashboard origin"
        );

        var allowedOrigin = response
            .Headers.GetValues("Access-Control-Allow-Origin")
            .FirstOrDefault();
        Assert.True(
            allowedOrigin == DashboardOrigin || allowedOrigin == "*",
            $"Scheduling API must allow Dashboard origin. Got: {allowedOrigin}"
        );
    }

    /// <summary>
    /// CRITICAL: Dashboard must be able to GET /Appointment with CORS headers.
    /// Note: Scheduling API uses /Appointment (no /fhir/ prefix).
    /// </summary>
    [Fact]
    public async Task SchedulingApi_GetAppointments_ReturnsDataWithCorsHeaders()
    {
        // Arrange - Scheduling API doesn't use /fhir/ prefix
        var request = new HttpRequestMessage(HttpMethod.Get, "/Appointment");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Headers.Add("Accept", "application/json");

        // Act
        var response = await _schedulingClient.SendAsync(request);

        // Assert
        Assert.True(
            response.IsSuccessStatusCode,
            $"Scheduling API GET /Appointment failed with {response.StatusCode}"
        );

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Scheduling API response must include Access-Control-Allow-Origin header"
        );
    }

    /// <summary>
    /// Dashboard fetches practitioners - must work with CORS.
    /// Note: Scheduling API uses /Practitioner (no /fhir/ prefix).
    /// </summary>
    [Fact]
    public async Task SchedulingApi_GetPractitioners_ReturnsDataWithCorsHeaders()
    {
        // Arrange - Scheduling API doesn't use /fhir/ prefix
        var request = new HttpRequestMessage(HttpMethod.Get, "/Practitioner");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Headers.Add("Accept", "application/json");

        // Act
        var response = await _schedulingClient.SendAsync(request);

        // Assert
        Assert.True(
            response.IsSuccessStatusCode,
            $"Scheduling API GET /Practitioner failed with {response.StatusCode}"
        );

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Scheduling API response must include Access-Control-Allow-Origin header"
        );
    }

    #endregion

    #region Patient Creation Tests

    /// <summary>
    /// CRITICAL: Proves patient creation API works end-to-end.
    /// This tests the actual POST endpoint that the AddPatientModal calls.
    /// </summary>
    [Fact]
    public async Task ClinicalApi_CreatePatient_WorksEndToEnd()
    {
        // Arrange - Create a patient with unique name
        var uniqueName = $"IntTest{DateTime.UtcNow.Ticks % 100000}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/fhir/Patient/");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Content = new StringContent(
            $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "IntegrationCreated", "Gender": "female"}""",
            System.Text.Encoding.UTF8,
            "application/json"
        );

        // Act - Create patient
        var createResponse = await _clinicalClient.SendAsync(request);
        createResponse.EnsureSuccessStatusCode();

        // Verify - Fetch all patients and confirm the new one is there
        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/fhir/Patient/");
        listRequest.Headers.Add("Origin", DashboardOrigin);
        var listResponse = await _clinicalClient.SendAsync(listRequest);
        var listBody = await listResponse.Content.ReadAsStringAsync();

        Assert.Contains(uniqueName, listBody);
        Assert.Contains("IntegrationCreated", listBody);
    }

    /// <summary>
    /// CRITICAL: Proves practitioner creation API works end-to-end.
    /// This tests the actual POST endpoint that the AddPractitionerModal would call.
    /// </summary>
    [Fact]
    public async Task SchedulingApi_CreatePractitioner_WorksEndToEnd()
    {
        // Arrange - Create a practitioner with unique identifier
        var uniqueId = $"DR{DateTime.UtcNow.Ticks % 100000}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/Practitioner");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Content = new StringContent(
            $$$"""{"Identifier": "{{{uniqueId}}}", "Active": true, "NameGiven": "IntDoctor", "NameFamily": "TestDoc", "Qualification": "MD", "Specialty": "Testing", "TelecomEmail": "inttest@hospital.org", "TelecomPhone": "+1-555-8888"}""",
            System.Text.Encoding.UTF8,
            "application/json"
        );

        // Act - Create practitioner
        var createResponse = await _schedulingClient.SendAsync(request);
        createResponse.EnsureSuccessStatusCode();

        // Verify - Fetch all practitioners and confirm the new one is there
        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/Practitioner");
        listRequest.Headers.Add("Origin", DashboardOrigin);
        var listResponse = await _schedulingClient.SendAsync(listRequest);
        var listBody = await listResponse.Content.ReadAsStringAsync();

        Assert.Contains(uniqueId, listBody);
        Assert.Contains("IntDoctor", listBody);
    }

    /// <summary>
    /// CRITICAL: Proves appointment creation API works end-to-end.
    /// This tests the actual POST endpoint that the AddAppointmentModal calls.
    /// </summary>
    [Fact]
    public async Task SchedulingApi_CreateAppointment_WorksEndToEnd()
    {
        // Arrange - Create an appointment with unique service type
        var uniqueService = $"Consult{DateTime.UtcNow.Ticks % 100000}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/Appointment");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Content = new StringContent(
            $$$"""{"ServiceCategory": "General", "ServiceType": "{{{uniqueService}}}", "Start": "2025-12-25T10:00:00Z", "End": "2025-12-25T11:00:00Z", "PatientReference": "Patient/test", "PractitionerReference": "Practitioner/test", "Priority": "routine"}""",
            System.Text.Encoding.UTF8,
            "application/json"
        );

        // Act - Create appointment
        var createResponse = await _schedulingClient.SendAsync(request);
        createResponse.EnsureSuccessStatusCode();

        // Verify - Fetch all appointments and confirm the new one is there
        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/Appointment");
        listRequest.Headers.Add("Origin", DashboardOrigin);
        var listResponse = await _schedulingClient.SendAsync(listRequest);
        var listBody = await listResponse.Content.ReadAsStringAsync();

        Assert.Contains(uniqueService, listBody);
    }

    #endregion
}
