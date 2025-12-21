namespace Gatekeeper.Api.Tests;

using System.Globalization;
using Generated;
using Microsoft.Data.Sqlite;

/// <summary>
/// Unit tests for TokenService JWT creation, validation, and revocation.
/// </summary>
public sealed class TokenServiceTests
{
    private static readonly byte[] TestSigningKey = new byte[32];

    [Fact]
    public void CreateToken_ReturnsValidJwtFormat()
    {
        var token = TokenService.CreateToken(
            "user-123",
            "Test User",
            "test@example.com",
            ["user", "admin"],
            TestSigningKey,
            TimeSpan.FromHours(1)
        );

        // JWT has 3 parts separated by dots
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        // All parts should be base64url encoded (no padding)
        Assert.DoesNotContain("=", parts[0]);
        Assert.DoesNotContain("=", parts[1]);
        Assert.DoesNotContain("=", parts[2]);
    }

    [Fact]
    public void CreateToken_HeaderContainsCorrectAlgorithm()
    {
        var token = TokenService.CreateToken(
            "user-123",
            "Test User",
            "test@example.com",
            ["user"],
            TestSigningKey,
            TimeSpan.FromHours(1)
        );

        var parts = token.Split('.');
        var headerJson = Base64UrlDecode(parts[0]);
        var header = JsonDocument.Parse(headerJson);

        Assert.Equal("HS256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal("JWT", header.RootElement.GetProperty("typ").GetString());
    }

    [Fact]
    public void CreateToken_PayloadContainsAllClaims()
    {
        var token = TokenService.CreateToken(
            "user-456",
            "Jane Doe",
            "jane@example.com",
            ["admin", "manager"],
            TestSigningKey,
            TimeSpan.FromHours(2)
        );

        var parts = token.Split('.');
        var payloadJson = Base64UrlDecode(parts[1]);
        var payload = JsonDocument.Parse(payloadJson);

        Assert.Equal("user-456", payload.RootElement.GetProperty("sub").GetString());
        Assert.Equal("Jane Doe", payload.RootElement.GetProperty("name").GetString());
        Assert.Equal("jane@example.com", payload.RootElement.GetProperty("email").GetString());

        var roles = payload
            .RootElement.GetProperty("roles")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();
        Assert.Contains("admin", roles);
        Assert.Contains("manager", roles);

        Assert.True(payload.RootElement.TryGetProperty("jti", out var jti));
        Assert.False(string.IsNullOrEmpty(jti.GetString()));

        Assert.True(payload.RootElement.TryGetProperty("iat", out _));
        Assert.True(payload.RootElement.TryGetProperty("exp", out _));
    }

    [Fact]
    public void CreateToken_ExpirationIsCorrect()
    {
        var beforeCreate = DateTimeOffset.UtcNow;

        var token = TokenService.CreateToken(
            "user-789",
            "Test",
            "test@example.com",
            [],
            TestSigningKey,
            TimeSpan.FromMinutes(30)
        );

        var parts = token.Split('.');
        var payloadJson = Base64UrlDecode(parts[1]);
        var payload = JsonDocument.Parse(payloadJson);

        var exp = payload.RootElement.GetProperty("exp").GetInt64();
        var iat = payload.RootElement.GetProperty("iat").GetInt64();
        var expTime = DateTimeOffset.FromUnixTimeSeconds(exp);
        var iatTime = DateTimeOffset.FromUnixTimeSeconds(iat);

        // exp should be ~30 minutes after iat
        var diff = expTime - iatTime;
        Assert.True(diff.TotalMinutes >= 29 && diff.TotalMinutes <= 31);

        // exp should be ~30 minutes from now
        var expFromNow = expTime - beforeCreate;
        Assert.True(expFromNow.TotalMinutes >= 29 && expFromNow.TotalMinutes <= 31);
    }

    [Fact]
    public async Task ValidateTokenAsync_ValidToken_ReturnsOk()
    {
        using var conn = CreateInMemoryDb();

        var token = TokenService.CreateToken(
            "user-valid",
            "Valid User",
            "valid@example.com",
            ["user"],
            TestSigningKey,
            TimeSpan.FromHours(1)
        );

        var result = await TokenService.ValidateTokenAsync(
            conn,
            token,
            TestSigningKey,
            checkRevocation: false
        );

        Assert.IsType<TokenService.TokenValidationOk>(result);
        var ok = (TokenService.TokenValidationOk)result;
        Assert.Equal("user-valid", ok.Claims.UserId);
        Assert.Equal("Valid User", ok.Claims.DisplayName);
        Assert.Equal("valid@example.com", ok.Claims.Email);
        Assert.Contains("user", ok.Claims.Roles);
    }

    [Fact]
    public async Task ValidateTokenAsync_InvalidFormat_ReturnsError()
    {
        using var conn = CreateInMemoryDb();

        var result = await TokenService.ValidateTokenAsync(
            conn,
            "not-a-jwt",
            TestSigningKey,
            checkRevocation: false
        );

        Assert.IsType<TokenService.TokenValidationError>(result);
        var error = (TokenService.TokenValidationError)result;
        Assert.Equal("Invalid token format", error.Reason);
    }

    [Fact]
    public async Task ValidateTokenAsync_TwoPartToken_ReturnsError()
    {
        using var conn = CreateInMemoryDb();

        var result = await TokenService.ValidateTokenAsync(
            conn,
            "header.payload",
            TestSigningKey,
            checkRevocation: false
        );

        Assert.IsType<TokenService.TokenValidationError>(result);
        var error = (TokenService.TokenValidationError)result;
        Assert.Equal("Invalid token format", error.Reason);
    }

    [Fact]
    public async Task ValidateTokenAsync_InvalidSignature_ReturnsError()
    {
        using var conn = CreateInMemoryDb();

        var token = TokenService.CreateToken(
            "user-sig",
            "Sig User",
            "sig@example.com",
            [],
            TestSigningKey,
            TimeSpan.FromHours(1)
        );

        // Use different key for validation
        var differentKey = new byte[32];
        differentKey[0] = 0xFF;

        var result = await TokenService.ValidateTokenAsync(
            conn,
            token,
            differentKey,
            checkRevocation: false
        );

        Assert.IsType<TokenService.TokenValidationError>(result);
        var error = (TokenService.TokenValidationError)result;
        Assert.Equal("Invalid signature", error.Reason);
    }

    [Fact]
    public async Task ValidateTokenAsync_ExpiredToken_ReturnsError()
    {
        using var conn = CreateInMemoryDb();

        // Create token that expired 1 hour ago
        var token = TokenService.CreateToken(
            "user-expired",
            "Expired User",
            "expired@example.com",
            [],
            TestSigningKey,
            TimeSpan.FromHours(-2) // Negative = already expired
        );

        var result = await TokenService.ValidateTokenAsync(
            conn,
            token,
            TestSigningKey,
            checkRevocation: false
        );

        Assert.IsType<TokenService.TokenValidationError>(result);
        var error = (TokenService.TokenValidationError)result;
        Assert.Equal("Token expired", error.Reason);
    }

    [Fact]
    public async Task ValidateTokenAsync_RevokedToken_ReturnsError()
    {
        using var conn = CreateInMemoryDb();

        var token = TokenService.CreateToken(
            "user-revoked",
            "Revoked User",
            "revoked@example.com",
            [],
            TestSigningKey,
            TimeSpan.FromHours(1)
        );

        // Extract JTI and revoke
        var parts = token.Split('.');
        var payloadJson = Base64UrlDecode(parts[1]);
        var payload = JsonDocument.Parse(payloadJson);
        var jti = payload.RootElement.GetProperty("jti").GetString()!;

        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var exp = DateTime.UtcNow.AddHours(1).ToString("o", CultureInfo.InvariantCulture);

        // Insert user and revoked session using DataProvider methods
        using var tx = conn.BeginTransaction();
        await tx.Insertgk_userAsync(
                "user-revoked",
                "Revoked User",
                null!, // email
                now,
                null!, // last_login_at
                1, // is_active
                null! // metadata
            )
            .ConfigureAwait(false);
        await tx.Insertgk_sessionAsync(
                jti,
                "user-revoked",
                null!, // credential_id
                now,
                exp,
                now,
                null!, // ip_address
                null!, // user_agent
                1 // is_revoked = true
            )
            .ConfigureAwait(false);
        tx.Commit();

        var result = await TokenService.ValidateTokenAsync(
            conn,
            token,
            TestSigningKey,
            checkRevocation: true
        );

        Assert.IsType<TokenService.TokenValidationError>(result);
        var error = (TokenService.TokenValidationError)result;
        Assert.Equal("Token revoked", error.Reason);
    }

    [Fact]
    public async Task ValidateTokenAsync_RevokedToken_IgnoredWhenCheckRevocationFalse()
    {
        using var conn = CreateInMemoryDb();

        var token = TokenService.CreateToken(
            "user-revoked2",
            "Revoked User 2",
            "revoked2@example.com",
            [],
            TestSigningKey,
            TimeSpan.FromHours(1)
        );

        // Extract JTI and revoke
        var parts = token.Split('.');
        var payloadJson = Base64UrlDecode(parts[1]);
        var payload = JsonDocument.Parse(payloadJson);
        var jti = payload.RootElement.GetProperty("jti").GetString()!;

        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var exp = DateTime.UtcNow.AddHours(1).ToString("o", CultureInfo.InvariantCulture);

        // Insert user and revoked session using DataProvider methods
        using var tx = conn.BeginTransaction();
        await tx.Insertgk_userAsync(
                "user-revoked2",
                "Revoked User 2",
                null!, // email
                now,
                null!, // last_login_at
                1, // is_active
                null! // metadata
            )
            .ConfigureAwait(false);
        await tx.Insertgk_sessionAsync(
                jti,
                "user-revoked2",
                null!, // credential_id
                now,
                exp,
                now,
                null!, // ip_address
                null!, // user_agent
                1 // is_revoked = true
            )
            .ConfigureAwait(false);
        tx.Commit();

        // With checkRevocation: false, should still validate
        var result = await TokenService.ValidateTokenAsync(
            conn,
            token,
            TestSigningKey,
            checkRevocation: false
        );

        Assert.IsType<TokenService.TokenValidationOk>(result);
    }

    [Fact]
    public async Task RevokeTokenAsync_SetsIsRevokedFlag()
    {
        using var conn = CreateInMemoryDb();

        var jti = Guid.NewGuid().ToString();
        var userId = "user-test";
        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var exp = DateTime.UtcNow.AddHours(1).ToString("o", CultureInfo.InvariantCulture);

        // Insert user and session using DataProvider methods
        using var tx = conn.BeginTransaction();
        await tx.Insertgk_userAsync(
                userId,
                "Test User",
                null!, // email
                now,
                null!, // last_login_at
                1, // is_active
                null! // metadata
            )
            .ConfigureAwait(false);
        await tx.Insertgk_sessionAsync(
                jti,
                userId,
                null!, // credential_id
                now,
                exp,
                now,
                null!, // ip_address
                null!, // user_agent
                0 // is_revoked = false
            )
            .ConfigureAwait(false);
        tx.Commit();

        // Revoke
        await TokenService.RevokeTokenAsync(conn, jti);

        // Verify
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT is_revoked FROM gk_session WHERE id = @jti";
        checkCmd.Parameters.AddWithValue("@jti", jti);
        var isRevoked = await checkCmd.ExecuteScalarAsync();

        Assert.Equal(1L, isRevoked);
    }

    [Fact]
    public void ExtractBearerToken_ValidHeader_ReturnsToken()
    {
        var token = TokenService.ExtractBearerToken("Bearer abc123xyz");

        Assert.Equal("abc123xyz", token);
    }

    [Fact]
    public void ExtractBearerToken_NullHeader_ReturnsNull()
    {
        var token = TokenService.ExtractBearerToken(null);

        Assert.Null(token);
    }

    [Fact]
    public void ExtractBearerToken_EmptyHeader_ReturnsNull()
    {
        var token = TokenService.ExtractBearerToken("");

        Assert.Null(token);
    }

    [Fact]
    public void ExtractBearerToken_NonBearerScheme_ReturnsNull()
    {
        var token = TokenService.ExtractBearerToken("Basic abc123xyz");

        Assert.Null(token);
    }

    [Fact]
    public void ExtractBearerToken_BearerWithoutSpace_ReturnsNull()
    {
        var token = TokenService.ExtractBearerToken("Bearerabc123xyz");

        Assert.Null(token);
    }

    private static SqliteConnection CreateInMemoryDb()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS gk_user (
                id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                email TEXT,
                created_at TEXT NOT NULL,
                last_login_at TEXT,
                is_active INTEGER NOT NULL DEFAULT 1,
                metadata TEXT
            );

            CREATE TABLE IF NOT EXISTS gk_session (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL REFERENCES gk_user(id) ON DELETE CASCADE,
                credential_id TEXT,
                created_at TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                last_activity_at TEXT NOT NULL,
                ip_address TEXT,
                user_agent TEXT,
                is_revoked INTEGER NOT NULL DEFAULT 0
            );
            """;
        cmd.ExecuteNonQuery();

        return conn;
    }

    private static string Base64UrlDecode(string input)
    {
        var padded = input.Replace("-", "+").Replace("_", "/");
        var padding = (4 - (padded.Length % 4)) % 4;
        padded += new string('=', padding);
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
