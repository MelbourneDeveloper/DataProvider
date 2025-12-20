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
}.ToString();

builder.Services.AddSingleton(connectionString);

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

static SqliteConnection OpenConnection(string connStr)
{
    var conn = new SqliteConnection(connStr);
    conn.Open();
    return conn;
}

var authGroup = app.MapGroup("/auth").WithTags("Authentication");

authGroup.MapPost(
    "/register/begin",
    async (RegisterBeginRequest request, IFido2 fido2, string connStr, ILogger<Program> logger) =>
    {
        try
        {
            using var conn = OpenConnection(connStr);
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
                _ => [],
            };

            var user = new Fido2User
            {
                Id = Encoding.UTF8.GetBytes(userId),
                Name = request.Email,
                DisplayName = request.DisplayName,
            };
            var authSelector = new AuthenticatorSelection
            {
                AuthenticatorAttachment = AuthenticatorAttachment.Platform,
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

            return Results.Ok(new { ChallengeId = challengeId, Options = options });
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
    async (LoginBeginRequest? request, IFido2 fido2, string connStr, ILogger<Program> logger) =>
    {
        try
        {
            using var conn = OpenConnection(connStr);
            var now = Now();
            string? userId = null;
            var allowCredentials = new List<PublicKeyCredentialDescriptor>();

            if (!string.IsNullOrEmpty(request?.Email))
            {
                var userResult = await conn.GetUserByEmailAsync(request.Email)
                    .ConfigureAwait(false);
                if (userResult is GetUserByEmailOk { Value.Count: > 0 } ok)
                {
                    userId = ok.Value[0].id;
                    var credResult = await conn.GetUserCredentialsAsync(userId)
                        .ConfigureAwait(false);
                    if (credResult is GetUserCredentialsOk credOk)
                    {
                        allowCredentials =
                        [
                            .. credOk.Value.Select(c => new PublicKeyCredentialDescriptor(
                                Convert.FromBase64String(c.id)
                            )),
                        ];
                    }
                }
            }

            var options = fido2.GetAssertionOptions(
                new GetAssertionOptionsParams
                {
                    AllowedCredentials = allowCredentials,
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
                    userId,
                    options.Challenge,
                    "authentication",
                    now,
                    challengeExpiry
                )
                .ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);

            return Results.Ok(new { ChallengeId = challengeId, Options = options });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login begin failed");
            return Results.Problem("Login failed");
        }
    }
);

authGroup.MapGet(
    "/session",
    async (HttpContext ctx, string connStr, JwtConfig jwtConfig) =>
    {
        var token = TokenService.ExtractBearerToken(ctx.Request.Headers.Authorization);
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = OpenConnection(connStr);

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
    async (HttpContext ctx, string connStr, JwtConfig jwtConfig) =>
    {
        var token = TokenService.ExtractBearerToken(ctx.Request.Headers.Authorization);
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = OpenConnection(connStr);

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
        string connStr,
        JwtConfig jwtConfig
    ) =>
    {
        var token = TokenService.ExtractBearerToken(ctx.Request.Headers.Authorization);
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = OpenConnection(connStr);

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
    async (HttpContext ctx, string connStr, JwtConfig jwtConfig) =>
    {
        var token = TokenService.ExtractBearerToken(ctx.Request.Headers.Authorization);
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = OpenConnection(connStr);

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
                    p.source,
                    p.source_name,
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
    async (EvaluateRequest request, HttpContext ctx, string connStr, JwtConfig jwtConfig) =>
    {
        var token = TokenService.ExtractBearerToken(ctx.Request.Headers.Authorization);
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = OpenConnection(connStr);

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
}
