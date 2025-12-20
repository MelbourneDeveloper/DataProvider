namespace Gatekeeper.Api.Tests;

/// <summary>
/// Integration tests for Gatekeeper authorization endpoints.
/// </summary>
public sealed class AuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Check_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/authz/check?permission=patient:read");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Permissions_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/authz/permissions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_WithoutToken_ReturnsUnauthorized()
    {
        var request = new
        {
            Checks = new[]
            {
                new
                {
                    Permission = "patient:read",
                    ResourceType = (string?)null,
                    ResourceId = (string?)null,
                },
            },
        };

        var response = await _client.PostAsJsonAsync("/authz/evaluate", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
