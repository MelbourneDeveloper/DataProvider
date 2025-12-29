using System.Globalization;
using Gatekeeper.Migration;
using Microsoft.Data.Sqlite;
using Migration;
using Migration.SQLite;

namespace Gatekeeper.Api.Tests;

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

        // Insert user and revoked session using raw SQL (consistent with other tests)
        using var tx = conn.BeginTransaction();

        using var userCmd = conn.CreateCommand();
        userCmd.Transaction = tx;
        userCmd.CommandText =
            @"INSERT INTO gk_user (id, display_name, email, created_at, last_login_at, is_active, metadata)
                                VALUES (@id, @name, @email, @now, NULL, 1, NULL)";
        userCmd.Parameters.AddWithValue("@id", "user-revoked");
        userCmd.Parameters.AddWithValue("@name", "Revoked User");
        userCmd.Parameters.AddWithValue("@email", DBNull.Value);
        userCmd.Parameters.AddWithValue("@now", now);
        await userCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        using var sessionCmd = conn.CreateCommand();
        sessionCmd.Transaction = tx;
        sessionCmd.CommandText =
            @"INSERT INTO gk_session (id, user_id, credential_id, created_at, expires_at, last_activity_at, ip_address, user_agent, is_revoked)
                                   VALUES (@id, @user_id, NULL, @created, @expires, @activity, NULL, NULL, 1)";
        sessionCmd.Parameters.AddWithValue("@id", jti);
        sessionCmd.Parameters.AddWithValue("@user_id", "user-revoked");
        sessionCmd.Parameters.AddWithValue("@created", now);
        sessionCmd.Parameters.AddWithValue("@expires", exp);
        sessionCmd.Parameters.AddWithValue("@activity", now);
        await sessionCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

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

        // Insert user and revoked session using raw SQL (consistent with other tests)
        using var tx = conn.BeginTransaction();

        using var userCmd = conn.CreateCommand();
        userCmd.Transaction = tx;
        userCmd.CommandText =
            @"INSERT INTO gk_user (id, display_name, email, created_at, last_login_at, is_active, metadata)
                                VALUES (@id, @name, @email, @now, NULL, 1, NULL)";
        userCmd.Parameters.AddWithValue("@id", "user-revoked2");
        userCmd.Parameters.AddWithValue("@name", "Revoked User 2");
        userCmd.Parameters.AddWithValue("@email", DBNull.Value);
        userCmd.Parameters.AddWithValue("@now", now);
        await userCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        using var sessionCmd = conn.CreateCommand();
        sessionCmd.Transaction = tx;
        sessionCmd.CommandText =
            @"INSERT INTO gk_session (id, user_id, credential_id, created_at, expires_at, last_activity_at, ip_address, user_agent, is_revoked)
                                   VALUES (@id, @user_id, NULL, @created, @expires, @activity, NULL, NULL, 1)";
        sessionCmd.Parameters.AddWithValue("@id", jti);
        sessionCmd.Parameters.AddWithValue("@user_id", "user-revoked2");
        sessionCmd.Parameters.AddWithValue("@created", now);
        sessionCmd.Parameters.AddWithValue("@expires", exp);
        sessionCmd.Parameters.AddWithValue("@activity", now);
        await sessionCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

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

        // Insert user and session using raw SQL (TEXT PK doesn't return rowid)
        using var tx = conn.BeginTransaction();

        using var userCmd = conn.CreateCommand();
        userCmd.Transaction = tx;
        userCmd.CommandText =
            @"INSERT INTO gk_user (id, display_name, email, created_at, last_login_at, is_active, metadata)
                                VALUES (@id, @name, @email, @now, NULL, 1, NULL)";
        userCmd.Parameters.AddWithValue("@id", userId);
        userCmd.Parameters.AddWithValue("@name", "Test User");
        userCmd.Parameters.AddWithValue("@email", DBNull.Value);
        userCmd.Parameters.AddWithValue("@now", now);
        await userCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        using var sessionCmd = conn.CreateCommand();
        sessionCmd.Transaction = tx;
        sessionCmd.CommandText =
            @"INSERT INTO gk_session (id, user_id, credential_id, created_at, expires_at, last_activity_at, ip_address, user_agent, is_revoked)
                                   VALUES (@id, @user_id, NULL, @created, @expires, @activity, NULL, NULL, 0)";
        sessionCmd.Parameters.AddWithValue("@id", jti);
        sessionCmd.Parameters.AddWithValue("@user_id", userId);
        sessionCmd.Parameters.AddWithValue("@created", now);
        sessionCmd.Parameters.AddWithValue("@expires", exp);
        sessionCmd.Parameters.AddWithValue("@activity", now);
        await sessionCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        tx.Commit();

        // Revoke
        await TokenService.RevokeTokenAsync(conn, jti);

        // Verify using DataProvider generated method
        var revokedResult = await conn.GetSessionRevokedAsync(jti);
        var isRevoked = revokedResult switch
        {
            GetSessionRevokedOk ok => ok.Value.FirstOrDefault()?.is_revoked ?? -1L,
            GetSessionRevokedError err => throw new InvalidOperationException(
                $"GetSessionRevoked failed: {err.Value.Message}, {err.Value.InnerException?.Message}"
            ),
        };

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

        // Use the GatekeeperSchema migration to create only the needed tables
        // gk_credential is needed because gk_session has a FK to it
        var schema = GatekeeperSchema.Build();
        var neededTables = new[] { "gk_user", "gk_credential", "gk_session" };

        foreach (var table in schema.Tables.Where(t => neededTables.Contains(t.Name)))
        {
            var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(table));
            foreach (
                var statement in ddl.Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
            )
            {
                if (string.IsNullOrWhiteSpace(statement))
                {
                    continue;
                }
                using var cmd = conn.CreateCommand();
                cmd.CommandText = statement;
                cmd.ExecuteNonQuery();
            }
        }

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
