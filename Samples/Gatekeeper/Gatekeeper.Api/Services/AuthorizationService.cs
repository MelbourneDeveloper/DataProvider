namespace Gatekeeper.Api.Services;

/// <summary>
/// RBAC authorization service. Evaluates permissions based on roles and direct grants.
/// </summary>
public static class AuthorizationService
{
    /// <summary>
    /// Result of a permission check.
    /// </summary>
    public sealed record AuthzResult(
        bool Allowed,
        string? Reason,
        ImmutableArray<string> EvaluatedSources);

    /// <summary>
    /// Effective permission for a user.
    /// </summary>
    public sealed record EffectivePermission(
        string Code,
        string ResourceType,
        string Action,
        string Source,
        string? SourceName,
        string ScopeType,
        string? ScopeValue);

    /// <summary>
    /// Checks if user has a specific permission.
    /// </summary>
    public static async Task<Result<AuthzResult, string>> CheckPermissionAsync(
        SqliteConnection conn,
        string userId,
        string permissionCode,
        string? resourceType,
        string? resourceId,
        ILogger logger)
    {
        try
        {
            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var sources = new List<string>();

            // 1. Check for admin:* permission (superuser)
            var adminCheck = await conn.CheckPermissionAsync(userId, "admin:*", now).ConfigureAwait(false);
            if (adminCheck is CheckPermissionOk(var adminResults) && adminResults.Count > 0)
            {
                sources.Add("admin:* (superuser)");
                logger.Log(LogLevel.Debug, "User {UserId} granted {Permission} via admin:*", userId, permissionCode);
                return new Result<AuthzResult, string>.Ok(new AuthzResult(
                    Allowed: true,
                    Reason: "Superuser access via admin:* permission",
                    EvaluatedSources: [.. sources]));
            }

            // 2. Check specific permission via roles or direct grant
            var permCheck = await conn.CheckPermissionAsync(userId, permissionCode, now).ConfigureAwait(false);
            if (permCheck is CheckPermissionOk(var permResults) && permResults.Count > 0)
            {
                sources.Add($"permission:{permissionCode}");
                logger.Log(LogLevel.Debug, "User {UserId} has permission {Permission}", userId, permissionCode);

                // If resource-specific check requested, verify resource grant
                if (!string.IsNullOrEmpty(resourceType) && !string.IsNullOrEmpty(resourceId))
                {
                    var resourceCheck = await conn.CheckResourceGrantAsync(
                        userId, resourceType, resourceId, permissionCode, now).ConfigureAwait(false);

                    if (resourceCheck is CheckResourceGrantOk(var resourceResults) && resourceResults.Count > 0)
                    {
                        sources.Add($"resource:{resourceType}/{resourceId}");
                        return new Result<AuthzResult, string>.Ok(new AuthzResult(
                            Allowed: true,
                            Reason: $"Resource-level grant for {resourceType}/{resourceId}",
                            EvaluatedSources: [.. sources]));
                    }

                    // Has permission but no resource grant - check if permission is global scope
                    var permsResult = await conn.GetUserPermissionsAsync(userId, now).ConfigureAwait(false);
                    if (permsResult is GetUserPermissionsOk(var perms))
                    {
                        var matchingPerm = perms.FirstOrDefault(p =>
                            p.Code == permissionCode && p.ScopeType == "all");
                        if (matchingPerm != null)
                        {
                            return new Result<AuthzResult, string>.Ok(new AuthzResult(
                                Allowed: true,
                                Reason: $"Global permission {permissionCode}",
                                EvaluatedSources: [.. sources]));
                        }
                    }

                    // Permission exists but not for this specific resource
                    return new Result<AuthzResult, string>.Ok(new AuthzResult(
                        Allowed: false,
                        Reason: $"No grant for resource {resourceType}/{resourceId}",
                        EvaluatedSources: [.. sources]));
                }

                return new Result<AuthzResult, string>.Ok(new AuthzResult(
                    Allowed: true,
                    Reason: $"Permission {permissionCode} granted",
                    EvaluatedSources: [.. sources]));
            }

            // 3. Default deny
            logger.Log(LogLevel.Debug, "User {UserId} denied {Permission}", userId, permissionCode);
            return new Result<AuthzResult, string>.Ok(new AuthzResult(
                Allowed: false,
                Reason: "Permission not granted",
                EvaluatedSources: [.. sources]));
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Error checking permission");
            return new Result<AuthzResult, string>.Error(ex.Message);
        }
    }

    /// <summary>
    /// Gets all effective permissions for a user.
    /// </summary>
    public static async Task<Result<ImmutableArray<EffectivePermission>, string>> GetUserPermissionsAsync(
        SqliteConnection conn,
        string userId,
        ILogger logger)
    {
        try
        {
            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            var result = await conn.GetUserPermissionsAsync(userId, now).ConfigureAwait(false);
            if (result is not GetUserPermissionsOk(var perms))
            {
                return new Result<ImmutableArray<EffectivePermission>, string>.Error("Failed to get permissions");
            }

            var effective = perms.Select(p => new EffectivePermission(
                Code: p.Code,
                ResourceType: p.ResourceType,
                Action: p.Action,
                Source: p.Source,
                SourceName: p.SourceName,
                ScopeType: p.ScopeType ?? "all",
                ScopeValue: p.ScopeValue)).ToImmutableArray();

            return new Result<ImmutableArray<EffectivePermission>, string>.Ok(effective);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Error getting user permissions");
            return new Result<ImmutableArray<EffectivePermission>, string>.Error(ex.Message);
        }
    }

    /// <summary>
    /// Bulk permission check for multiple permissions at once.
    /// </summary>
    public static async Task<Result<ImmutableArray<(string Permission, string? ResourceId, bool Allowed)>, string>> EvaluateBulkAsync(
        SqliteConnection conn,
        string userId,
        ImmutableArray<(string Permission, string? ResourceId)> checks,
        ILogger logger)
    {
        try
        {
            var results = new List<(string Permission, string? ResourceId, bool Allowed)>();

            foreach (var (permission, resourceId) in checks)
            {
                // Extract resource type from permission code (e.g., "patient:read" -> "patient")
                var resourceType = permission.Contains(':')
                    ? permission.Split(':')[0]
                    : null;

                var checkResult = await CheckPermissionAsync(
                    conn, userId, permission, resourceType, resourceId, logger).ConfigureAwait(false);

                var allowed = checkResult is Result<AuthzResult, string>.Ok(var authz) && authz.Allowed;
                results.Add((permission, resourceId, allowed));
            }

            return new Result<ImmutableArray<(string Permission, string? ResourceId, bool Allowed)>, string>.Ok(
                [.. results]);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Error in bulk permission check");
            return new Result<ImmutableArray<(string Permission, string? ResourceId, bool Allowed)>, string>.Error(
                ex.Message);
        }
    }

    /// <summary>
    /// Checks if user has admin permission.
    /// </summary>
    public static async Task<bool> IsAdminAsync(SqliteConnection conn, string userId, ILogger logger)
    {
        var result = await CheckPermissionAsync(conn, userId, "admin:*", null, null, logger)
            .ConfigureAwait(false);
        return result is Result<AuthzResult, string>.Ok(var authz) && authz.Allowed;
    }
}
