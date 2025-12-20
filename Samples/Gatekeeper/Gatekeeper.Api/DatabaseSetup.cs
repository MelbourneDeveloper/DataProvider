namespace Gatekeeper.Api;

using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using global::Migration;
using global::Migration.SQLite;

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
        var schema = GatekeeperSchema.Build();
        var generator = new SqliteDdlGenerator();
        var inspector = new SqliteSchemaInspector();

        var currentSchema = inspector.InspectSchema(conn, "gatekeeper");
        var diff = SchemaDiff.Compare(currentSchema, schema);
        var ddlStatements = generator.GenerateDdl(diff);

        foreach (var ddl in ddlStatements)
        {
            logger.LogInformation("Executing DDL: {Ddl}", ddl);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = ddl;
            cmd.ExecuteNonQuery();
        }

        SeedDefaultData(conn, logger);
    }

    private static void SeedDefaultData(SqliteConnection conn, ILogger logger)
    {
        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        // Check if already seeded
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM gk_role WHERE is_system = 1";
        var count = Convert.ToInt64(checkCmd.ExecuteScalar(), CultureInfo.InvariantCulture);

        if (count > 0)
        {
            logger.LogInformation("Database already seeded, skipping");
            return;
        }

        logger.LogInformation("Seeding default roles and permissions");

        // Create system roles
        ExecuteNonQuery(
            conn,
            """
            INSERT INTO gk_role (id, name, description, is_system, created_at)
            VALUES ('role-admin', 'admin', 'Full system access', 1, @now),
                   ('role-user', 'user', 'Basic authenticated user', 1, @now)
            """,
            ("@now", now)
        );

        // Create core permissions
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

        // Assign permissions to roles
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

    private static void ExecuteNonQuery(SqliteConnection conn, string sql, params (string name, object value)[] parameters)
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
