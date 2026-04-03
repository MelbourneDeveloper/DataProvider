using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.DataProvider.Migration.Postgres;
using InitError = Outcome.Result<bool, string>.Error<bool, string>;
using InitOk = Outcome.Result<bool, string>.Ok<bool, string>;
using InitResult = Outcome.Result<bool, string>;

namespace Gatekeeper.Api;

/// <summary>
/// Database initialization and seeding using Migration library.
/// </summary>
internal static class DatabaseSetup
{
    /// <summary>
    /// Initializes the database schema and seeds default data.
    /// </summary>
    public static InitResult Initialize(NpgsqlConnection conn, ILogger logger)
    {
        var schemaResult = CreateSchemaFromMigration(conn, logger);
        if (schemaResult is InitError)
            return schemaResult;

        return SeedDefaultData(conn, logger);
    }

    private static InitResult CreateSchemaFromMigration(NpgsqlConnection conn, ILogger logger)
    {
        logger.LogInformation("Creating database schema from gatekeeper-schema.yaml");

        try
        {
            // Load schema from YAML (source of truth)
            var yamlPath = Path.Combine(AppContext.BaseDirectory, "gatekeeper-schema.yaml");
            var schema = SchemaYamlSerializer.FromYamlFile(yamlPath);

            foreach (var table in schema.Tables)
            {
                var ddl = PostgresDdlGenerator.Generate(new CreateTableOperation(table));
                // DDL may contain multiple statements (CREATE TABLE + CREATE INDEX)
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

            logger.LogInformation("Created Gatekeeper database schema from YAML");
            return new InitOk(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Gatekeeper database schema");
            return new InitError($"Failed to create Gatekeeper database schema: {ex.Message}");
        }
    }

    private static InitResult SeedDefaultData(NpgsqlConnection conn, ILogger logger)
    {
        try
        {
            var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM gk_role WHERE is_system = true";
            var count = Convert.ToInt64(checkCmd.ExecuteScalar(), CultureInfo.InvariantCulture);

            if (count > 0)
            {
                logger.LogInformation("Database already seeded, skipping");
                return new InitOk(true);
            }

            logger.LogInformation("Seeding default roles and permissions");

            ExecuteNonQuery(
                conn,
                """
                INSERT INTO gk_role (id, name, description, is_system, created_at)
                VALUES ('role-admin', 'admin', 'Full system access', true, @now),
                       ('role-user', 'user', 'Basic authenticated user', true, @now)
                """,
                ("@now", now)
            );

            ExecuteNonQuery(
                conn,
                """
                INSERT INTO gk_permission (id, code, resource_type, action, description, created_at)
                VALUES ('perm-admin-all', 'admin:*', 'admin', '*', 'Full admin access', @now),
                       ('perm-user-profile', 'user:profile', 'user', 'read', 'View own profile', @now),
                       ('perm-user-credentials', 'user:credentials', 'user', 'manage', 'Manage own passkeys', @now),
                       ('perm-patient-read', 'patient:read', 'patient', 'read', 'Read patient records', @now),
                       ('perm-order-read', 'order:read', 'order', 'read', 'Read order records', @now),
                       ('perm-sync-read', 'sync:read', 'sync', 'read', 'Read sync data', @now),
                       ('perm-sync-write', 'sync:write', 'sync', 'write', 'Write sync data', @now)
                """,
                ("@now", now)
            );

            ExecuteNonQuery(
                conn,
                """
                INSERT INTO gk_role_permission (role_id, permission_id, granted_at)
                VALUES ('role-admin', 'perm-admin-all', @now),
                       ('role-admin', 'perm-sync-read', @now),
                       ('role-admin', 'perm-sync-write', @now),
                       ('role-user', 'perm-user-profile', @now),
                       ('role-user', 'perm-user-credentials', @now)
                """,
                ("@now", now)
            );

            logger.LogInformation("Default data seeded successfully");
            return new InitOk(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed Gatekeeper default data");
            return new InitError($"Failed to seed Gatekeeper default data: {ex.Message}");
        }
    }

    private static void ExecuteNonQuery(
        NpgsqlConnection conn,
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
