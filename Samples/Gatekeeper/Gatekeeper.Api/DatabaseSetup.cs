namespace Gatekeeper.Api;

/// <summary>
/// Database initialization and seeding.
/// </summary>
public static class DatabaseSetup
{
    /// <summary>
    /// Initializes the database schema and seeds default data.
    /// </summary>
    public static void Initialize(SqliteConnection conn, ILogger logger)
    {
        CreateSchemaIfNeeded(conn, logger);
        SeedDefaultData(conn, logger);
    }

    private static void CreateSchemaIfNeeded(SqliteConnection conn, ILogger logger)
    {
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText =
            "SELECT name FROM sqlite_master WHERE type='table' AND name='gk_user'";
        var exists = checkCmd.ExecuteScalar() is not null;

        if (exists)
        {
            logger.LogInformation("Database schema already exists");
            return;
        }

        logger.LogInformation("Creating database schema");

        var schema = GetSchemaScript();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = schema;
        cmd.ExecuteNonQuery();

        logger.LogInformation("Database schema created successfully");
    }

    private static string GetSchemaScript() =>
        """
            -- Core Authentication Tables
            CREATE TABLE gk_user (
                id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                email TEXT,
                created_at TEXT NOT NULL,
                last_login_at TEXT,
                is_active INTEGER NOT NULL DEFAULT 1,
                metadata TEXT
            );
            CREATE UNIQUE INDEX idx_user_email ON gk_user(email);

            CREATE TABLE gk_credential (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL REFERENCES gk_user(id) ON DELETE CASCADE,
                public_key BLOB NOT NULL,
                sign_count INTEGER NOT NULL DEFAULT 0,
                aaguid TEXT,
                credential_type TEXT NOT NULL,
                transports TEXT,
                attestation_format TEXT,
                created_at TEXT NOT NULL,
                last_used_at TEXT,
                device_name TEXT,
                is_backup_eligible INTEGER,
                is_backed_up INTEGER
            );
            CREATE INDEX idx_credential_user ON gk_credential(user_id);

            CREATE TABLE gk_session (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL REFERENCES gk_user(id) ON DELETE CASCADE,
                credential_id TEXT REFERENCES gk_credential(id),
                created_at TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                last_activity_at TEXT NOT NULL,
                ip_address TEXT,
                user_agent TEXT,
                is_revoked INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX idx_session_user ON gk_session(user_id);
            CREATE INDEX idx_session_expires ON gk_session(expires_at);

            CREATE TABLE gk_challenge (
                id TEXT PRIMARY KEY,
                user_id TEXT,
                challenge BLOB NOT NULL,
                type TEXT NOT NULL,
                created_at TEXT NOT NULL,
                expires_at TEXT NOT NULL
            );

            -- RBAC Tables
            CREATE TABLE gk_role (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                description TEXT,
                is_system INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                parent_role_id TEXT REFERENCES gk_role(id)
            );
            CREATE UNIQUE INDEX idx_role_name ON gk_role(name);

            CREATE TABLE gk_user_role (
                user_id TEXT NOT NULL REFERENCES gk_user(id) ON DELETE CASCADE,
                role_id TEXT NOT NULL REFERENCES gk_role(id) ON DELETE CASCADE,
                granted_at TEXT NOT NULL,
                granted_by TEXT REFERENCES gk_user(id),
                expires_at TEXT,
                PRIMARY KEY (user_id, role_id)
            );

            CREATE TABLE gk_permission (
                id TEXT PRIMARY KEY,
                code TEXT NOT NULL,
                resource_type TEXT NOT NULL,
                action TEXT NOT NULL,
                description TEXT,
                created_at TEXT NOT NULL
            );
            CREATE UNIQUE INDEX idx_permission_code ON gk_permission(code);
            CREATE INDEX idx_permission_resource ON gk_permission(resource_type);

            CREATE TABLE gk_role_permission (
                role_id TEXT NOT NULL REFERENCES gk_role(id) ON DELETE CASCADE,
                permission_id TEXT NOT NULL REFERENCES gk_permission(id) ON DELETE CASCADE,
                granted_at TEXT NOT NULL,
                PRIMARY KEY (role_id, permission_id)
            );

            CREATE TABLE gk_user_permission (
                user_id TEXT NOT NULL REFERENCES gk_user(id) ON DELETE CASCADE,
                permission_id TEXT NOT NULL REFERENCES gk_permission(id) ON DELETE CASCADE,
                scope_type TEXT,
                scope_value TEXT,
                granted_at TEXT NOT NULL,
                granted_by TEXT REFERENCES gk_user(id),
                expires_at TEXT,
                reason TEXT
            );
            CREATE UNIQUE INDEX idx_user_permission ON gk_user_permission(user_id, permission_id, scope_value);

            -- Fine-Grained Access Control
            CREATE TABLE gk_resource_grant (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL REFERENCES gk_user(id) ON DELETE CASCADE,
                resource_type TEXT NOT NULL,
                resource_id TEXT NOT NULL,
                permission_id TEXT NOT NULL REFERENCES gk_permission(id),
                granted_at TEXT NOT NULL,
                granted_by TEXT REFERENCES gk_user(id),
                expires_at TEXT
            );
            CREATE INDEX idx_resource_grant_user ON gk_resource_grant(user_id);
            CREATE INDEX idx_resource_grant_resource ON gk_resource_grant(resource_type, resource_id);
            CREATE UNIQUE INDEX uq_resource_grant ON gk_resource_grant(user_id, resource_type, resource_id, permission_id);

            CREATE TABLE gk_policy (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                description TEXT,
                resource_type TEXT NOT NULL,
                action TEXT NOT NULL,
                condition TEXT NOT NULL,
                effect TEXT NOT NULL DEFAULT 'allow',
                priority INTEGER NOT NULL DEFAULT 0,
                is_active INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL
            );
            CREATE UNIQUE INDEX idx_policy_name ON gk_policy(name);
            """;

    private static void SeedDefaultData(SqliteConnection conn, ILogger logger)
    {
        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM gk_role WHERE is_system = 1";
        var count = Convert.ToInt64(checkCmd.ExecuteScalar(), CultureInfo.InvariantCulture);

        if (count > 0)
        {
            logger.LogInformation("Database already seeded, skipping");
            return;
        }

        logger.LogInformation("Seeding default roles and permissions");

        ExecuteNonQuery(
            conn,
            """
            INSERT INTO gk_role (id, name, description, is_system, created_at)
            VALUES ('role-admin', 'admin', 'Full system access', 1, @now),
                   ('role-user', 'user', 'Basic authenticated user', 1, @now)
            """,
            ("@now", now)
        );

        ExecuteNonQuery(
            conn,
            """
            INSERT INTO gk_permission (id, code, resource_type, action, description, created_at)
            VALUES ('perm-admin-all', 'admin:*', 'admin', '*', 'Full admin access', @now),
                   ('perm-user-profile', 'user:profile', 'user', 'read', 'View own profile', @now),
                   ('perm-user-credentials', 'user:credentials', 'user', 'manage', 'Manage own passkeys', @now)
            """,
            ("@now", now)
        );

        ExecuteNonQuery(
            conn,
            """
            INSERT INTO gk_role_permission (role_id, permission_id, granted_at)
            VALUES ('role-admin', 'perm-admin-all', @now),
                   ('role-user', 'perm-user-profile', @now),
                   ('role-user', 'perm-user-credentials', @now)
            """,
            ("@now", now)
        );

        logger.LogInformation("Default data seeded successfully");
    }

    private static void ExecuteNonQuery(
        SqliteConnection conn,
        string sql,
        params (string name, object value)[] parameters
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        cmd.ExecuteNonQuery();
    }
}
