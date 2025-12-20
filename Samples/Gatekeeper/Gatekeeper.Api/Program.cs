#pragma warning disable CS8509 // Exhaustive switch
#pragma warning disable IDE0037 // Use inferred member name

using System.Security.Cryptography;
using System.Text;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Gatekeeper.Api;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Dashboard",
        policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
    );
});

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

var dbPath = builder.Configuration["DbPath"] ?? Path.Combine(AppContext.BaseDirectory, "gatekeeper.db");
var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    ForeignKeys = true,
}.ToString();

builder.Services.AddSingleton(() =>
{
    var conn = new SqliteConnection(connectionString);
    conn.Open();
    return conn;
});

var app = builder.Build();

using (var conn = new SqliteConnection(connectionString))
{
    conn.Open();
    DatabaseSetup.Initialize(conn, app.Logger);
}

app.UseCors("Dashboard");

// ═══════════════════════════════════════════════════════════════════════════
// AUTHENTICATION ENDPOINTS
// ═══════════════════════════════════════════════════════════════════════════

var authGroup = app.MapGroup("/auth").WithTags("Authentication");

authGroup.MapPost(
    "/register/begin",
    async (RegisterBeginRequest request, IFido2 fido2, Func<SqliteConnection> getConn) =>
    {
        try
        {
            using var conn = getConn();
            var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            var existingUser = await conn.GetUserByEmailAsync(request.Email).ConfigureAwait(false);
            string userId;
            var isNewUser = existingUser switch
            {
                Result<ImmutableList<Generated.GetUserByEmail>, SqlError>.Ok(var users) when users.Count > 0 => false,
                _ => true
            };

            userId = isNewUser switch
            {
                true => Guid.NewGuid().ToString(),
                false => ((Result<ImmutableList<Generated.GetUserByEmail>, SqlError>.Ok<ImmutableList<Generated.GetUserByEmail>, SqlError>)existingUser).Value[0].Id
            };

            if (isNewUser)
            {
                await conn.InsertUserAsync(userId, request.DisplayName, request.Email, now, null).ConfigureAwait(false);
            }

            var existingCredentials = await conn.GetUserCredentialsAsync(userId).ConfigureAwait(false);
            var excludeCredentials = existingCredentials switch
            {
                Result<ImmutableList<Generated.GetUserCredentials>, SqlError>.Ok(var creds) =>
                    creds.Select(c => new PublicKeyCredentialDescriptor(Convert.FromBase64String(c.Id))).ToList(),
                _ => new List<PublicKeyCredentialDescriptor>()
            };

            var user = new Fido2User
            {
                Id = Encoding.UTF8.GetBytes(userId),
                Name = request.Email,
                DisplayName = request.DisplayName
            };

            var authenticatorSelection = new AuthenticatorSelection
            {
                AuthenticatorAttachment = AuthenticatorAttachment.Platform,
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Required
            };

            var options = fido2.RequestNewCredential(
                user,
                excludeCredentials,
                authenticatorSelection,
                AttestationConveyancePreference.None
            );

            var challengeId = Guid.NewGuid().ToString();
            var challengeExpiry = DateTime.UtcNow.AddMinutes(5).ToString("o", CultureInfo.InvariantCulture);

            await conn.InsertChallengeAsync(
                challengeId,
                userId,
                options.Challenge,
                "registration",
                now,
                challengeExpiry
            ).ConfigureAwait(false);

            return Results.Ok(new { ChallengeId = challengeId, Options = options });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
);

authGroup.MapPost(
    "/register/complete",
    async (RegisterCompleteRequest request, IFido2 fido2, Func<SqliteConnection> getConn) =>
    {
        try
        {
            using var conn = getConn();
            var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            var challengeResult = await conn.GetChallengeByIdAsync(request.ChallengeId, now).ConfigureAwait(false);
            var challenge = challengeResult switch
            {
                Result<ImmutableList<Generated.GetChallengeById>, SqlError>.Ok(var challenges) when challenges.Count > 0 => challenges[0],
                _ => null
            };

            if (challenge == null)
            {
                return Results.BadRequest(new { Error = "Invalid or expired challenge" });
            }

            var options = new CredentialCreateOptions
            {
                Challenge = challenge.Challenge,
                Rp = new PublicKeyCredentialRpEntity(fido2.Config.ServerDomain, fido2.Config.ServerName, null),
                User = new Fido2User
                {
                    Id = Encoding.UTF8.GetBytes(challenge.UserId ?? ""),
                    Name = "",
                    DisplayName = ""
                }
            };

            var credential = await fido2.MakeNewCredentialAsync(
                request.Response,
                options,
                (args, ct) => Task.FromResult(true)
            ).ConfigureAwait(false);

            if (credential.Result == null)
            {
                return Results.BadRequest(new { Error = "Credential creation failed" });
            }

            var credentialId = Convert.ToBase64String(credential.Result.Id);
            var publicKey = credential.Result.PublicKey;
            var signCount = (long)credential.Result.SignCount;
            var aaguid = credential.Result.AaGuid.ToString();
            var transports = credential.Result.Transports != null
                ? JsonSerializer.Serialize(credential.Result.Transports)
                : null;

            await conn.InsertCredentialAsync(
                credentialId,
                challenge.UserId ?? "",
                publicKey,
                signCount,
                aaguid,
                "public-key",
                transports,
                credential.Result.AttestationFormat,
                now,
                request.DeviceName,
                credential.Result.IsBackupEligible ? 1L : 0L,
                credential.Result.IsBackedUp ? 1L : 0L
            ).ConfigureAwait(false);

            await conn.DeleteChallengeAsync(request.ChallengeId).ConfigureAwait(false);

            var sessionId = Guid.NewGuid().ToString();
            var sessionExpiry = DateTime.UtcNow.AddHours(24).ToString("o", CultureInfo.InvariantCulture);

            await conn.InsertSessionAsync(
                sessionId,
                challenge.UserId ?? "",
                credentialId,
                now,
                sessionExpiry,
                now,
                null,
                null
            ).ConfigureAwait(false);

            await conn.UpdateUserLastLoginAsync(challenge.UserId ?? "", now).ConfigureAwait(false);

            return Results.Ok(new
            {
                UserId = challenge.UserId,
                CredentialId = credentialId,
                Session = new { Token = sessionId, ExpiresAt = sessionExpiry }
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
);

authGroup.MapPost(
    "/login/begin",
    async (LoginBeginRequest? request, IFido2 fido2, Func<SqliteConnection> getConn) =>
    {
        try
        {
            using var conn = getConn();
            var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            var allowCredentials = new List<PublicKeyCredentialDescriptor>();
            string? userId = null;

            if (!string.IsNullOrEmpty(request?.Email))
            {
                var userResult = await conn.GetUserByEmailAsync(request.Email).ConfigureAwait(false);
                var user = userResult switch
                {
                    Result<ImmutableList<Generated.GetUserByEmail>, SqlError>.Ok(var users) when users.Count > 0 => users[0],
                    _ => null
                };

                if (user != null)
                {
                    userId = user.Id;
                    var credResult = await conn.GetUserCredentialsAsync(user.Id).ConfigureAwait(false);
                    allowCredentials = credResult switch
                    {
                        Result<ImmutableList<Generated.GetUserCredentials>, SqlError>.Ok(var creds) =>
                            creds.Select(c => new PublicKeyCredentialDescriptor(Convert.FromBase64String(c.Id))).ToList(),
                        _ => new List<PublicKeyCredentialDescriptor>()
                    };
                }
            }

            var options = fido2.GetAssertionOptions(
                allowCredentials,
                UserVerificationRequirement.Required
            );

            var challengeId = Guid.NewGuid().ToString();
            var challengeExpiry = DateTime.UtcNow.AddMinutes(5).ToString("o", CultureInfo.InvariantCulture);

            await conn.InsertChallengeAsync(
                challengeId,
                userId,
                options.Challenge,
                "authentication",
                now,
                challengeExpiry
            ).ConfigureAwait(false);

            return Results.Ok(new { ChallengeId = challengeId, Options = options });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
);

authGroup.MapPost(
    "/login/complete",
    async (LoginCompleteRequest request, IFido2 fido2, Func<SqliteConnection> getConn, HttpContext ctx) =>
    {
        try
        {
            using var conn = getConn();
            var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            var challengeResult = await conn.GetChallengeByIdAsync(request.ChallengeId, now).ConfigureAwait(false);
            var challenge = challengeResult switch
            {
                Result<ImmutableList<Generated.GetChallengeById>, SqlError>.Ok(var challenges) when challenges.Count > 0 => challenges[0],
                _ => null
            };

            if (challenge == null)
            {
                return Results.BadRequest(new { Error = "Invalid or expired challenge" });
            }

            var credentialId = Convert.ToBase64String(request.Response.Id);
            var credResult = await conn.GetCredentialByIdAsync(credentialId).ConfigureAwait(false);
            var storedCredential = credResult switch
            {
                Result<ImmutableList<Generated.GetCredentialById>, SqlError>.Ok(var creds) when creds.Count > 0 => creds[0],
                _ => null
            };

            if (storedCredential == null)
            {
                return Results.BadRequest(new { Error = "Credential not found" });
            }

            var options = new AssertionOptions
            {
                Challenge = challenge.Challenge,
                RpId = fido2.Config.ServerDomain
            };

            var result = await fido2.MakeAssertionAsync(
                request.Response,
                options,
                storedCredential.PublicKey,
                [],
                (uint)storedCredential.SignCount,
                (args, ct) => Task.FromResult(true)
            ).ConfigureAwait(false);

            if (result.Status != "ok")
            {
                return Results.BadRequest(new { Error = result.ErrorMessage });
            }

            await conn.UpdateCredentialSignCountAsync(
                credentialId,
                (long)result.SignCount,
                now
            ).ConfigureAwait(false);

            await conn.DeleteChallengeAsync(request.ChallengeId).ConfigureAwait(false);

            var sessionId = Guid.NewGuid().ToString();
            var sessionExpiry = DateTime.UtcNow.AddHours(24).ToString("o", CultureInfo.InvariantCulture);

            await conn.InsertSessionAsync(
                sessionId,
                storedCredential.UserId,
                credentialId,
                now,
                sessionExpiry,
                now,
                ctx.Connection.RemoteIpAddress?.ToString(),
                ctx.Request.Headers.UserAgent.ToString()
            ).ConfigureAwait(false);

            await conn.UpdateUserLastLoginAsync(storedCredential.UserId, now).ConfigureAwait(false);

            return Results.Ok(new
            {
                UserId = storedCredential.UserId,
                DisplayName = storedCredential.DisplayName,
                Session = new { Token = sessionId, ExpiresAt = sessionExpiry }
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
);

authGroup.MapGet(
    "/session",
    async (HttpContext ctx, Func<SqliteConnection> getConn) =>
    {
        var token = ctx.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = getConn();
        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        var sessionResult = await conn.GetSessionByIdAsync(token, now).ConfigureAwait(false);
        var session = sessionResult switch
        {
            Result<ImmutableList<Generated.GetSessionById>, SqlError>.Ok(var sessions) when sessions.Count > 0 => sessions[0],
            _ => null
        };

        if (session == null)
        {
            return Results.Unauthorized();
        }

        var rolesResult = await conn.GetUserRolesAsync(session.UserId, now).ConfigureAwait(false);
        var roles = rolesResult switch
        {
            Result<ImmutableList<Generated.GetUserRoles>, SqlError>.Ok(var r) => r.Select(x => x.Name).ToList(),
            _ => new List<string>()
        };

        return Results.Ok(new
        {
            UserId = session.UserId,
            DisplayName = session.DisplayName,
            Email = session.Email,
            Roles = roles,
            ExpiresAt = session.ExpiresAt
        });
    }
);

authGroup.MapPost(
    "/logout",
    async (HttpContext ctx, Func<SqliteConnection> getConn) =>
    {
        var token = ctx.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = getConn();
        await conn.RevokeSessionAsync(token).ConfigureAwait(false);

        return Results.NoContent();
    }
);

// ═══════════════════════════════════════════════════════════════════════════
// AUTHORIZATION ENDPOINTS
// ═══════════════════════════════════════════════════════════════════════════

var authzGroup = app.MapGroup("/authz").WithTags("Authorization");

authzGroup.MapGet(
    "/check",
    async (string permission, string? resourceType, string? resourceId, HttpContext ctx, Func<SqliteConnection> getConn) =>
    {
        var token = ctx.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = getConn();
        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        var sessionResult = await conn.GetSessionByIdAsync(token, now).ConfigureAwait(false);
        var session = sessionResult switch
        {
            Result<ImmutableList<Generated.GetSessionById>, SqlError>.Ok(var sessions) when sessions.Count > 0 => sessions[0],
            _ => null
        };

        if (session == null)
        {
            return Results.Unauthorized();
        }

        var (allowed, reason) = await AuthorizationService.CheckPermissionAsync(
            conn,
            session.UserId,
            permission,
            resourceType,
            resourceId,
            now
        ).ConfigureAwait(false);

        return Results.Ok(new { Allowed = allowed, Reason = reason });
    }
);

authzGroup.MapGet(
    "/permissions",
    async (HttpContext ctx, Func<SqliteConnection> getConn) =>
    {
        var token = ctx.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = getConn();
        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        var sessionResult = await conn.GetSessionByIdAsync(token, now).ConfigureAwait(false);
        var session = sessionResult switch
        {
            Result<ImmutableList<Generated.GetSessionById>, SqlError>.Ok(var sessions) when sessions.Count > 0 => sessions[0],
            _ => null
        };

        if (session == null)
        {
            return Results.Unauthorized();
        }

        var permissionsResult = await conn.GetUserPermissionsAsync(session.UserId, now).ConfigureAwait(false);
        var permissions = permissionsResult switch
        {
            Result<ImmutableList<Generated.GetUserPermissions>, SqlError>.Ok(var perms) => perms.Select(p => new
            {
                Code = p.Code,
                Source = p.Source,
                SourceName = p.SourceName,
                ScopeType = p.ScopeType,
                ScopeValue = p.ScopeValue
            }).ToList(),
            _ => new List<object>()
        };

        return Results.Ok(new { Permissions = permissions });
    }
);

authzGroup.MapPost(
    "/evaluate",
    async (EvaluateRequest request, HttpContext ctx, Func<SqliteConnection> getConn) =>
    {
        var token = ctx.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        using var conn = getConn();
        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        var sessionResult = await conn.GetSessionByIdAsync(token, now).ConfigureAwait(false);
        var session = sessionResult switch
        {
            Result<ImmutableList<Generated.GetSessionById>, SqlError>.Ok(var sessions) when sessions.Count > 0 => sessions[0],
            _ => null
        };

        if (session == null)
        {
            return Results.Unauthorized();
        }

        var results = new List<object>();
        foreach (var check in request.Checks)
        {
            var (allowed, _) = await AuthorizationService.CheckPermissionAsync(
                conn,
                session.UserId,
                check.Permission,
                check.ResourceType,
                check.ResourceId,
                now
            ).ConfigureAwait(false);

            results.Add(new
            {
                Permission = check.Permission,
                ResourceId = check.ResourceId,
                Allowed = allowed
            });
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

    /// <summary>
    /// Request to begin passkey registration.
    /// </summary>
    public sealed record RegisterBeginRequest(string Email, string DisplayName);

    /// <summary>
    /// Request to complete passkey registration.
    /// </summary>
    public sealed record RegisterCompleteRequest(
        string ChallengeId,
        AuthenticatorAttestationRawResponse Response,
        string? DeviceName
    );

    /// <summary>
    /// Request to begin passkey login.
    /// </summary>
    public sealed record LoginBeginRequest(string? Email);

    /// <summary>
    /// Request to complete passkey login.
    /// </summary>
    public sealed record LoginCompleteRequest(string ChallengeId, AuthenticatorAssertionRawResponse Response);

    /// <summary>
    /// Request to evaluate multiple permissions.
    /// </summary>
    public sealed record EvaluateRequest(List<PermissionCheck> Checks);

    /// <summary>
    /// Single permission check.
    /// </summary>
    public sealed record PermissionCheck(string Permission, string? ResourceType, string? ResourceId);
}
