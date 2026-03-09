namespace Samples.Authorization;

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Helper for generating JWT tokens in tests.
/// </summary>
public static class TestTokenHelper
{
    /// <summary>
    /// A fixed 32-byte signing key for testing (base64: AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=).
    /// </summary>
    public static readonly ImmutableArray<byte> TestSigningKey = ImmutableArray.Create(
        new byte[32]
    );

    /// <summary>
    /// Generates a valid JWT token for testing purposes.
    /// </summary>
    /// <param name="userId">The user ID (sub claim).</param>
    /// <param name="roles">The roles to include in the token.</param>
    /// <param name="expiresInMinutes">Token expiration time in minutes from now.</param>
    /// <returns>A signed JWT token string.</returns>
    public static string GenerateToken(
        string userId,
        ImmutableArray<string> roles,
        int expiresInMinutes = 60
    )
    {
        var header = new { alg = "HS256", typ = "JWT" };
        var payload = new
        {
            sub = userId,
            jti = Guid.NewGuid().ToString(),
            roles,
            exp = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes).ToUnixTimeSeconds(),
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        var signature = ComputeSignature(headerBase64, payloadBase64, [.. TestSigningKey]);

        return $"{headerBase64}.{payloadBase64}.{signature}";
    }

    /// <summary>
    /// Generates a token for a clinician with full clinical permissions.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>A JWT token for a clinician.</returns>
    public static string GenerateClinicianToken(string userId = "test-clinician") =>
        GenerateToken(userId, ["clinician"]);

    /// <summary>
    /// Generates a token for a scheduler with scheduling permissions.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>A JWT token for a scheduler.</returns>
    public static string GenerateSchedulerToken(string userId = "test-scheduler") =>
        GenerateToken(userId, ["scheduler"]);

    /// <summary>
    /// Generates a token for a sync client with sync permissions.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>A JWT token for a sync client.</returns>
    public static string GenerateSyncClientToken(string userId = "test-sync-client") =>
        GenerateToken(userId, ["sync-client"]);

    /// <summary>
    /// Generates a token with no roles (authenticated but no permissions).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>A JWT token with no roles.</returns>
    public static string GenerateNoRoleToken(string userId = "test-user") =>
        GenerateToken(userId, []);

    /// <summary>
    /// Generates an expired token for testing expiration handling.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>An expired JWT token.</returns>
    public static string GenerateExpiredToken(string userId = "test-user") =>
        GenerateToken(userId, ["clinician"], expiresInMinutes: -60);

    private static string Base64UrlEncode(byte[] input) =>
        Convert
            .ToBase64String(input)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');

    private static string ComputeSignature(string header, string payload, byte[] key)
    {
        var data = Encoding.UTF8.GetBytes($"{header}.{payload}");
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(data);
        return Base64UrlEncode(hash);
    }
}
