using Gatekeeper.Migration;
using Migration;
using Migration.SQLite;

namespace Gatekeeper.Api;

/// <summary>
/// Database initialization and seeding using Migration library.
/// </summary>
public static class DatabaseSetup
{
    /// <summary>
    /// Initializes the database schema and seeds default data.
    /// </summary>
    public static void Initialize(SqliteConnection conn, ILogger logger)
    {
        CreateSchemaFromMigration(conn, logger);
        SeedDefaultData(conn, logger);
    }

    private static void CreateSchemaFromMigration(SqliteConnection conn, ILogger logger)
    {
        logger.LogInformation("Creating database schema from GatekeeperSchema");

        try
        {
            // Set journal mode to DELETE and synchronous to FULL for test isolation
            using var pragmaCmd = conn.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA journal_mode = DELETE; PRAGMA synchronous = FULL;";
            pragmaCmd.ExecuteNonQuery();
            var schema = GatekeeperSchema.Build();
            foreach (var table in schema.Tables)
            {
                var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(table));
                // DDL may contain multiple statements (CREATE TABLE + CREATE INDEX)
                // SQLite ExecuteNonQuery only executes the first statement, so split them
                foreach (
                    var statement in ddl.Split(
                        ';',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    )
                )
                {
                    if (string.IsNullOrWhiteSpace(statement))
                    {
                        continue;
                    }
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = statement;
                    cmd.ExecuteNonQuery();
                }
                logger.LogDebug("Created table {TableName}", table.Name);
            }

            logger.LogInformation(
                "Created Gatekeeper database schema from GatekeeperSchema metadata"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Gatekeeper database schema");
            throw;
        }
    }

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

        SeedClinicalSchedulingPermissions(conn, now, logger);

        logger.LogInformation("Default data seeded successfully");
    }

    private static void SeedClinicalSchedulingPermissions(
        SqliteConnection conn,
        string now,
        ILogger logger
    )
    {
        logger.LogInformation("Seeding Clinical and Scheduling permissions");

        // Clinical domain permissions
        ExecuteNonQuery(
            conn,
            """
            INSERT INTO gk_permission (id, code, resource_type, action, description, created_at)
            VALUES
                ('perm-patient-read', 'patient:read', 'patient', 'read', 'Read patient records', @now),
                ('perm-patient-create', 'patient:create', 'patient', 'create', 'Create patient records', @now),
                ('perm-patient-update', 'patient:update', 'patient', 'update', 'Update patient records', @now),
                ('perm-patient-all', 'patient:*', 'patient', '*', 'Full patient access', @now),
                ('perm-encounter-read', 'encounter:read', 'encounter', 'read', 'Read encounters', @now),
                ('perm-encounter-create', 'encounter:create', 'encounter', 'create', 'Create encounters', @now),
                ('perm-encounter-all', 'encounter:*', 'encounter', '*', 'Full encounter access', @now),
                ('perm-condition-read', 'condition:read', 'condition', 'read', 'Read conditions', @now),
                ('perm-condition-create', 'condition:create', 'condition', 'create', 'Create conditions', @now),
                ('perm-condition-all', 'condition:*', 'condition', '*', 'Full condition access', @now),
                ('perm-medicationrequest-read', 'medicationrequest:read', 'medicationrequest', 'read', 'Read medication requests', @now),
                ('perm-medicationrequest-create', 'medicationrequest:create', 'medicationrequest', 'create', 'Create medication requests', @now),
                ('perm-medicationrequest-all', 'medicationrequest:*', 'medicationrequest', '*', 'Full medication request access', @now)
            """,
            ("@now", now)
        );

        // Scheduling domain permissions
        ExecuteNonQuery(
            conn,
            """
            INSERT INTO gk_permission (id, code, resource_type, action, description, created_at)
            VALUES
                ('perm-practitioner-read', 'practitioner:read', 'practitioner', 'read', 'Read practitioners', @now),
                ('perm-practitioner-create', 'practitioner:create', 'practitioner', 'create', 'Create practitioners', @now),
                ('perm-practitioner-update', 'practitioner:update', 'practitioner', 'update', 'Update practitioners', @now),
                ('perm-practitioner-all', 'practitioner:*', 'practitioner', '*', 'Full practitioner access', @now),
                ('perm-appointment-read', 'appointment:read', 'appointment', 'read', 'Read appointments', @now),
                ('perm-appointment-create', 'appointment:create', 'appointment', 'create', 'Create appointments', @now),
                ('perm-appointment-update', 'appointment:update', 'appointment', 'update', 'Update appointments', @now),
                ('perm-appointment-all', 'appointment:*', 'appointment', '*', 'Full appointment access', @now)
            """,
            ("@now", now)
        );

        // Sync permissions
        ExecuteNonQuery(
            conn,
            """
            INSERT INTO gk_permission (id, code, resource_type, action, description, created_at)
            VALUES
                ('perm-sync-read', 'sync:read', 'sync', 'read', 'Read sync data', @now),
                ('perm-sync-write', 'sync:write', 'sync', 'write', 'Write sync data', @now),
                ('perm-sync-all', 'sync:*', 'sync', '*', 'Full sync access', @now)
            """,
            ("@now", now)
        );

        // Clinical roles
        ExecuteNonQuery(
            conn,
            """
            INSERT INTO gk_role (id, name, description, is_system, created_at)
            VALUES
                ('role-clinician', 'clinician', 'Clinical staff with patient access', 1, @now),
                ('role-scheduler', 'scheduler', 'Scheduling staff with appointment access', 1, @now),
                ('role-sync-client', 'sync-client', 'Sync service account', 1, @now)
            """,
            ("@now", now)
        );

        // Assign permissions to clinician role
        ExecuteNonQuery(
            conn,
            """
            INSERT INTO gk_role_permission (role_id, permission_id, granted_at)
            VALUES
                ('role-clinician', 'perm-patient-all', @now),
                ('role-clinician', 'perm-encounter-all', @now),
                ('role-clinician', 'perm-condition-all', @now),
                ('role-clinician', 'perm-medicationrequest-all', @now),
                ('role-clinician', 'perm-practitioner-read', @now),
                ('role-clinician', 'perm-appointment-read', @now)
            """,
            ("@now", now)
        );

        // Assign permissions to scheduler role
        ExecuteNonQuery(
            conn,
            """
            INSERT INTO gk_role_permission (role_id, permission_id, granted_at)
            VALUES
                ('role-scheduler', 'perm-practitioner-all', @now),
                ('role-scheduler', 'perm-appointment-all', @now),
                ('role-scheduler', 'perm-patient-read', @now)
            """,
            ("@now", now)
        );

        // Assign permissions to sync-client role
        ExecuteNonQuery(
            conn,
            """
            INSERT INTO gk_role_permission (role_id, permission_id, granted_at)
            VALUES ('role-sync-client', 'perm-sync-all', @now)
            """,
            ("@now", now)
        );

        logger.LogInformation("Clinical and Scheduling permissions seeded successfully");
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
