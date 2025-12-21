namespace Gatekeeper.Api.Tests;

/// <summary>
/// Integration tests for Gatekeeper authentication endpoints.
/// Tests WebAuthn/FIDO2 passkey registration and login flows.
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
    public async Task RegisterBegin_RequiresResidentKey_ForDiscoverableCredentials()
    {
        // Registration must require resident keys so login works without email
        var request = new { Email = "resident@example.com", DisplayName = "Resident User" };

        var response = await _client.PostAsJsonAsync("/auth/register/begin", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var optionsJson = doc.RootElement.GetProperty("OptionsJson").GetString()!;
        var options = JsonDocument.Parse(optionsJson);

        // Verify authenticatorSelection requires resident key
        Assert.True(
            options.RootElement.TryGetProperty("authenticatorSelection", out var authSelection)
        );
        Assert.True(authSelection.TryGetProperty("residentKey", out var residentKey));
        Assert.Equal("required", residentKey.GetString());
    }

    [Fact]
    public async Task RegisterBegin_RequiresUserVerification()
    {
        // Registration must require user verification for security
        var request = new { Email = "verify@example.com", DisplayName = "Verify User" };

        var response = await _client.PostAsJsonAsync("/auth/register/begin", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var optionsJson = doc.RootElement.GetProperty("OptionsJson").GetString()!;
        var options = JsonDocument.Parse(optionsJson);

        var authSelection = options.RootElement.GetProperty("authenticatorSelection");
        Assert.True(authSelection.TryGetProperty("userVerification", out var userVerification));
        Assert.Equal("required", userVerification.GetString());
    }

    [Fact]
    public async Task LoginBegin_WithEmptyBody_ReturnsChallenge_ForDiscoverableCredentials()
    {
        // Discoverable credentials flow: no email needed, browser shows all passkeys
        // Server returns challenge with empty allowCredentials
        var response = await _client.PostAsJsonAsync("/auth/login/begin", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        // Should return a valid challenge
        Assert.True(doc.RootElement.TryGetProperty("ChallengeId", out var challengeId));
        Assert.False(string.IsNullOrEmpty(challengeId.GetString()));

        // Verify options structure
        Assert.True(doc.RootElement.TryGetProperty("OptionsJson", out var optionsJson));
        var options = JsonDocument.Parse(optionsJson.GetString()!);
        Assert.True(options.RootElement.TryGetProperty("challenge", out _));

        // allowCredentials should be empty for discoverable credentials
        Assert.True(
            options.RootElement.TryGetProperty("allowCredentials", out var allowCredentials)
        );
        Assert.Equal(JsonValueKind.Array, allowCredentials.ValueKind);
        Assert.Equal(0, allowCredentials.GetArrayLength());
    }

    [Fact]
    public async Task LoginBegin_RequiresUserVerification()
    {
        // Login must require user verification (Touch ID, Face ID, etc.)
        var response = await _client.PostAsJsonAsync("/auth/login/begin", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var optionsJson = doc.RootElement.GetProperty("OptionsJson").GetString()!;
        var options = JsonDocument.Parse(optionsJson);

        Assert.True(
            options.RootElement.TryGetProperty("userVerification", out var userVerification)
        );
        Assert.Equal("required", userVerification.GetString());
    }

    [Fact]
    public async Task LoginComplete_WithInvalidChallengeId_ReturnsBadRequest()
    {
        // Attempting to complete login with invalid challenge should fail
        var request = new
        {
            ChallengeId = "non-existent-challenge-id",
            OptionsJson = "{}",
            AssertionResponse = new
            {
                Id = "fake-credential-id",
                RawId = "fake-credential-id",
                Type = "public-key",
                Response = new
                {
                    AuthenticatorData = "AAAA",
                    ClientDataJson = "AAAA",
                    Signature = "AAAA",
                    UserHandle = (string?)null,
                },
            },
        };

        var response = await _client.PostAsJsonAsync("/auth/login/complete", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Challenge not found", content);
    }

    [Fact]
    public async Task RegisterComplete_WithInvalidChallengeId_ReturnsBadRequest()
    {
        // Attempting to complete registration with invalid challenge should fail
        var request = new
        {
            ChallengeId = "non-existent-challenge-id",
            OptionsJson = "{}",
            AttestationResponse = new
            {
                Id = "fake-credential-id",
                RawId = "fake-credential-id",
                Type = "public-key",
                Response = new { AttestationObject = "AAAA", ClientDataJson = "AAAA" },
            },
        };

        var response = await _client.PostAsJsonAsync("/auth/register/complete", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Challenge not found", content);
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

/// <summary>
/// Tests for Base64Url encoding used in WebAuthn credential IDs.
/// </summary>
public sealed class Base64UrlTests
{
    [Fact]
    public void Encode_ProducesUrlSafeOutput()
    {
        // Standard base64 uses + and /, base64url uses - and _
        var input = new byte[] { 0xfb, 0xff, 0xfe }; // Would produce +//+ in standard base64

        var result = Base64Url.Encode(input);

        Assert.DoesNotContain("+", result);
        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain("=", result);
        Assert.Contains("-", result); // Should use - instead of +
        Assert.Contains("_", result); // Should use _ instead of /
    }

    [Fact]
    public void Encode_Decode_RoundTrip()
    {
        var original = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var encoded = Base64Url.Encode(original);
        var decoded = Base64Url.Decode(encoded);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Decode_HandlesNoPadding()
    {
        // base64url typically omits padding
        var encoded = "AQIDBA"; // No = padding

        var decoded = Base64Url.Decode(encoded);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, decoded);
    }

    [Fact]
    public void Decode_HandlesUrlSafeCharacters()
    {
        // Test decoding with - and _ (url-safe chars)
        var encoded = "-_8"; // base64url for 0xfb, 0xff

        var decoded = Base64Url.Decode(encoded);

        Assert.Equal(new byte[] { 0xfb, 0xff }, decoded);
    }

    [Fact]
    public void Encode_MatchesWebAuthnCredentialIdFormat()
    {
        // WebAuthn credential IDs use base64url encoding
        // This test verifies our encoding matches the expected format
        var credentialId = new byte[]
        {
            0x01,
            0x02,
            0x03,
            0x04,
            0x05,
            0x06,
            0x07,
            0x08,
            0x09,
            0x0a,
            0x0b,
            0x0c,
            0x0d,
            0x0e,
            0x0f,
            0x10,
        };

        var encoded = Base64Url.Encode(credentialId);

        // Should be AQIDBAUGBwgJCgsMDQ4PEA (no padding)
        Assert.Equal("AQIDBAUGBwgJCgsMDQ4PEA", encoded);

        // Verify round-trip
        var decoded = Base64Url.Decode(encoded);
        Assert.Equal(credentialId, decoded);
    }
}
