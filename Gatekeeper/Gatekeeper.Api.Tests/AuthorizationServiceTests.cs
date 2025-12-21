namespace Gatekeeper.Api.Tests;

using System.Globalization;
using Gatekeeper.Api;
using Microsoft.Data.Sqlite;

/// <summary>
/// Unit tests for AuthorizationService permission evaluation.
/// Tests permission matching, wildcard patterns, scope handling, and resource grants.
/// </summary>
public sealed class AuthorizationServiceTests
{
    [Fact]
    public async Task CheckPermissionAsync_ExactMatch_ReturnsAllowed()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithPermission(conn, "patient:read");
        var now = Now();

        // Debug: Check what's in the database
        using var debugCmd = conn.CreateCommand();
        debugCmd.CommandText = """
            SELECT 'user_permission' as tbl, up.user_id, up.permission_id, p.code, up.scope_type
            FROM gk_user_permission up
            JOIN gk_permission p ON up.permission_id = p.id
            WHERE up.user_id = @userId;
            """;
        debugCmd.Parameters.AddWithValue("@userId", userId);
        using var reader = await debugCmd.ExecuteReaderAsync();
        var debugRows = new System.Collections.Generic.List<string>();
        while (await reader.ReadAsync())
        {
            debugRows.Add($"user_id={reader["user_id"]}, perm={reader["code"]}, scope={reader["scope_type"]}");
        }

        var (allowed, reason) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "patient:read",
            null,
            null,
            now
        );

        Assert.True(allowed, $"Expected allowed=true but got reason={reason}. UserId={userId}, Now={now}, Debug rows: [{string.Join("; ", debugRows)}]");
        Assert.Contains("patient:read", reason);
    }

    [Fact]
    public async Task CheckPermissionAsync_NoMatch_ReturnsDenied()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithPermission(conn, "patient:read");

        var (allowed, reason) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "patient:write",
            null,
            null,
            Now()
        );

        Assert.False(allowed);
        Assert.Equal("no matching permission", reason);
    }

    [Fact]
    public async Task CheckPermissionAsync_WildcardMatch_ReturnsAllowed()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithPermission(conn, "admin:*");

        var (allowed, reason) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "admin:users",
            null,
            null,
            Now()
        );

        Assert.True(allowed);
        Assert.Contains("admin:*", reason);
    }

    [Fact]
    public async Task CheckPermissionAsync_WildcardMatchesDeepPermission_ReturnsAllowed()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithPermission(conn, "admin:*");

        var (allowed, reason) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "admin:users:create:bulk",
            null,
            null,
            Now()
        );

        Assert.True(allowed);
    }

    [Fact]
    public async Task CheckPermissionAsync_GlobalWildcard_MatchesEverything()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithPermission(conn, "*:*");

        var (allowed, _) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "anything:here:at:all",
            null,
            null,
            Now()
        );

        Assert.True(allowed);
    }

    [Fact]
    public async Task CheckPermissionAsync_WildcardDoesNotMatchDifferentPrefix()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithPermission(conn, "admin:*");

        var (allowed, _) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "user:profile",
            null,
            null,
            Now()
        );

        Assert.False(allowed);
    }

    [Fact]
    public async Task CheckPermissionAsync_ResourceGrant_ReturnsAllowedForSpecificResource()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithResourceGrant(conn, "patient", "patient-123", "patient:read");

        var (allowed, reason) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "patient:read",
            "patient",
            "patient-123",
            Now()
        );

        Assert.True(allowed);
        Assert.Contains("resource-grant", reason);
        Assert.Contains("patient/patient-123", reason);
    }

    [Fact]
    public async Task CheckPermissionAsync_ResourceGrant_DeniedForDifferentResource()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithResourceGrant(conn, "patient", "patient-123", "patient:read");

        var (allowed, _) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "patient:read",
            "patient",
            "patient-456",
            Now()
        );

        Assert.False(allowed);
    }

    [Fact]
    public async Task CheckPermissionAsync_ExpiredResourceGrant_ReturnsDenied()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithExpiredResourceGrant(conn, "order", "order-999", "order:read");

        var (allowed, _) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "order:read",
            "order",
            "order-999",
            Now()
        );

        Assert.False(allowed);
    }

    [Fact]
    public async Task CheckPermissionAsync_RolePermission_ReturnsAllowed()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithRole(conn, "role-user");

        // role-user has user:profile and user:credentials from seeded data
        var (allowed, reason) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "user:profile",
            null,
            null,
            Now()
        );

        Assert.True(allowed);
        Assert.Contains("role", reason);
    }

    [Fact]
    public async Task CheckPermissionAsync_AdminRole_HasWildcardAccess()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithRole(conn, "role-admin");

        // role-admin has admin:* from seeded data
        var (allowed, reason) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "admin:delete:everything",
            null,
            null,
            Now()
        );

        Assert.True(allowed);
        Assert.Contains("admin", reason);
    }

    [Fact]
    public async Task CheckPermissionAsync_DirectPermissionWithScopeAll_MatchesAnyResource()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithDirectPermission(conn, "patient:read", "all", null);

        var (allowed, _) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "patient:read",
            "patient",
            "any-patient-id",
            Now()
        );

        Assert.True(allowed);
    }

    [Fact]
    public async Task CheckPermissionAsync_DirectPermissionWithScopeRecord_MatchesOnlyThatRecord()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithDirectPermission(conn, "patient:read", "record", "patient-xyz");

        var (allowedMatch, _) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "patient:read",
            "patient",
            "patient-xyz",
            Now()
        );

        var (allowedOther, _) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "patient:read",
            "patient",
            "patient-other",
            Now()
        );

        Assert.True(allowedMatch);
        Assert.False(allowedOther);
    }

    [Fact]
    public async Task CheckPermissionAsync_NoPermissions_ReturnsDenied()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateBareUser(conn);

        var (allowed, reason) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "any:permission",
            null,
            null,
            Now()
        );

        Assert.False(allowed);
        Assert.Equal("no matching permission", reason);
    }

    [Fact]
    public async Task CheckPermissionAsync_ExpiredRoleGrant_ReturnsDenied()
    {
        using var conn = CreateSeededDb();
        var userId = await CreateUserWithExpiredRole(conn, "role-admin");

        var (allowed, _) = await AuthorizationService.CheckPermissionAsync(
            conn,
            userId,
            "admin:users",
            null,
            null,
            Now()
        );

        Assert.False(allowed);
    }

    private static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    private static SqliteConnection CreateSeededDb()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            -- Users
            CREATE TABLE gk_user (
                id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                email TEXT,
                created_at TEXT NOT NULL,
                last_login_at TEXT,
                is_active INTEGER NOT NULL DEFAULT 1,
                metadata TEXT
            );

            -- Roles
            CREATE TABLE gk_role (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                description TEXT,
                is_system INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                parent_role_id TEXT
            );

            -- User-Role assignments
            CREATE TABLE gk_user_role (
                user_id TEXT NOT NULL,
                role_id TEXT NOT NULL,
                granted_at TEXT NOT NULL,
                granted_by TEXT,
                expires_at TEXT,
                PRIMARY KEY (user_id, role_id)
            );

            -- Permissions
            CREATE TABLE gk_permission (
                id TEXT PRIMARY KEY,
                code TEXT NOT NULL,
                resource_type TEXT NOT NULL,
                action TEXT NOT NULL,
                description TEXT,
                created_at TEXT NOT NULL
            );
            CREATE UNIQUE INDEX idx_permission_code ON gk_permission(code);

            -- Role-Permission assignments
            CREATE TABLE gk_role_permission (
                role_id TEXT NOT NULL,
                permission_id TEXT NOT NULL,
                granted_at TEXT NOT NULL,
                PRIMARY KEY (role_id, permission_id)
            );

            -- Direct user-permission grants
            CREATE TABLE gk_user_permission (
                user_id TEXT NOT NULL,
                permission_id TEXT NOT NULL,
                scope_type TEXT,
                scope_value TEXT,
                granted_at TEXT NOT NULL,
                granted_by TEXT,
                expires_at TEXT,
                reason TEXT
            );

            -- Resource-level grants
            CREATE TABLE gk_resource_grant (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL,
                resource_type TEXT NOT NULL,
                resource_id TEXT NOT NULL,
                permission_id TEXT NOT NULL,
                granted_at TEXT NOT NULL,
                granted_by TEXT,
                expires_at TEXT
            );

            -- Seed default roles and permissions
            INSERT INTO gk_role (id, name, description, is_system, created_at)
            VALUES ('role-admin', 'admin', 'Full system access', 1, '2025-01-01T00:00:00Z'),
                   ('role-user', 'user', 'Basic authenticated user', 1, '2025-01-01T00:00:00Z');

            INSERT INTO gk_permission (id, code, resource_type, action, created_at)
            VALUES ('perm-admin-all', 'admin:*', 'admin', '*', '2025-01-01T00:00:00Z'),
                   ('perm-user-profile', 'user:profile', 'user', 'read', '2025-01-01T00:00:00Z'),
                   ('perm-user-credentials', 'user:credentials', 'user', 'manage', '2025-01-01T00:00:00Z');

            INSERT INTO gk_role_permission (role_id, permission_id, granted_at)
            VALUES ('role-admin', 'perm-admin-all', '2025-01-01T00:00:00Z'),
                   ('role-user', 'perm-user-profile', '2025-01-01T00:00:00Z'),
                   ('role-user', 'perm-user-credentials', '2025-01-01T00:00:00Z');
            """;
        cmd.ExecuteNonQuery();

        return conn;
    }

    private static async Task<string> CreateBareUser(SqliteConnection conn)
    {
        var userId = Guid.NewGuid().ToString();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO gk_user (id, display_name, email, created_at, is_active)
            VALUES (@id, 'Bare User', 'bare@example.com', @now, 1);
            """;
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@now", Now());
        await cmd.ExecuteNonQueryAsync();
        return userId;
    }

    private static async Task<string> CreateUserWithRole(SqliteConnection conn, string roleId)
    {
        var userId = Guid.NewGuid().ToString();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO gk_user (id, display_name, email, created_at, is_active)
            VALUES (@id, 'Role User', 'role@example.com', @now, 1);

            INSERT INTO gk_user_role (user_id, role_id, granted_at)
            VALUES (@id, @roleId, @now);
            """;
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@roleId", roleId);
        cmd.Parameters.AddWithValue("@now", Now());
        await cmd.ExecuteNonQueryAsync();
        return userId;
    }

    private static async Task<string> CreateUserWithExpiredRole(SqliteConnection conn, string roleId)
    {
        var userId = Guid.NewGuid().ToString();
        var expired = DateTime.UtcNow.AddHours(-1).ToString("o", CultureInfo.InvariantCulture);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO gk_user (id, display_name, email, created_at, is_active)
            VALUES (@id, 'Expired Role User', 'expired-role@example.com', @now, 1);

            INSERT INTO gk_user_role (user_id, role_id, granted_at, expires_at)
            VALUES (@id, @roleId, @now, @expired);
            """;
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@roleId", roleId);
        cmd.Parameters.AddWithValue("@now", Now());
        cmd.Parameters.AddWithValue("@expired", expired);
        await cmd.ExecuteNonQueryAsync();
        return userId;
    }

    private static async Task<string> CreateUserWithPermission(SqliteConnection conn, string permissionCode)
    {
        var userId = Guid.NewGuid().ToString();
        var permId = $"perm-{Guid.NewGuid():N}";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO gk_user (id, display_name, email, created_at, is_active)
            VALUES (@id, 'Perm User', 'perm@example.com', @now, 1);

            INSERT INTO gk_permission (id, code, resource_type, action, created_at)
            VALUES (@permId, @code, 'test', 'test', @now);

            INSERT INTO gk_user_permission (user_id, permission_id, scope_type, granted_at)
            VALUES (@id, @permId, 'all', @now);
            """;
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@permId", permId);
        cmd.Parameters.AddWithValue("@code", permissionCode);
        cmd.Parameters.AddWithValue("@now", Now());
        await cmd.ExecuteNonQueryAsync();
        return userId;
    }

    private static async Task<string> CreateUserWithDirectPermission(
        SqliteConnection conn,
        string permissionCode,
        string scopeType,
        string? scopeValue
    )
    {
        var userId = Guid.NewGuid().ToString();
        var permId = $"perm-{Guid.NewGuid():N}";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO gk_user (id, display_name, email, created_at, is_active)
            VALUES (@id, 'Direct Perm User', 'direct@example.com', @now, 1);

            INSERT INTO gk_permission (id, code, resource_type, action, created_at)
            VALUES (@permId, @code, 'test', 'test', @now);

            INSERT INTO gk_user_permission (user_id, permission_id, scope_type, scope_value, granted_at)
            VALUES (@id, @permId, @scopeType, @scopeValue, @now);
            """;
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@permId", permId);
        cmd.Parameters.AddWithValue("@code", permissionCode);
        cmd.Parameters.AddWithValue("@scopeType", scopeType);
        cmd.Parameters.AddWithValue("@scopeValue", (object?)scopeValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@now", Now());
        await cmd.ExecuteNonQueryAsync();
        return userId;
    }

    private static async Task<string> CreateUserWithResourceGrant(
        SqliteConnection conn,
        string resourceType,
        string resourceId,
        string permissionCode
    )
    {
        var userId = Guid.NewGuid().ToString();
        var permId = $"perm-{Guid.NewGuid():N}";
        var grantId = Guid.NewGuid().ToString();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO gk_user (id, display_name, email, created_at, is_active)
            VALUES (@id, 'Resource User', 'resource@example.com', @now, 1);

            INSERT INTO gk_permission (id, code, resource_type, action, created_at)
            VALUES (@permId, @code, @resourceType, 'read', @now);

            INSERT INTO gk_resource_grant (id, user_id, resource_type, resource_id, permission_id, granted_at)
            VALUES (@grantId, @id, @resourceType, @resourceId, @permId, @now);
            """;
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@permId", permId);
        cmd.Parameters.AddWithValue("@grantId", grantId);
        cmd.Parameters.AddWithValue("@code", permissionCode);
        cmd.Parameters.AddWithValue("@resourceType", resourceType);
        cmd.Parameters.AddWithValue("@resourceId", resourceId);
        cmd.Parameters.AddWithValue("@now", Now());
        await cmd.ExecuteNonQueryAsync();
        return userId;
    }

    private static async Task<string> CreateUserWithExpiredResourceGrant(
        SqliteConnection conn,
        string resourceType,
        string resourceId,
        string permissionCode
    )
    {
        var userId = Guid.NewGuid().ToString();
        var permId = $"perm-{Guid.NewGuid():N}";
        var grantId = Guid.NewGuid().ToString();
        var expired = DateTime.UtcNow.AddHours(-1).ToString("o", CultureInfo.InvariantCulture);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO gk_user (id, display_name, email, created_at, is_active)
            VALUES (@id, 'Expired Grant User', 'expired-grant@example.com', @now, 1);

            INSERT INTO gk_permission (id, code, resource_type, action, created_at)
            VALUES (@permId, @code, @resourceType, 'read', @now);

            INSERT INTO gk_resource_grant (id, user_id, resource_type, resource_id, permission_id, granted_at, expires_at)
            VALUES (@grantId, @id, @resourceType, @resourceId, @permId, @now, @expired);
            """;
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@permId", permId);
        cmd.Parameters.AddWithValue("@grantId", grantId);
        cmd.Parameters.AddWithValue("@code", permissionCode);
        cmd.Parameters.AddWithValue("@resourceType", resourceType);
        cmd.Parameters.AddWithValue("@resourceId", resourceId);
        cmd.Parameters.AddWithValue("@now", Now());
        cmd.Parameters.AddWithValue("@expired", expired);
        await cmd.ExecuteNonQueryAsync();
        return userId;
    }
}
