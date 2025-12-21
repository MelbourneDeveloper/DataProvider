namespace Gatekeeper.Api.Tests;

/// <summary>
/// Integration tests for Gatekeeper authentication endpoints.
/// </summary>
public sealed class AuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthenticationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterBegin_WithValidEmail_ReturnsChallenge()
    {
        var request = new { Email = "test@example.com", DisplayName = "Test User" };

        var response = await _client.PostAsJsonAsync("/auth/register/begin", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.TryGetProperty("ChallengeId", out var challengeId));
        Assert.False(string.IsNullOrEmpty(challengeId.GetString()));

        // API returns OptionsJson as a JSON string (for JS to parse)
        Assert.True(doc.RootElement.TryGetProperty("OptionsJson", out var optionsJson));
        var parsedOptions = JsonDocument.Parse(optionsJson.GetString()!);
        Assert.True(parsedOptions.RootElement.TryGetProperty("challenge", out _));
    }

    [Fact]
    public async Task LoginBegin_WithNoCredentials_ReturnsBadRequest()
    {
        // Login without registered credentials should fail with helpful message
        var response = await _client.PostAsJsonAsync("/auth/login/begin", new { Email = "nobody@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("No passkey registered", content);
    }

    [Fact]
    public async Task Session_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/auth/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Session_WithInvalidToken_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await _client.GetAsync("/auth/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync("/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
