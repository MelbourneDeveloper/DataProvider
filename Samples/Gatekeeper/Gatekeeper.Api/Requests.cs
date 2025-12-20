namespace Gatekeeper.Api;

// ═══════════════════════════════════════════════════════════════
// AUTHENTICATION REQUESTS
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Request to begin passkey registration.
/// </summary>
public sealed record RegisterBeginRequest(
    string Email,
    string DisplayName);

/// <summary>
/// Request to complete passkey registration.
/// </summary>
public sealed record RegisterCompleteRequest(
    string ChallengeId,
    AuthenticatorAttestationRawResponse Response,
    string? DeviceName);

/// <summary>
/// Request to begin passkey login.
/// </summary>
public sealed record LoginBeginRequest(
    string? Email);

/// <summary>
/// Request to complete passkey login.
/// </summary>
public sealed record LoginCompleteRequest(
    string ChallengeId,
    AuthenticatorAssertionRawResponse Response);

// ═══════════════════════════════════════════════════════════════
// AUTHORIZATION REQUESTS
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Single permission check in bulk evaluation.
/// </summary>
public sealed record PermissionCheck(
    string Permission,
    string? ResourceId);

/// <summary>
/// Request for bulk permission evaluation.
/// </summary>
public sealed record EvaluateRequest(
    ImmutableArray<PermissionCheck> Checks);

// ═══════════════════════════════════════════════════════════════
// ADMIN REQUESTS
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Request to create a new user (generates registration invite).
/// </summary>
public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    ImmutableArray<string>? Roles);

/// <summary>
/// Request to update a user.
/// </summary>
public sealed record UpdateUserRequest(
    string? DisplayName,
    bool? IsActive);

/// <summary>
/// Request to create a new role.
/// </summary>
public sealed record CreateRoleRequest(
    string Name,
    string? Description,
    string? ParentRoleId);

/// <summary>
/// Request to update a role.
/// </summary>
public sealed record UpdateRoleRequest(
    string? Description);

/// <summary>
/// Request to create a new permission.
/// </summary>
public sealed record CreatePermissionRequest(
    string Code,
    string ResourceType,
    string Action,
    string? Description);

/// <summary>
/// Request to assign a role to a user.
/// </summary>
public sealed record AssignRoleRequest(
    string RoleId,
    string? ExpiresAt);

/// <summary>
/// Request for direct permission grant.
/// </summary>
public sealed record GrantPermissionRequest(
    string PermissionId,
    string? ScopeType,
    string? ScopeValue,
    string? ExpiresAt,
    string? Reason);

/// <summary>
/// Request for resource-level grant.
/// </summary>
public sealed record GrantResourceRequest(
    string ResourceType,
    string ResourceId,
    string PermissionId,
    string? ExpiresAt);
