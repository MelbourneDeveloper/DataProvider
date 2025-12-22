namespace Samples.Authorization;

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Static helper methods for authentication and authorization.
/// </summary>
public static class AuthHelpers
{
    private static readonly Action<ILogger, Exception?> LogTokenValidationFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, "TokenValidationFailed"),
            "Token validation failed"
        );

    /// <summary>
    /// Extracts the Bearer token from an Authorization header.
    /// </summary>
    /// <param name="authHeader">The Authorization header value.</param>
    /// <returns>The token if present, null otherwise.</returns>
    public static string? ExtractBearerToken(string? authHeader) =>
        authHeader?.StartsWith("Bearer ", StringComparison.Ordinal) == true
            ? authHeader["Bearer ".Length..]
            : null;

    /// <summary>
    /// Validates a JWT token locally without network calls.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <param name="signingKey">The HMAC-SHA256 signing key.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    /// <returns>AuthSuccess with claims or AuthFailure with reason.</returns>
    public static object ValidateTokenLocally(
        string token,
        ImmutableArray<byte> signingKey,
        ILogger? logger = null
    )
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return new AuthFailure("Invalid token format");
            }

            var keyArray = signingKey.ToArray();
            var expectedSignature = ComputeSignature(parts[0], parts[1], keyArray);
            if (
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expectedSignature),
                    Encoding.UTF8.GetBytes(parts[2])
                )
            )
            {
                return new AuthFailure("Invalid signature");
            }

            var payloadBytes = Base64UrlDecode(parts[1]);
            using var doc = JsonDocument.Parse(payloadBytes);
            var root = doc.RootElement;

            var exp = root.GetProperty("exp").GetInt64();
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
            {
                return new AuthFailure("Token expired");
            }

            var jti = root.GetProperty("jti").GetString() ?? string.Empty;

            var roles = root.TryGetProperty("roles", out var rolesElement)
                ? [.. rolesElement.EnumerateArray().Select(e => e.GetString() ?? string.Empty)]
                : ImmutableArray<string>.Empty;

            var claims = new AuthClaims(
                UserId: root.GetProperty("sub").GetString() ?? string.Empty,
                DisplayName: root.TryGetProperty("name", out var nameElem)
                    ? nameElem.GetString()
                    : null,
                Email: root.TryGetProperty("email", out var emailElem)
                    ? emailElem.GetString()
                    : null,
                Roles: roles,
                Jti: jti,
                ExpiresAt: exp
            );

            return new AuthSuccess(claims);
        }
        catch (Exception ex)
        {
            if (logger is not null)
            {
                LogTokenValidationFailed(logger, ex);
            }
            return new AuthFailure("Token validation failed");
        }
    }

    /// <summary>
    /// Checks permission via Gatekeeper API.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured for Gatekeeper.</param>
    /// <param name="token">The Bearer token for authorization.</param>
    /// <param name="permission">The permission code to check.</param>
    /// <param name="resourceType">Optional resource type for record-level access.</param>
    /// <param name="resourceId">Optional resource ID for record-level access.</param>
    /// <returns>PermissionResult indicating if access is allowed.</returns>
    public static async Task<PermissionResult> CheckPermissionAsync(
        HttpClient httpClient,
        string token,
        string permission,
        string? resourceType = null,
        string? resourceId = null
    )
    {
        try
        {
            var url = $"/authz/check?permission={Uri.EscapeDataString(permission)}";
            if (!string.IsNullOrEmpty(resourceType))
            {
                url += $"&resourceType={Uri.EscapeDataString(resourceType)}";
            }
            if (!string.IsNullOrEmpty(resourceId))
            {
                url += $"&resourceId={Uri.EscapeDataString(resourceId)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token
            );

            using var response = await httpClient.SendAsync(request).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new PermissionResult(false, $"Gatekeeper returned {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var allowed =
                root.TryGetProperty("Allowed", out var allowedElem) && allowedElem.GetBoolean();
            var reason = root.TryGetProperty("Reason", out var reasonElem)
                ? reasonElem.GetString() ?? "unknown"
                : "unknown";

            return new PermissionResult(allowed, reason);
        }
        catch (Exception ex)
        {
            return new PermissionResult(false, $"Permission check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates an IResult for unauthorized responses.
    /// </summary>
    /// <param name="reason">The reason for the unauthorized response.</param>
    /// <returns>A 401 Unauthorized result.</returns>
    public static IResult Unauthorized(string reason) =>
        Results.Json(new { Error = "Unauthorized", Reason = reason }, statusCode: 401);

    /// <summary>
    /// Creates an IResult for forbidden responses.
    /// </summary>
    /// <param name="reason">The reason for the forbidden response.</param>
    /// <returns>A 403 Forbidden result.</returns>
    public static IResult Forbidden(string reason) =>
        Results.Json(new { Error = "Forbidden", Reason = reason }, statusCode: 403);

    private static string Base64UrlEncode(byte[] input) =>
        Convert
            .ToBase64String(input)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input
            .Replace("-", "+", StringComparison.Ordinal)
            .Replace("_", "/", StringComparison.Ordinal);
        var padding = (4 - (padded.Length % 4)) % 4;
        padded += new string('=', padding);
        return Convert.FromBase64String(padded);
    }

    private static string ComputeSignature(string header, string payload, byte[] key)
    {
        var data = Encoding.UTF8.GetBytes($"{header}.{payload}");
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(data);
        return Base64UrlEncode(hash);
    }
}
