#pragma warning disable CS8509 // Exhaustive switch
#pragma warning disable IDE0037 // Use inferred member name

using System.Data;
using System.Text;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Gatekeeper.Api;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(options =>
    options.SerializerOptions.PropertyNamingPolicy = null);

builder.Services.AddCors(options =>
    options.AddPolicy("Dashboard", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var serverDomain = builder.Configuration["Fido2:ServerDomain"] ?? "localhost";
var serverName = builder.Configuration["Fido2:ServerName"] ?? "Gatekeeper";
var origin = builder.Configuration["Fido2:Origin"] ?? "http://localhost:5173";

builder.Services.AddFido2(options =>
{
    options.ServerDomain = serverDomain;
    options.ServerName = serverName;
    options.Origins = [origin];
    options.TimestampDriftTolerance = 300000;
});

var dbPath = builder.Configuration["DbPath"] ?? Path.Combine(AppContext.BaseDirectory, "gatekeeper.db");
var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath, ForeignKeys = true }.ToString();

builder.Services.AddSingleton(connectionString);

var app = builder.Build();

using (var conn = new SqliteConnection(connectionString))
{
    conn.Open();
    DatabaseSetup.Initialize(conn, app.Logger);
}

app.UseCors("Dashboard");

// ═══════════════════════════════════════════════════════════════════════════
// HELPER FUNCTIONS
// ═══════════════════════════════════════════════════════════════════════════

static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

static SqliteConnection OpenConnection(string connStr)
{
    var conn = new SqliteConnection(connStr);
    conn.Open();
    return conn;
}

// ═══════════════════════════════════════════════════════════════════════════
// AUTHENTICATION ENDPOINTS
// ═══════════════════════════════════════════════════════════════════════════

var authGroup = app.MapGroup("/auth").WithTags("Authentication");

authGroup.MapPost("/register/begin", async (RegisterBeginRequest request, IFido2 fido2, string connStr) =>
{
    try
    {
        using var conn = OpenConnection(connStr);
        var now = Now();

        var existingUser = await conn.GetUserByEmailAsync(request.Email).ConfigureAwait(false);
        var isNewUser = existingUser is not GetUserByEmailOk { Value.Count: > 0 };
        var userId = isNewUser
            ? Guid.NewGuid().ToString()
            : ((GetUserByEmailOk)existingUser).Value[0].Id;

        if (isNewUser)
        {
            using var tx = conn.BeginTransaction();
            await tx.Insertgk_userAsync(userId, request.DisplayName, request.Email, now, null, 1, null).ConfigureAwait(false);
            tx.Commit();
        }

        var existingCredentials = await conn.GetUserCredentialsAsync(userId).ConfigureAwait(false);
        var excludeCredentials = existingCredentials switch
        {
            GetUserCredentialsOk ok => ok.Value.Select(c => new PublicKeyCredentialDescriptor(Convert.FromBase64String(c.Id))).ToList(),
            _ => []
        };

        var user = new Fido2User { Id = Encoding.UTF8.GetBytes(userId), Name = request.Email, DisplayName = request.DisplayName };
        var authSelector = new AuthenticatorSelection
        {
            AuthenticatorAttachment = AuthenticatorAttachment.Platform,
            ResidentKey = ResidentKeyRequirement.Required,
            UserVerification = UserVerificationRequirement.Required
        };

        var options = fido2.RequestNewCredential(user, excludeCredentials, authSelector, AttestationConveyancePreference.None);
        var challengeId = Guid.NewGuid().ToString();
        var challengeExpiry = DateTime.UtcNow.AddMinutes(5).ToString("o", CultureInfo.InvariantCulture);

        using var tx2 = conn.BeginTransaction();
        await tx2.Insertgk_challengeAsync(challengeId, userId, options.Challenge, "registration", now, challengeExpiry).ConfigureAwait(false);
        tx2.Commit();

        return Results.Ok(new { ChallengeId = challengeId, Options = options });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

authGroup.MapPost("/login/begin", async (LoginBeginRequest? request, IFido2 fido2, string connStr) =>
{
    try
    {
        using var conn = OpenConnection(connStr);
        var now = Now();
        string? userId = null;
        var allowCredentials = new List<PublicKeyCredentialDescriptor>();

        if (!string.IsNullOrEmpty(request?.Email))
        {
            var userResult = await conn.GetUserByEmailAsync(request.Email).ConfigureAwait(false);
            if (userResult is GetUserByEmailOk { Value.Count: > 0 } ok)
            {
                userId = ok.Value[0].Id;
                var credResult = await conn.GetUserCredentialsAsync(userId).ConfigureAwait(false);
                if (credResult is GetUserCredentialsOk credOk)
                {
                    allowCredentials = credOk.Value.Select(c => new PublicKeyCredentialDescriptor(Convert.FromBase64String(c.Id))).ToList();
                }
            }
        }

        var options = fido2.GetAssertionOptions(allowCredentials, UserVerificationRequirement.Required);
        var challengeId = Guid.NewGuid().ToString();
        var challengeExpiry = DateTime.UtcNow.AddMinutes(5).ToString("o", CultureInfo.InvariantCulture);

        using var tx = conn.BeginTransaction();
        await tx.Insertgk_challengeAsync(challengeId, userId, options.Challenge, "authentication", now, challengeExpiry).ConfigureAwait(false);
        tx.Commit();

        return Results.Ok(new { ChallengeId = challengeId, Options = options });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

authGroup.MapGet("/session", async (HttpContext ctx, string connStr) =>
{
    var token = ctx.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

    using var conn = OpenConnection(connStr);
    var now = Now();

    var sessionResult = await conn.GetSessionByIdAsync(token, now).ConfigureAwait(false);
    if (sessionResult is not GetSessionByIdOk { Value.Count: > 0 } sessionOk) return Results.Unauthorized();

    var session = sessionOk.Value[0];
    var rolesResult = await conn.GetUserRolesAsync(session.UserId, now).ConfigureAwait(false);
    var roles = rolesResult is GetUserRolesOk rolesOk ? rolesOk.Value.Select(x => x.Name).ToList() : [];

    return Results.Ok(new { session.UserId, session.DisplayName, session.Email, Roles = roles, session.ExpiresAt });
});

authGroup.MapPost("/logout", async (HttpContext ctx, string connStr) =>
{
    var token = ctx.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

    using var conn = OpenConnection(connStr);
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE gk_session SET is_revoked = 1 WHERE id = @id";
    cmd.Parameters.AddWithValue("@id", token);
    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

    return Results.NoContent();
});

// ═══════════════════════════════════════════════════════════════════════════
// AUTHORIZATION ENDPOINTS
// ═══════════════════════════════════════════════════════════════════════════

var authzGroup = app.MapGroup("/authz").WithTags("Authorization");

authzGroup.MapGet("/check", async (string permission, string? resourceType, string? resourceId, HttpContext ctx, string connStr) =>
{
    var token = ctx.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

    using var conn = OpenConnection(connStr);
    var now = Now();

    var sessionResult = await conn.GetSessionByIdAsync(token, now).ConfigureAwait(false);
    if (sessionResult is not GetSessionByIdOk { Value.Count: > 0 } sessionOk) return Results.Unauthorized();

    var (allowed, reason) = await AuthorizationService.CheckPermissionAsync(conn, sessionOk.Value[0].UserId, permission, resourceType, resourceId, now).ConfigureAwait(false);
    return Results.Ok(new { Allowed = allowed, Reason = reason });
});

authzGroup.MapGet("/permissions", async (HttpContext ctx, string connStr) =>
{
    var token = ctx.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

    using var conn = OpenConnection(connStr);
    var now = Now();

    var sessionResult = await conn.GetSessionByIdAsync(token, now).ConfigureAwait(false);
    if (sessionResult is not GetSessionByIdOk { Value.Count: > 0 } sessionOk) return Results.Unauthorized();

    var permissionsResult = await conn.GetUserPermissionsAsync(sessionOk.Value[0].UserId, now).ConfigureAwait(false);
    var permissions = permissionsResult is GetUserPermissionsOk permOk
        ? permOk.Value.Select(p => new { p.Code, p.Source, p.SourceName, p.ScopeType, p.ScopeValue }).ToList()
        : [];

    return Results.Ok(new { Permissions = permissions });
});

authzGroup.MapPost("/evaluate", async (EvaluateRequest request, HttpContext ctx, string connStr) =>
{
    var token = ctx.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
    if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

    using var conn = OpenConnection(connStr);
    var now = Now();

    var sessionResult = await conn.GetSessionByIdAsync(token, now).ConfigureAwait(false);
    if (sessionResult is not GetSessionByIdOk { Value.Count: > 0 } sessionOk) return Results.Unauthorized();

    var results = new List<object>();
    foreach (var check in request.Checks)
    {
        var (allowed, _) = await AuthorizationService.CheckPermissionAsync(conn, sessionOk.Value[0].UserId, check.Permission, check.ResourceType, check.ResourceId, now).ConfigureAwait(false);
        results.Add(new { check.Permission, check.ResourceId, Allowed = allowed });
    }

    return Results.Ok(new { Results = results });
});

app.Run();

namespace Gatekeeper.Api
{
    /// <summary>
    /// Program entry point marker for WebApplicationFactory.
    /// </summary>
    public partial class Program { }

    /// <summary>Request to begin passkey registration.</summary>
    public sealed record RegisterBeginRequest(string Email, string DisplayName);

    /// <summary>Request to begin passkey login.</summary>
    public sealed record LoginBeginRequest(string? Email);

    /// <summary>Request to evaluate multiple permissions.</summary>
    public sealed record EvaluateRequest(List<PermissionCheck> Checks);

    /// <summary>Single permission check.</summary>
    public sealed record PermissionCheck(string Permission, string? ResourceType, string? ResourceId);
}
