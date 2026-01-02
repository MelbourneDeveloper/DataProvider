namespace Samples.Authorization;

using System.Collections.Immutable;

/// <summary>
/// Claims extracted from a validated JWT token.
/// </summary>
/// <param name="UserId">The user's unique identifier.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="Roles">The roles assigned to the user.</param>
/// <param name="Jti">The JWT token ID.</param>
/// <param name="ExpiresAt">The Unix timestamp when the token expires.</param>
public sealed record AuthClaims(
    string UserId,
    string? DisplayName,
    string? Email,
    ImmutableArray<string> Roles,
    string Jti,
    long ExpiresAt
);

/// <summary>
/// Successful authentication with claims.
/// </summary>
/// <param name="Claims">The authenticated user's claims.</param>
public sealed record AuthSuccess(AuthClaims Claims);

/// <summary>
/// Failed authentication with reason.
/// </summary>
/// <param name="Reason">The reason for the failure.</param>
public sealed record AuthFailure(string Reason);

/// <summary>
/// Result of a permission check.
/// </summary>
/// <param name="Allowed">Whether the permission was granted.</param>
/// <param name="Reason">The reason for the decision.</param>
public sealed record PermissionResult(bool Allowed, string Reason);

/// <summary>
/// Configuration for authentication and authorization.
/// </summary>
/// <param name="SigningKey">The JWT signing key.</param>
/// <param name="GatekeeperBaseUrl">The base URL of the Gatekeeper service.</param>
public sealed record AuthConfig(ImmutableArray<byte> SigningKey, Uri GatekeeperBaseUrl);
