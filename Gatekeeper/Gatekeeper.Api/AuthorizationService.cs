
using System.Text;

#pragma warning disable CS8509 // Non-exhaustive switch

namespace Gatekeeper.Api;
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
        string now
    )
    {
        // Step 1: Check resource-level grants first (most specific)
        if (!string.IsNullOrEmpty(resourceType) && !string.IsNullOrEmpty(resourceId))
        {
            // Use raw SQL instead of generated method due to non-deterministic param ordering bug
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = @"
                SELECT rg.id, rg.user_id, rg.resource_type, rg.resource_id, rg.permission_id,
                       rg.granted_at, rg.granted_by, rg.expires_at, p.code as permission_code
                FROM gk_resource_grant rg
                JOIN gk_permission p ON rg.permission_id = p.id
                WHERE rg.user_id = @user_id
                  AND rg.resource_type = @resource_type
                  AND rg.resource_id = @resource_id
                  AND p.code = @permission_code
                  AND (rg.expires_at IS NULL OR rg.expires_at > @now)";
            checkCmd.Parameters.AddWithValue("@user_id", userId);
            checkCmd.Parameters.AddWithValue("@resource_type", resourceType);
            checkCmd.Parameters.AddWithValue("@resource_id", resourceId);
            checkCmd.Parameters.AddWithValue("@permission_code", permissionCode);
            checkCmd.Parameters.AddWithValue("@now", now);

            using var checkReader = await checkCmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (await checkReader.ReadAsync().ConfigureAwait(false))
            {
                return (true, $"resource-grant:{resourceType}/{resourceId}");
            }
        }

        // Step 2: Check user permissions (direct grants and role-based)
        var permResult = await conn.GetUserPermissionsAsync(userId, now).ConfigureAwait(false);
        var permissions = permResult is GetUserPermissionsOk ok ? ok.Value : [];

        foreach (var perm in permissions)
        {
            var matches = PermissionMatches(perm.code, permissionCode);
            if (!matches)
            {
                continue;
            }

            // Check scope - handle both string and byte[] types from generated code
            var scopeType = ToStringValue(perm.scope_type);
            var scopeValue = ToStringValue(perm.scope_value);

            var scopeMatches = scopeType switch
            {
                null or "" or "all" => true,
                "record" => scopeValue == resourceId,
                _ => false,
            };

            if (scopeMatches)
            {
                // source_type is role_id for role-based permissions, permission_id for direct grants
                // source_name is role name for role-based, permission code for direct
                var source =
                    perm.source_name != perm.code ? $"role:{perm.source_name}" : "direct-grant";
                return (true, $"{source} grants {perm.code}");
            }
        }

        return (false, "no matching permission");
    }

    /// <summary>
    /// Converts a value to string, handling byte[] from SQLite.
    /// </summary>
    private static string? ToStringValue(object? value) =>
        value switch
        {
            null => null,
            string s => s,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            _ => value.ToString(),
        };

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
