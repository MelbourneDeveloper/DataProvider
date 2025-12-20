namespace Dashboard.Integration.Tests;

/// <summary>
/// Tests that verify the Dashboard frontend can communicate with backend APIs.
/// These tests simulate browser requests with CORS headers to ensure the APIs
/// are properly configured for cross-origin requests from the Dashboard.
/// </summary>
public sealed class DashboardApiCorsTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Clinical.Api.Program> _clinicalFactory;
    private readonly WebApplicationFactory<Scheduling.Api.Program> _schedulingFactory;
    private HttpClient _clinicalClient = null!;
    private HttpClient _schedulingClient = null!;

    // Dashboard origin - this is where the frontend runs
    private const string DashboardOrigin = "http://localhost:5173";

    public DashboardApiCorsTests()
    {
        _clinicalFactory = new WebApplicationFactory<Clinical.Api.Program>();
        _schedulingFactory = new WebApplicationFactory<Scheduling.Api.Program>();
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

        var allowedOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
        Assert.True(
            allowedOrigin == DashboardOrigin || allowedOrigin == "*",
            $"Clinical API must allow Dashboard origin. Got: {allowedOrigin}"
        );
    }

    /// <summary>
    /// CRITICAL: Dashboard must be able to GET /fhir/Patient with CORS headers.
    /// </summary>
    [Fact]
    public async Task ClinicalApi_GetPatients_ReturnsDataWithCorsHeaders()
    {
        // Arrange - simulate browser request with Origin header
        var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/Patient");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Headers.Add("Accept", "application/json");

        // Act
        var response = await _clinicalClient.SendAsync(request);

        // Assert - must succeed AND have CORS header
        Assert.True(
            response.IsSuccessStatusCode,
            $"Clinical API GET /fhir/Patient failed with {response.StatusCode}"
        );

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Clinical API response must include Access-Control-Allow-Origin header"
        );
    }

    /// <summary>
    /// Dashboard fetches encounters - must work with CORS.
    /// </summary>
    [Fact]
    public async Task ClinicalApi_GetEncounters_ReturnsDataWithCorsHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/Encounter");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Headers.Add("Accept", "application/json");

        // Act
        var response = await _clinicalClient.SendAsync(request);

        // Assert
        Assert.True(
            response.IsSuccessStatusCode,
            $"Clinical API GET /fhir/Encounter failed with {response.StatusCode}"
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
    /// </summary>
    [Fact]
    public async Task SchedulingApi_AppointmentsEndpoint_AllowsCorsFromDashboard()
    {
        // Arrange - simulate browser preflight request
        var request = new HttpRequestMessage(HttpMethod.Options, "/fhir/Appointment");
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

        var allowedOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
        Assert.True(
            allowedOrigin == DashboardOrigin || allowedOrigin == "*",
            $"Scheduling API must allow Dashboard origin. Got: {allowedOrigin}"
        );
    }

    /// <summary>
    /// CRITICAL: Dashboard must be able to GET /fhir/Appointment with CORS headers.
    /// </summary>
    [Fact]
    public async Task SchedulingApi_GetAppointments_ReturnsDataWithCorsHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/Appointment");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Headers.Add("Accept", "application/json");

        // Act
        var response = await _schedulingClient.SendAsync(request);

        // Assert
        Assert.True(
            response.IsSuccessStatusCode,
            $"Scheduling API GET /fhir/Appointment failed with {response.StatusCode}"
        );

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Scheduling API response must include Access-Control-Allow-Origin header"
        );
    }

    /// <summary>
    /// Dashboard fetches practitioners - must work with CORS.
    /// </summary>
    [Fact]
    public async Task SchedulingApi_GetPractitioners_ReturnsDataWithCorsHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/Practitioner");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Headers.Add("Accept", "application/json");

        // Act
        var response = await _schedulingClient.SendAsync(request);

        // Assert
        Assert.True(
            response.IsSuccessStatusCode,
            $"Scheduling API GET /fhir/Practitioner failed with {response.StatusCode}"
        );

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Scheduling API response must include Access-Control-Allow-Origin header"
        );
    }

    #endregion
}
