#pragma warning disable CS8509 // Exhaustive switch
#pragma warning disable IDE0037 // Use inferred member name

using System.Security.Cryptography;
using System.Text;
using Gatekeeper.Api;
using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(options =>
    options.SerializerOptions.PropertyNamingPolicy = null
);

builder.Services.AddCors(options =>
    options.AddPolicy(
        "Dashboard",
        policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
    )
);

var serverDomain = builder.Configuration["Fido2:ServerDomain"] ?? "localhost";
var serverName = builder.Configuration["Fido2:ServerName"] ?? "Gatekeeper";
var origin = builder.Configuration["Fido2:Origin"] ?? "http://localhost:5173";

builder.Services.AddFido2(options =>
{
    options.ServerDomain = serverDomain;
    options.ServerName = serverName;
    options.Origins = new HashSet<string> { origin };
    options.TimestampDriftTolerance = 300000;
});

var dbPath =
    builder.Configuration["DbPath"] ?? Path.Combine(AppContext.BaseDirectory, "gatekeeper.db");
var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    ForeignKeys = true,
    Pooling = false, // Disable pooling for test isolation
    Cache = SqliteCacheMode.Shared, // Use shared cache for better cross-connection visibility
}.ToString();

builder.Services.AddSingleton(new DbConfig(connectionString));

var signingKeyBase64 = builder.Configuration["Jwt:SigningKey"];
var signingKey = string.IsNullOrEmpty(signingKeyBase64)
    ? RandomNumberGenerator.GetBytes(32)
    : Convert.FromBase64String(signingKeyBase64);
builder.Services.AddSingleton(new JwtConfig(signingKey, TimeSpan.FromHours(24)));

var app = builder.Build();

using (var conn = new SqliteConnection(connectionString))
{
    conn.Open();
    DatabaseSetup.Initialize(conn, app.Logger);
}

app.UseCors("Dashboard");

static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

static SqliteConnection OpenConnection(DbConfig db)
{
    var conn = new SqliteConnection(db.ConnectionString);
    conn.Open();
    return conn;
}

var authGroup = app.MapGroup("/auth").WithTags("Authentication");

authGroup.MapPost(
    "/register/begin",
    async (RegisterBeginRequest request, IFido2 fido2, DbConfig db, ILogger<Program> logger) =>
    {
        try
        {
            using var conn = OpenConnection(db);
            var now = Now();

            var existingUser = await conn.GetUserByEmailAsync(request.Email).ConfigureAwait(false);
            var isNewUser = existingUser is not GetUserByEmailOk { Value.Count: > 0 };
            var userId = isNewUser
                ? Guid.NewGuid().ToString()
                : ((GetUserByEmailOk)existingUser).Value[0].id;

            if (isNewUser)
            {
                await using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);
                _ = await tx.Insertgk_userAsync(
                        userId,
                        request.DisplayName,
                        request.Email,
                        now,
                        null,
                        1,
                        null
                    )
                    .ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
            }

            var existingCredentials = await conn.GetUserCredentialsAsync(userId)
                .ConfigureAwait(false);
            var excludeCredentials = existingCredentials switch
            {
                GetUserCredentialsOk ok => ok
                    .Value.Select(c => new PublicKeyCredentialDescriptor(
                        Convert.FromBase64String(c.id)
                    ))
                    .ToList(),
                GetUserCredentialsError _ => [],
            };

            var user = new Fido2User
            {
                Id = Encoding.UTF8.GetBytes(userId),
                Name = request.Email,
                DisplayName = request.DisplayName,
            };
            // Don't restrict to platform authenticators only - allows security keys too
            // Chrome on macOS can timeout with Platform-only restriction
            var authSelector = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Required,
            };

            var options = fido2.RequestNewCredential(
                new RequestNewCredentialParams
                {
                    User = user,
                    ExcludeCredentials = excludeCredentials,
                    AuthenticatorSelection = authSelector,
                    AttestationPreference = AttestationConveyancePreference.None,
                }
            );
            var challengeId = Guid.NewGuid().ToString();
            var challengeExpiry = DateTime
                .UtcNow.AddMinutes(5)
                .ToString("o", CultureInfo.InvariantCulture);

            await using var tx2 = await conn.BeginTransactionAsync().ConfigureAwait(false);
            _ = await tx2.Insertgk_challengeAsync(
                    challengeId,
                    userId,
                    options.Challenge,
                    "registration",
                    now,
                    challengeExpiry
                )
                .ConfigureAwait(false);
            await tx2.CommitAsync().ConfigureAwait(false);

            return Results.Ok(new { ChallengeId = challengeId, OptionsJson = options.ToJson() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Registration begin failed");
            return Results.Problem("Registration failed");
        }
    }
);

authGroup.MapPost(
    "/login/begin",
    async (IFido2 fido2, DbConfig db, ILogger<Program> logger) =>
    {
        try
        {
            using var conn = OpenConnection(db);
            var now = Now();

            // Discoverable credentials: empty allowCredentials lets browser show all stored passkeys
            // The credential contains userHandle which we use in /login/complete to identify the user
            // See: https://webauthn.guide/ and fido2-net-lib docs
            var options = fido2.GetAssertionOptions(
                new GetAssertionOptionsParams
                {
                    AllowedCredentials = [], // Empty = discoverable credentials
                    UserVerification = UserVerificationRequirement.Required,
                }
            );
            var challengeId = Guid.NewGuid().ToString();
            var challengeExpiry = DateTime
                .UtcNow.AddMinutes(5)
                .ToString("o", CultureInfo.InvariantCulture);

            await using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);
            _ = await tx.Insertgk_challengeAsync(
                    challengeId,
                    null, // No user ID - discovered from credential in /login/complete
                    options.Challenge,
                    "authentication",
                    now,
                    challengeExpiry
                )
                .ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);

            return Results.Ok(new { ChallengeId = challengeId, OptionsJson = options.ToJson() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login begin failed");
            return Results.Problem("Login failed");
        }
    }
);

authGroup.MapPost(
    "/register/complete",
    async (
        RegisterCompleteRequest request,
        IFido2 fido2,
        DbConfig db,
        JwtConfig jwtConfig,
        ILogger<Program> logger
    ) =>
    {
        try
        {
            using var conn = OpenConnection(db);
            var now = Now();

            // Get the stored challenge
            var challengeResult = await conn.GetChallengeByIdAsync(request.ChallengeId, now)
                .ConfigureAwait(false);
            if (challengeResult is not GetChallengeByIdOk { Value.Count: > 0 } challengeOk)
            {
                return Results.BadRequest(new { Error = "Challenge not found or expired" });
            }

            var storedChallenge = challengeOk.Value[0];
            if (string.IsNullOrEmpty(storedChallenge.user_id))
            {
                return Results.BadRequest(new { Error = "Invalid challenge" });
            }

            // Parse the authenticator response
            var options = CredentialCreateOptions.FromJson(request.OptionsJson);

            // Verify the attestation
            var credentialResult = await fido2
                .MakeNewCredentialAsync(
                    new MakeNewCredentialParams
                    {
                        AttestationResponse = request.AttestationResponse,
                        OriginalOptions = options,
                        IsCredentialIdUniqueToUserCallback = async (args, ct) =>
                        {
                            var existing = await conn.GetCredentialByIdAsync(
                                    Base64Url.Encode(args.CredentialId)
                                )
                                .ConfigureAwait(false);
                            return existing is not GetCredentialByIdOk { Value.Count: > 0 };
                        },
                    }
                )
                .ConfigureAwait(false);

            var cred = credentialResult;

            // Store the credential - use base64url encoding to match WebAuthn spec
            await using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);
            _ = await tx.Insertgk_credentialAsync(
                    Base64Url.Encode(cred.Id),
                    storedChallenge.user_id,
                    cred.PublicKey,
                    cred.SignCount,
                    cred.AaGuid.ToString(),
                    cred.Type.ToString(),
                    cred.Transports != null ? string.Join(",", cred.Transports) : null,
                    cred.AttestationFormat,
                    now,
                    null,
                    request.DeviceName,
                    cred.IsBackupEligible ? 1 : 0,
                    cred.IsBackedUp ? 1 : 0
                )
                .ConfigureAwait(false);

            // Assign default user role
            _ = await tx.Insertgk_user_roleAsync(
                    storedChallenge.user_id,
                    "role-user",
                    now,
                    null,
                    null
                )
                .ConfigureAwait(false);

            await tx.CommitAsync().ConfigureAwait(false);

            // Get user info for token
            var userResult = await conn.GetUserByIdAsync(storedChallenge.user_id)
                .ConfigureAwait(false);
            var user = userResult is GetUserByIdOk { Value.Count: > 0 } userOk
                ? userOk.Value[0]
                : null;

            // Get user roles
            var rolesResult = await conn.GetUserRolesAsync(storedChallenge.user_id, now)
                .ConfigureAwait(false);
            var roles = rolesResult is GetUserRolesOk rolesOk
                ? rolesOk.Value.Select(r => r.name).ToList()
                : [];

            // Generate JWT
            var token = TokenService.CreateToken(
                storedChallenge.user_id,
                user?.display_name,
                user?.email,
                roles,
                jwtConfig.SigningKey,
                jwtConfig.TokenLifetime
            );

            return Results.Ok(
                new
                {
                    Token = token,
                    UserId = storedChallenge.user_id,
                    DisplayName = user?.display_name,
                    Email = user?.email,
                    Roles = roles,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Registration complete failed");
            return Results.Problem("Registration failed");
        }
    }
);

authGroup.MapPost(
    "/login/complete",
    async (
        LoginCompleteRequest request,
        IFido2 fido2,
        DbConfig db,
        JwtConfig jwtConfig,
        ILogger<Program> logger
    ) =>
    {
        try
        {
            using var conn = OpenConnection(db);
            var now = Now();

            // Get the stored challenge
            var challengeResult = await conn.GetChallengeByIdAsync(request.ChallengeId, now)
                .ConfigureAwait(false);
            if (challengeResult is not GetChallengeByIdOk { Value.Count: > 0 } challengeOk)
            {
                return Results.BadRequest(new { Error = "Challenge not found or expired" });
            }

            var storedChallenge = challengeOk.Value[0];

            // Get credential from database - Id is already base64url encoded
            var credentialId = request.AssertionResponse.Id;
            var credResult = await conn.GetCredentialByIdAsync(credentialId).ConfigureAwait(false);
            if (credResult is not GetCredentialByIdOk { Value.Count: > 0 } credOk)
            {
                return Results.BadRequest(new { Error = "Credential not found" });
            }

            var storedCred = credOk.Value[0];

            // Parse the assertion options
            var options = AssertionOptions.FromJson(request.OptionsJson);

            // Verify the assertion
            var assertionResult = await fido2
                .MakeAssertionAsync(
                    new MakeAssertionParams
                    {
                        AssertionResponse = request.AssertionResponse,
                        OriginalOptions = options,
                        StoredPublicKey = storedCred.public_key,
                        StoredSignatureCounter = (uint)storedCred.sign_count,
                        IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                        {
                            var userIdFromHandle = Encoding.UTF8.GetString(args.UserHandle);
                            return Task.FromResult(storedCred.user_id == userIdFromHandle);
                        },
                    }
                )
                .ConfigureAwait(false);

            // Update sign count and last used
            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText =
                @"
                UPDATE gk_credential
                SET sign_count = @signCount, last_used_at = @now
                WHERE id = @id";
            updateCmd.Parameters.AddWithValue("@signCount", (long)assertionResult.SignCount);
            updateCmd.Parameters.AddWithValue("@now", now);
            updateCmd.Parameters.AddWithValue("@id", credentialId);
            await updateCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Update user last login
            using var userUpdateCmd = conn.CreateCommand();
            userUpdateCmd.CommandText = "UPDATE gk_user SET last_login_at = @now WHERE id = @id";
            userUpdateCmd.Parameters.AddWithValue("@now", now);
            userUpdateCmd.Parameters.AddWithValue("@id", storedCred.user_id);
            await userUpdateCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Get user info for token
            var userResult = await conn.GetUserByIdAsync(storedCred.user_id).ConfigureAwait(false);
            var user = userResult is GetUserByIdOk { Value.Count: > 0 } userOk
                ? userOk.Value[0]
                : null;

            // Get user roles
            var rolesResult = await conn.GetUserRolesAsync(storedCred.user_id, now)
                .ConfigureAwait(false);
            var roles = rolesResult is GetUserRolesOk rolesOk
                ? rolesOk.Value.Select(r => r.name).ToList()
                : [];

            // Generate JWT
            var token = TokenService.CreateToken(
                storedCred.user_id,
                user?.display_name,
                user?.email,
                roles,
                jwtConfig.SigningKey,
                jwtConfig.TokenLifetime
            );

            return Results.Ok(
                new
                {
                    Token = token,
                    UserId = storedCred.user_id,
                    DisplayName = user?.display_name,
                    Email = user?.email,
                    Roles = roles,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login complete failed");
            return Results.Problem("Login failed");
        }
    }
);

authGroup.MapGet(
    "/session",
    async (HttpContext ctx, DbConfig db, JwtConfig jwtConfig) =>
    {
        var token = TokenService.ExtractBearerToken(ctx.Request.Headers.Authorization);
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = OpenConnection(db);

        var result = await TokenService
            .ValidateTokenAsync(conn, token, jwtConfig.SigningKey, checkRevocation: true)
            .ConfigureAwait(false);
        if (result is not TokenService.TokenValidationOk ok)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(
            new
            {
                ok.Claims.UserId,
                ok.Claims.DisplayName,
                ok.Claims.Email,
                ok.Claims.Roles,
                ExpiresAt = DateTimeOffset
                    .FromUnixTimeSeconds(ok.Claims.Exp)
                    .ToString("o", CultureInfo.InvariantCulture),
            }
        );
    }
);

authGroup.MapPost(
    "/logout",
    async (HttpContext ctx, DbConfig db, JwtConfig jwtConfig) =>
    {
        var token = TokenService.ExtractBearerToken(ctx.Request.Headers.Authorization);
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = OpenConnection(db);

        var result = await TokenService
            .ValidateTokenAsync(conn, token, jwtConfig.SigningKey, checkRevocation: false)
            .ConfigureAwait(false);
        if (result is TokenService.TokenValidationOk ok)
        {
            await TokenService.RevokeTokenAsync(conn, ok.Claims.Jti).ConfigureAwait(false);
        }

        return Results.NoContent();
    }
);

var authzGroup = app.MapGroup("/authz").WithTags("Authorization");

authzGroup.MapGet(
    "/check",
    async (
        string permission,
        string? resourceType,
        string? resourceId,
        HttpContext ctx,
        DbConfig db,
        JwtConfig jwtConfig
    ) =>
    {
        var token = TokenService.ExtractBearerToken(ctx.Request.Headers.Authorization);
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = OpenConnection(db);

        var validateResult = await TokenService
            .ValidateTokenAsync(conn, token, jwtConfig.SigningKey, checkRevocation: true)
            .ConfigureAwait(false);
        if (validateResult is not TokenService.TokenValidationOk ok)
        {
            return Results.Unauthorized();
        }

        var (allowed, reason) = await AuthorizationService
            .CheckPermissionAsync(
                conn,
                ok.Claims.UserId,
                permission,
                resourceType,
                resourceId,
                Now()
            )
            .ConfigureAwait(false);
        return Results.Ok(new { Allowed = allowed, Reason = reason });
    }
);

authzGroup.MapGet(
    "/permissions",
    async (HttpContext ctx, DbConfig db, JwtConfig jwtConfig) =>
    {
        var token = TokenService.ExtractBearerToken(ctx.Request.Headers.Authorization);
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = OpenConnection(db);

        var validateResult = await TokenService
            .ValidateTokenAsync(conn, token, jwtConfig.SigningKey, checkRevocation: true)
            .ConfigureAwait(false);
        if (validateResult is not TokenService.TokenValidationOk ok)
        {
            return Results.Unauthorized();
        }

        var permissionsResult = await conn.GetUserPermissionsAsync(ok.Claims.UserId, Now())
            .ConfigureAwait(false);
        var permissions = permissionsResult is GetUserPermissionsOk permOk
            ? permOk
                .Value.Select(p => new
                {
                    p.code,
                    p.source_name,
                    p.source_type,
                    p.scope_type,
                    p.scope_value,
                })
                .ToList()
            : [];

        return Results.Ok(new { Permissions = permissions });
    }
);

authzGroup.MapPost(
    "/evaluate",
    async (EvaluateRequest request, HttpContext ctx, DbConfig db, JwtConfig jwtConfig) =>
    {
        var token = TokenService.ExtractBearerToken(ctx.Request.Headers.Authorization);
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = OpenConnection(db);

        var validateResult = await TokenService
            .ValidateTokenAsync(conn, token, jwtConfig.SigningKey, checkRevocation: true)
            .ConfigureAwait(false);
        if (validateResult is not TokenService.TokenValidationOk ok)
        {
            return Results.Unauthorized();
        }

        var now = Now();
        var results = new List<object>();
        foreach (var check in request.Checks)
        {
            var (allowed, _) = await AuthorizationService
                .CheckPermissionAsync(
                    conn,
                    ok.Claims.UserId,
                    check.Permission,
                    check.ResourceType,
                    check.ResourceId,
                    now
                )
                .ConfigureAwait(false);
            results.Add(
                new
                {
                    check.Permission,
                    check.ResourceId,
                    Allowed = allowed,
                }
            );
        }

        return Results.Ok(new { Results = results });
    }
);

app.Run();

namespace Gatekeeper.Api
{
    /// <summary>
    /// Program entry point marker for WebApplicationFactory.
    /// </summary>
    public partial class Program { }

    /// <summary>Database connection configuration.</summary>
    public sealed record DbConfig(string ConnectionString);

    /// <summary>JWT signing configuration.</summary>
    public sealed record JwtConfig(byte[] SigningKey, TimeSpan TokenLifetime);

    /// <summary>Request to begin passkey registration.</summary>
    public sealed record RegisterBeginRequest(string Email, string DisplayName);

    /// <summary>Request to begin passkey login.</summary>
    public sealed record LoginBeginRequest(string? Email);

    /// <summary>Request to evaluate multiple permissions.</summary>
    public sealed record EvaluateRequest(List<PermissionCheck> Checks);

    /// <summary>Single permission check.</summary>
    public sealed record PermissionCheck(
        string Permission,
        string? ResourceType,
        string? ResourceId
    );

    /// <summary>Request to complete passkey registration.</summary>
    public sealed record RegisterCompleteRequest(
        string ChallengeId,
        string OptionsJson,
        AuthenticatorAttestationRawResponse AttestationResponse,
        string? DeviceName
    );

    /// <summary>Request to complete passkey login.</summary>
    public sealed record LoginCompleteRequest(
        string ChallengeId,
        string OptionsJson,
        AuthenticatorAssertionRawResponse AssertionResponse
    );

    /// <summary>Base64URL encoding utilities for WebAuthn credential IDs.</summary>
    public static class Base64Url
    {
        /// <summary>Encodes bytes to base64url string.</summary>
        public static string Encode(byte[] input) =>
            Convert
                .ToBase64String(input)
                .Replace("+", "-", StringComparison.Ordinal)
                .Replace("/", "_", StringComparison.Ordinal)
                .TrimEnd('=');

        /// <summary>Decodes base64url string to bytes.</summary>
        public static byte[] Decode(string input)
        {
            var padded = input
                .Replace("-", "+", StringComparison.Ordinal)
                .Replace("_", "/", StringComparison.Ordinal);
            var padding = (4 - (padded.Length % 4)) % 4;
            padded += new string('=', padding);
            return Convert.FromBase64String(padded);
        }
    }
}
