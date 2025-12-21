namespace Gatekeeper.Api.Tests;

using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Gatekeeper.Api;

/// <summary>
/// Integration tests for Gatekeeper authorization endpoints.
/// Tests RBAC permission checks, resource grants, and bulk evaluation.
/// </summary>
public sealed class AuthorizationTests : IClassFixture<GatekeeperTestFixture>
{
    private readonly GatekeeperTestFixture _fixture;

    public AuthorizationTests(GatekeeperTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Check_WithoutToken_ReturnsUnauthorized()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/authz/check?permission=test:read");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Check_WithInvalidToken_ReturnsUnauthorized()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await client.GetAsync("/authz/check?permission=test:read");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Check_WithValidToken_UserHasDefaultPermissions_ReturnsAllowed()
    {
        var client = _fixture.CreateClient();
        var token = await _fixture.CreateTestUserAndGetToken("authz-user-1@example.com");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Default 'user' role has 'user:profile' permission
        var response = await client.GetAsync("/authz/check?permission=user:profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("Allowed").GetBoolean(), $"Response: {content}");
        Assert.Contains("user:profile", doc.RootElement.GetProperty("Reason").GetString());
    }

    [Fact]
    public async Task Check_WithValidToken_UserLacksPermission_ReturnsDenied()
    {
        var client = _fixture.CreateClient();
        var token = await _fixture.CreateTestUserAndGetToken("authz-user-2@example.com");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Default 'user' role does NOT have 'admin:users' permission
        var response = await client.GetAsync("/authz/check?permission=admin:users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        Assert.False(doc.RootElement.GetProperty("Allowed").GetBoolean());
        Assert.Equal("no matching permission", doc.RootElement.GetProperty("Reason").GetString());
    }

    [Fact]
    public async Task Check_AdminWildcardPermission_MatchesSubPermissions()
    {
        var client = _fixture.CreateClient();
        var token = await _fixture.CreateAdminUserAndGetToken("admin-wildcard@example.com");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Admin role has 'admin:*' which should match 'admin:users'
        var response = await client.GetAsync("/authz/check?permission=admin:users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("Allowed").GetBoolean());
        Assert.Contains("admin", doc.RootElement.GetProperty("Reason").GetString());
    }

    [Fact]
    public async Task Check_AdminWildcardPermission_MatchesNestedSubPermissions()
    {
        var client = _fixture.CreateClient();
        var token = await _fixture.CreateAdminUserAndGetToken("admin-nested@example.com");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Admin role has 'admin:*' which should match 'admin:users:create'
        var response = await client.GetAsync("/authz/check?permission=admin:users:create");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("Allowed").GetBoolean());
    }

    [Fact]
    public async Task Permissions_WithoutToken_ReturnsUnauthorized()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/authz/permissions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Permissions_WithValidToken_ReturnsUserPermissions()
    {
        var client = _fixture.CreateClient();
        var token = await _fixture.CreateTestUserAndGetToken("authz-perms@example.com");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/authz/permissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.TryGetProperty("Permissions", out var perms));
        Assert.Equal(JsonValueKind.Array, perms.ValueKind);

        // Default user role has 'user:profile' and 'user:credentials'
        var permCodes = perms
            .EnumerateArray()
            .Select(p => p.GetProperty("code").GetString())
            .ToList();
        Assert.Contains("user:profile", permCodes);
        Assert.Contains("user:credentials", permCodes);
    }

    [Fact]
    public async Task Permissions_AdminUser_ReturnsAdminPermissions()
    {
        var client = _fixture.CreateClient();
        var token = await _fixture.CreateAdminUserAndGetToken("admin-perms@example.com");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/authz/permissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        var perms = doc.RootElement.GetProperty("Permissions");
        var permCodes = perms
            .EnumerateArray()
            .Select(p => p.GetProperty("code").GetString())
            .ToList();
        Assert.Contains("admin:*", permCodes);
    }

    [Fact]
    public async Task Evaluate_WithoutToken_ReturnsUnauthorized()
    {
        var client = _fixture.CreateClient();

        var request = new
        {
            Checks = new[]
            {
                new
                {
                    Permission = "test:read",
                    ResourceType = (string?)null,
                    ResourceId = (string?)null,
                },
            },
        };
        var response = await client.PostAsJsonAsync("/authz/evaluate", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_WithValidToken_ReturnsBulkResults()
    {
        var client = _fixture.CreateClient();
        var token = await _fixture.CreateTestUserAndGetToken("authz-eval@example.com");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            Checks = new[]
            {
                new
                {
                    Permission = "user:profile",
                    ResourceType = (string?)null,
                    ResourceId = (string?)null,
                },
                new
                {
                    Permission = "admin:users",
                    ResourceType = (string?)null,
                    ResourceId = (string?)null,
                },
                new
                {
                    Permission = "user:credentials",
                    ResourceType = (string?)null,
                    ResourceId = (string?)null,
                },
            },
        };

        var response = await client.PostAsJsonAsync("/authz/evaluate", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.TryGetProperty("Results", out var results));
        Assert.Equal(3, results.GetArrayLength());

        var resultsList = results.EnumerateArray().ToList();

        // user:profile - allowed
        Assert.True(resultsList[0].GetProperty("Allowed").GetBoolean());

        // admin:users - denied
        Assert.False(resultsList[1].GetProperty("Allowed").GetBoolean());

        // user:credentials - allowed
        Assert.True(resultsList[2].GetProperty("Allowed").GetBoolean());
    }

    [Fact]
    public async Task Evaluate_EmptyChecks_ReturnsEmptyResults()
    {
        var client = _fixture.CreateClient();
        var token = await _fixture.CreateTestUserAndGetToken("authz-empty@example.com");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new { Checks = Array.Empty<object>() };

        var response = await client.PostAsJsonAsync("/authz/evaluate", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.TryGetProperty("Results", out var results));
        Assert.Equal(0, results.GetArrayLength());
    }

    [Fact]
    public async Task Check_WithResourceGrant_AllowsAccessToSpecificResource()
    {
        var client = _fixture.CreateClient();
        var (token, userId) = await _fixture.CreateTestUserAndGetTokenWithId(
            "resource-grant@example.com"
        );
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Grant access to a specific patient record
        await _fixture.GrantResourceAccess(userId, "patient", "patient-123", "patient:read");

        var response = await client.GetAsync(
            "/authz/check?permission=patient:read&resourceType=patient&resourceId=patient-123"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("Allowed").GetBoolean());
        Assert.Contains("resource-grant", doc.RootElement.GetProperty("Reason").GetString());
    }

    [Fact]
    public async Task Check_WithResourceGrant_DeniesAccessToDifferentResource()
    {
        var client = _fixture.CreateClient();
        var (token, userId) = await _fixture.CreateTestUserAndGetTokenWithId(
            "resource-deny@example.com"
        );
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Grant access only to patient-123
        await _fixture.GrantResourceAccess(userId, "patient", "patient-123", "patient:read");

        // Check access to patient-456 (should be denied)
        var response = await client.GetAsync(
            "/authz/check?permission=patient:read&resourceType=patient&resourceId=patient-456"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        Assert.False(doc.RootElement.GetProperty("Allowed").GetBoolean());
    }

    [Fact]
    public async Task Check_WithExpiredResourceGrant_DeniesAccess()
    {
        var client = _fixture.CreateClient();
        var (token, userId) = await _fixture.CreateTestUserAndGetTokenWithId(
            "expired-grant@example.com"
        );
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Grant access that's already expired
        await _fixture.GrantResourceAccessExpired(userId, "order", "order-999", "order:read");

        var response = await client.GetAsync(
            "/authz/check?permission=order:read&resourceType=order&resourceId=order-999"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        Assert.False(doc.RootElement.GetProperty("Allowed").GetBoolean());
    }
}

/// <summary>
/// Test fixture providing shared setup for Gatekeeper tests.
/// Creates test users and tokens without WebAuthn ceremony.
/// </summary>
public sealed class GatekeeperTestFixture : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly byte[] _signingKey;

    public GatekeeperTestFixture()
    {
        // Use full absolute path for the test database
        var dbPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"gatekeeper-test-{Guid.NewGuid()}.db"));
        _signingKey = new byte[32];

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DbPath", dbPath);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(_signingKey));
        });

        // Initialize database by making HTTP requests through the factory
        // This ensures the app creates and seeds the database before we access it directly
        using var client = _factory.CreateClient();
        // Make a request that forces full app initialization
        _ = client.PostAsJsonAsync("/auth/login/begin", new { }).GetAwaiter().GetResult();
    }

    /// <summary>Gets the connection string from the app's DbConfig singleton.</summary>
    private string GetConnectionString() =>
        _factory.Services.GetRequiredService<DbConfig>().ConnectionString;

    /// <summary>Creates a fresh HTTP client for testing.</summary>
    public HttpClient CreateClient() => _factory.CreateClient();

    /// <summary>
    /// Creates a test user and returns a valid JWT token.
    /// Bypasses WebAuthn by directly inserting user and generating token.
    /// </summary>
    public async Task<string> CreateTestUserAndGetToken(string email)
    {
        var (token, _) = await CreateTestUserAndGetTokenWithId(email).ConfigureAwait(false);
        return token;
    }

    /// <summary>
    /// Creates a test user and returns both the token and user ID.
    /// </summary>
    public async Task<(string Token, string UserId)> CreateTestUserAndGetTokenWithId(string email)
    {
        using var conn = new SqliteConnection(GetConnectionString());
        await conn.OpenAsync().ConfigureAwait(false);

        var userId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        // Insert user
        using var userCmd = conn.CreateCommand();
        userCmd.CommandText = """
            INSERT INTO gk_user (id, display_name, email, created_at, is_active)
            VALUES (@id, @name, @email, @now, 1)
            """;
        userCmd.Parameters.AddWithValue("@id", userId);
        userCmd.Parameters.AddWithValue("@name", "Test User");
        userCmd.Parameters.AddWithValue("@email", email);
        userCmd.Parameters.AddWithValue("@now", now);
        await userCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        // Link user to role
        using var roleCmd = conn.CreateCommand();
        roleCmd.CommandText = """
            INSERT INTO gk_user_role (user_id, role_id, granted_at)
            VALUES (@id, 'role-user', @now)
            """;
        roleCmd.Parameters.AddWithValue("@id", userId);
        roleCmd.Parameters.AddWithValue("@now", now);
        await roleCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        var token = TokenService.CreateToken(
            userId,
            "Test User",
            email,
            ["user"],
            _signingKey,
            TimeSpan.FromHours(1)
        );

        return (token, userId);
    }

    /// <summary>
    /// Creates an admin user and returns a valid JWT token.
    /// </summary>
    public async Task<string> CreateAdminUserAndGetToken(string email)
    {
        using var conn = new SqliteConnection(GetConnectionString());
        await conn.OpenAsync().ConfigureAwait(false);

        var userId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        // Insert user
        using var userCmd = conn.CreateCommand();
        userCmd.CommandText = """
            INSERT INTO gk_user (id, display_name, email, created_at, is_active)
            VALUES (@id, @name, @email, @now, 1)
            """;
        userCmd.Parameters.AddWithValue("@id", userId);
        userCmd.Parameters.AddWithValue("@name", "Admin User");
        userCmd.Parameters.AddWithValue("@email", email);
        userCmd.Parameters.AddWithValue("@now", now);
        await userCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        // Link user to admin role
        using var roleCmd = conn.CreateCommand();
        roleCmd.CommandText = """
            INSERT INTO gk_user_role (user_id, role_id, granted_at)
            VALUES (@id, 'role-admin', @now)
            """;
        roleCmd.Parameters.AddWithValue("@id", userId);
        roleCmd.Parameters.AddWithValue("@now", now);
        await roleCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        var token = TokenService.CreateToken(
            userId,
            "Admin User",
            email,
            ["admin"],
            _signingKey,
            TimeSpan.FromHours(1)
        );

        return token;
    }

    /// <summary>
    /// Grants resource-level access to a user.
    /// </summary>
    public async Task GrantResourceAccess(
        string userId,
        string resourceType,
        string resourceId,
        string permissionCode
    )
    {
        using var conn = new SqliteConnection(GetConnectionString());
        await conn.OpenAsync().ConfigureAwait(false);

        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var grantId = Guid.NewGuid().ToString();
        var permId = $"perm-{permissionCode}-{Guid.NewGuid():N}";

        // First ensure the permission exists
        using var permCmd = conn.CreateCommand();
        permCmd.CommandText = """
            INSERT INTO gk_permission (id, code, resource_type, action, created_at)
            VALUES (@id, @code, @resourceType, @action, @now);
            """;
        permCmd.Parameters.AddWithValue("@id", permId);
        permCmd.Parameters.AddWithValue("@code", permissionCode);
        permCmd.Parameters.AddWithValue("@resourceType", resourceType);
        permCmd.Parameters.AddWithValue(
            "@action",
            permissionCode.Split(':').LastOrDefault() ?? "read"
        );
        permCmd.Parameters.AddWithValue("@now", now);
        await permCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        // Then grant access
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO gk_resource_grant (id, user_id, resource_type, resource_id, permission_id, granted_at)
            VALUES (@id, @userId, @resourceType, @resourceId, @permId, @now);
            """;
        cmd.Parameters.AddWithValue("@id", grantId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@resourceType", resourceType);
        cmd.Parameters.AddWithValue("@resourceId", resourceId);
        cmd.Parameters.AddWithValue("@permId", permId);
        cmd.Parameters.AddWithValue("@now", now);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Grants resource-level access that has already expired.
    /// </summary>
    public async Task GrantResourceAccessExpired(
        string userId,
        string resourceType,
        string resourceId,
        string permissionCode
    )
    {
        using var conn = new SqliteConnection(GetConnectionString());
        await conn.OpenAsync().ConfigureAwait(false);

        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var expired = DateTime.UtcNow.AddHours(-1).ToString("o", CultureInfo.InvariantCulture);
        var grantId = Guid.NewGuid().ToString();
        var permId = $"perm-{permissionCode}-{Guid.NewGuid():N}";

        // First ensure the permission exists
        using var permCmd = conn.CreateCommand();
        permCmd.CommandText = """
            INSERT INTO gk_permission (id, code, resource_type, action, created_at)
            VALUES (@id, @code, @resourceType, @action, @now);
            """;
        permCmd.Parameters.AddWithValue("@id", permId);
        permCmd.Parameters.AddWithValue("@code", permissionCode);
        permCmd.Parameters.AddWithValue("@resourceType", resourceType);
        permCmd.Parameters.AddWithValue(
            "@action",
            permissionCode.Split(':').LastOrDefault() ?? "read"
        );
        permCmd.Parameters.AddWithValue("@now", now);
        await permCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        // Then grant access with expired timestamp
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO gk_resource_grant (id, user_id, resource_type, resource_id, permission_id, granted_at, expires_at)
            VALUES (@id, @userId, @resourceType, @resourceId, @permId, @now, @expired);
            """;
        cmd.Parameters.AddWithValue("@id", grantId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@resourceType", resourceType);
        cmd.Parameters.AddWithValue("@resourceId", resourceId);
        cmd.Parameters.AddWithValue("@permId", permId);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.Parameters.AddWithValue("@expired", expired);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>Disposes the test fixture.</summary>
    public void Dispose()
    {
        _factory.Dispose();
    }
}
