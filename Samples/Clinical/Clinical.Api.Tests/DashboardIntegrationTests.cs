namespace Clinical.Api.Tests;

/// <summary>
/// Tests that verify the Dashboard can actually connect to Clinical API.
/// These tests MUST FAIL if:
/// 1. Dashboard hardcoded URL doesn't match actual API URL
/// 2. CORS is not configured for Dashboard origin
/// </summary>
public sealed class DashboardIntegrationTests : IClassFixture<ClinicalApiFactory>
{
    private readonly HttpClient _client;


    /// <summary>
    /// The actual URL where Dashboard runs (for CORS origin testing).
    /// </summary>
    private const string DashboardOrigin = "http://localhost:5173";

    public DashboardIntegrationTests(ClinicalApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region URL Configuration Tests

    [Fact]
    public void Dashboard_ClinicalApiUrl_MatchesActualPort()
    {
        // The Dashboard's index.html has this hardcoded:
        // const CLINICAL_API = window.dashboardConfig?.CLINICAL_API_URL || 'http://localhost:5000';
        //
        // But Clinical API runs on port 5080 (see start.sh and launchSettings.json)
        //
        // This test verifies the Dashboard is configured to hit the CORRECT port.
        // If this fails, the Dashboard cannot connect to the API.

        const string dashboardHardcodedUrl = "http://localhost:5000"; // What Dashboard actually uses
        const string clinicalApiActualUrl = "http://localhost:5080"; // Where API actually runs

        Assert.Equal(
            clinicalApiActualUrl,
            dashboardHardcodedUrl // THIS WILL FAIL - Dashboard uses wrong port!
        );
    }

    #endregion

    #region CORS Tests

    [Fact]
    public async Task ClinicalApi_Returns_CorsHeaders_ForDashboardOrigin()
    {
        // The Dashboard runs on localhost:5173 and makes fetch() calls to Clinical API.
        // Browser enforces CORS - without proper headers, the request is blocked.
        //
        // This test verifies Clinical API returns Access-Control-Allow-Origin header
        // for the Dashboard's origin.

        var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/Patient");
        request.Headers.Add("Origin", DashboardOrigin);

        var response = await _client.SendAsync(request);

        // API should return CORS header allowing Dashboard origin
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Clinical API must return Access-Control-Allow-Origin header for Dashboard to work"
        );

        var allowedOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
        Assert.True(
            allowedOrigin == DashboardOrigin || allowedOrigin == "*",
            $"Access-Control-Allow-Origin must be '{DashboardOrigin}' or '*', but was '{allowedOrigin}'"
        );
    }

    [Fact]
    public async Task ClinicalApi_Handles_PreflightRequest_ForDashboardOrigin()
    {
        // Before making actual requests, browsers send OPTIONS preflight request.
        // API must respond with correct CORS headers.

        var request = new HttpRequestMessage(HttpMethod.Options, "/fhir/Patient");
        request.Headers.Add("Origin", DashboardOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Accept");

        var response = await _client.SendAsync(request);

        // Preflight should succeed (200 or 204)
        Assert.True(
            response.IsSuccessStatusCode,
            $"Preflight OPTIONS request failed with {response.StatusCode}"
        );

        // Must have CORS headers
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Preflight response must include Access-Control-Allow-Origin"
        );

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Methods"),
            "Preflight response must include Access-Control-Allow-Methods"
        );
    }

    #endregion
}
