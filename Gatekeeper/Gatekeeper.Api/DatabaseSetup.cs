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
                foreach (var statement in ddl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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
