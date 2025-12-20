namespace Gatekeeper.Api;

using Microsoft.Data.Sqlite;
using Outcome;

/// <summary>
/// Service for evaluating authorization decisions.
/// </summary>
public static class AuthorizationService
{
    /// <summary>
    /// Checks if a user has a specific permission, optionally scoped to a resource.
    /// </summary>
    public static async Task<(bool Allowed, string Reason)> CheckPermissionAsync(
        SqliteConnection conn,
        string userId,
        string permissionCode,
        string? resourceType,
        string? resourceId,
        string now)
    {
        // Step 1: Check resource-level grants first (most specific)
        if (!string.IsNullOrEmpty(resourceType) && !string.IsNullOrEmpty(resourceId))
        {
            var grantResult = await conn.CheckResourceGrantAsync(
                userId,
                resourceType,
                resourceId,
                permissionCode,
                now
            ).ConfigureAwait(false);

            var hasGrant = grantResult switch
            {
                Result<ImmutableList<Generated.CheckResourceGrant>, SqlError>.Ok(var grants) when grants.Count > 0 => true,
                _ => false
            };

            if (hasGrant)
            {
                return (true, $"resource-grant:{resourceType}/{resourceId}");
            }
        }

        // Step 2: Check user permissions (direct grants and role-based)
        var permResult = await conn.GetUserPermissionsAsync(userId, now).ConfigureAwait(false);
        var permissions = permResult switch
        {
            Result<ImmutableList<Generated.GetUserPermissions>, SqlError>.Ok(var perms) => perms,
            _ => ImmutableList<Generated.GetUserPermissions>.Empty
        };

        foreach (var perm in permissions)
        {
            var matches = PermissionMatches(perm.Code, permissionCode);
            if (!matches)
            {
                continue;
            }

            // Check scope
            var scopeMatches = perm.ScopeType switch
            {
                null or "all" => true,
                "record" => perm.ScopeValue == resourceId,
                _ => false
            };

            if (scopeMatches)
            {
                var source = perm.Source switch
                {
                    "role" => $"role:{perm.SourceName}",
                    "direct" => "direct-grant",
                    _ => perm.Source
                };
                return (true, $"{source} grants {perm.Code}");
            }
        }

        return (false, "no matching permission");
    }

    /// <summary>
    /// Checks if a permission code matches a target, supporting wildcards.
    /// </summary>
    private static bool PermissionMatches(string grantedCode, string targetCode)
    {
        if (grantedCode == targetCode)
        {
            return true;
        }

        // Handle wildcards like "admin:*" matching "admin:users"
        if (grantedCode.EndsWith(":*", StringComparison.Ordinal))
        {
            var prefix = grantedCode[..^1]; // Remove "*"
            return targetCode.StartsWith(prefix, StringComparison.Ordinal);
        }

        // Handle global wildcard
        if (grantedCode == "*:*" || grantedCode == "*")
        {
            return true;
        }

        return false;
    }
}
