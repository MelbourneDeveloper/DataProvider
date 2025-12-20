namespace Scheduling.Api.Tests;

/// <summary>
/// Tests that verify the Dashboard can actually connect to Scheduling API.
/// These tests MUST FAIL if CORS is not configured for Dashboard origin.
/// </summary>
public sealed class DashboardIntegrationTests : IClassFixture<SchedulingApiFactory>
{
    private readonly HttpClient _client;

    /// <summary>
    /// The actual URL where Dashboard runs (for CORS origin testing).
    /// </summary>
    private const string DashboardOrigin = "http://localhost:5173";

    public DashboardIntegrationTests(SchedulingApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region CORS Tests

    [Fact]
    public async Task SchedulingApi_Returns_CorsHeaders_ForDashboardOrigin()
    {
        // The Dashboard runs on localhost:5173 and makes fetch() calls to Scheduling API.
        // Browser enforces CORS - without proper headers, the request is blocked.
        //
        // This test verifies Scheduling API returns Access-Control-Allow-Origin header
        // for the Dashboard's origin.

        var request = new HttpRequestMessage(HttpMethod.Get, "/Practitioner");
        request.Headers.Add("Origin", DashboardOrigin);

        var response = await _client.SendAsync(request);

        // API should return CORS header allowing Dashboard origin
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Scheduling API must return Access-Control-Allow-Origin header for Dashboard to work"
        );

        var allowedOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
        Assert.True(
            allowedOrigin == DashboardOrigin || allowedOrigin == "*",
            $"Access-Control-Allow-Origin must be '{DashboardOrigin}' or '*', but was '{allowedOrigin}'"
        );
    }

    [Fact]
    public async Task SchedulingApi_Handles_PreflightRequest_ForDashboardOrigin()
    {
        // Before making actual requests, browsers send OPTIONS preflight request.
        // API must respond with correct CORS headers.

        var request = new HttpRequestMessage(HttpMethod.Options, "/Practitioner");
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
