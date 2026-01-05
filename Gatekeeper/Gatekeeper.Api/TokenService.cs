using System.Security.Cryptography;
using System.Text;

namespace Gatekeeper.Api;

/// <summary>
/// JWT token generation and validation service.
/// </summary>
public static class TokenService
{
    /// <summary>Token claims data.</summary>
    public sealed record TokenClaims(
        string UserId,
        string? DisplayName,
        string? Email,
        IReadOnlyList<string> Roles,
        string Jti,
        long Exp
    );

    /// <summary>Successful token validation result.</summary>
    public sealed record TokenValidationOk(TokenClaims Claims);

    /// <summary>Failed token validation result.</summary>
    public sealed record TokenValidationError(string Reason);

    /// <summary>
    /// Extracts the token from a Bearer authorization header.
    /// </summary>
    public static string? ExtractBearerToken(string? authHeader) =>
        authHeader?.StartsWith("Bearer ", StringComparison.Ordinal) == true
            ? authHeader["Bearer ".Length..]
            : null;

    /// <summary>
    /// Creates a JWT token for the given user.
    /// </summary>
    public static string CreateToken(
        string userId,
        string? displayName,
        string? email,
        IReadOnlyList<string> roles,
        byte[] signingKey,
        TimeSpan lifetime
    )
    {
        var now = DateTimeOffset.UtcNow;
        var exp = now.Add(lifetime);
        var jti = Guid.NewGuid().ToString();

        var header = Base64UrlEncode(
            JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" })
        );

        var payload = Base64UrlEncode(
            JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    sub = userId,
                    name = displayName,
                    email,
                    roles,
                    jti,
                    iat = now.ToUnixTimeSeconds(),
                    exp = exp.ToUnixTimeSeconds(),
                }
            )
        );

        var signature = ComputeSignature(header, payload, signingKey);
        return $"{header}.{payload}.{signature}";
    }

    /// <summary>
    /// Validates a JWT token.
    /// </summary>
    public static async Task<object> ValidateTokenAsync(
        SqliteConnection conn,
        string token,
        byte[] signingKey,
        bool checkRevocation,
        ILogger? logger = null
    )
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return new TokenValidationError("Invalid token format");
            }

            var expectedSignature = ComputeSignature(parts[0], parts[1], signingKey);
            if (
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expectedSignature),
                    Encoding.UTF8.GetBytes(parts[2])
                )
            )
            {
                return new TokenValidationError("Invalid signature");
            }

            var payloadBytes = Base64UrlDecode(parts[1]);
            using var doc = JsonDocument.Parse(payloadBytes);
            var root = doc.RootElement;

            var exp = root.GetProperty("exp").GetInt64();
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
            {
                return new TokenValidationError("Token expired");
            }

            var jti = root.GetProperty("jti").GetString() ?? string.Empty;

            if (checkRevocation)
            {
                var isRevoked = await IsTokenRevokedAsync(conn, jti).ConfigureAwait(false);
                if (isRevoked)
                {
                    return new TokenValidationError("Token revoked");
                }
            }

            var roles = root.TryGetProperty("roles", out var rolesElement)
                ? rolesElement.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList()
                : [];

            var claims = new TokenClaims(
                UserId: root.GetProperty("sub").GetString() ?? string.Empty,
                DisplayName: root.TryGetProperty("name", out var nameElem)
                    ? nameElem.GetString()
                    : null,
                Email: root.TryGetProperty("email", out var emailElem)
                    ? emailElem.GetString()
                    : null,
                Roles: roles,
                Jti: jti,
                Exp: exp
            );

            return new TokenValidationOk(claims);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Token validation failed");
            return new TokenValidationError("Token validation failed");
        }
    }

    /// <summary>
    /// Revokes a token by JTI using DataProvider generated method.
    /// </summary>
    public static async Task RevokeTokenAsync(SqliteConnection conn, string jti) =>
        _ = await conn.RevokeSessionAsync(jti).ConfigureAwait(false);

    private static async Task<bool> IsTokenRevokedAsync(SqliteConnection conn, string jti)
    {
        var result = await conn.GetSessionRevokedAsync(jti).ConfigureAwait(false);
        return result switch
        {
            GetSessionRevokedOk ok => ok.Value.FirstOrDefault()?.is_revoked == 1,
            GetSessionRevokedError => false,
        };
    }

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
