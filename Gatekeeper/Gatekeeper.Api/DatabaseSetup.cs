using System.Data;
using Gatekeeper.Migration;
using Migration;
using Migration.SQLite;

namespace Gatekeeper.Api;

/// <summary>
/// Database initialization and seeding using Migration library and DataProvider extensions.
/// </summary>
public static class DatabaseSetup
{
    /// <summary>
    /// Initializes the database schema and seeds default data.
    /// </summary>
    public static void Initialize(SqliteConnection conn, ILogger logger)
    {
        CreateSchemaFromMigration(conn, logger);
        SeedDefaultDataAsync(conn, logger).GetAwaiter().GetResult();
    }

    private static void CreateSchemaFromMigration(SqliteConnection conn, ILogger logger)
    {
        logger.LogInformation("Creating database schema from GatekeeperSchema");

        try
        {
            // Set journal mode to DELETE and synchronous to FULL for test isolation
            _ = conn.SetPragmasAsync().GetAwaiter().GetResult();

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

    private static async Task SeedDefaultDataAsync(SqliteConnection conn, ILogger logger)
    {
        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        // Check if already seeded using DataProvider
        var countResult = await conn.CountSystemRolesAsync().ConfigureAwait(false);
        var count = countResult switch
        {
            CountSystemRolesOk ok => ok.Value.FirstOrDefault()?.cnt ?? 0L,
            _ => 0L,
        };

        if (count > 0)
        {
            logger.LogInformation("Database already seeded, skipping");
            return;
        }

        logger.LogInformation("Seeding default roles and permissions");

        await using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);

        // Core roles
        _ = await tx.Insertgk_roleAsync("role-admin", "admin", "Full system access", 1, now, null).ConfigureAwait(false);
        _ = await tx.Insertgk_roleAsync("role-user", "user", "Basic authenticated user", 1, now, null).ConfigureAwait(false);

        // Core permissions
        _ = await tx.Insertgk_permissionAsync("perm-admin-all", "admin:*", "admin", "*", "Full admin access", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-user-profile", "user:profile", "user", "read", "View own profile", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-user-credentials", "user:credentials", "user", "manage", "Manage own passkeys", now).ConfigureAwait(false);

        // Core role-permission assignments
        _ = await tx.Insertgk_role_permissionAsync("role-admin", "perm-admin-all", now).ConfigureAwait(false);
        _ = await tx.Insertgk_role_permissionAsync("role-user", "perm-user-profile", now).ConfigureAwait(false);
        _ = await tx.Insertgk_role_permissionAsync("role-user", "perm-user-credentials", now).ConfigureAwait(false);

        await SeedClinicalSchedulingPermissionsAsync(tx, now, logger).ConfigureAwait(false);

        await tx.CommitAsync().ConfigureAwait(false);

        logger.LogInformation("Default data seeded successfully");
    }

    private static async Task SeedClinicalSchedulingPermissionsAsync(
        IDbTransaction tx,
        string now,
        ILogger logger
    )
    {
        logger.LogInformation("Seeding Clinical and Scheduling permissions");

        // Clinical domain permissions
        _ = await tx.Insertgk_permissionAsync("perm-patient-read", "patient:read", "patient", "read", "Read patient records", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-patient-create", "patient:create", "patient", "create", "Create patient records", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-patient-update", "patient:update", "patient", "update", "Update patient records", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-patient-all", "patient:*", "patient", "*", "Full patient access", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-encounter-read", "encounter:read", "encounter", "read", "Read encounters", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-encounter-create", "encounter:create", "encounter", "create", "Create encounters", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-encounter-all", "encounter:*", "encounter", "*", "Full encounter access", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-condition-read", "condition:read", "condition", "read", "Read conditions", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-condition-create", "condition:create", "condition", "create", "Create conditions", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-condition-all", "condition:*", "condition", "*", "Full condition access", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-medicationrequest-read", "medicationrequest:read", "medicationrequest", "read", "Read medication requests", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-medicationrequest-create", "medicationrequest:create", "medicationrequest", "create", "Create medication requests", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-medicationrequest-all", "medicationrequest:*", "medicationrequest", "*", "Full medication request access", now).ConfigureAwait(false);

        // Scheduling domain permissions
        _ = await tx.Insertgk_permissionAsync("perm-practitioner-read", "practitioner:read", "practitioner", "read", "Read practitioners", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-practitioner-create", "practitioner:create", "practitioner", "create", "Create practitioners", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-practitioner-update", "practitioner:update", "practitioner", "update", "Update practitioners", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-practitioner-all", "practitioner:*", "practitioner", "*", "Full practitioner access", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-appointment-read", "appointment:read", "appointment", "read", "Read appointments", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-appointment-create", "appointment:create", "appointment", "create", "Create appointments", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-appointment-update", "appointment:update", "appointment", "update", "Update appointments", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-appointment-all", "appointment:*", "appointment", "*", "Full appointment access", now).ConfigureAwait(false);

        // Sync permissions
        _ = await tx.Insertgk_permissionAsync("perm-sync-read", "sync:read", "sync", "read", "Read sync data", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-sync-write", "sync:write", "sync", "write", "Write sync data", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-sync-all", "sync:*", "sync", "*", "Full sync access", now).ConfigureAwait(false);

        // Order permissions (for testing resource grants)
        _ = await tx.Insertgk_permissionAsync("perm-order-read", "order:read", "order", "read", "Read orders", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-order-create", "order:create", "order", "create", "Create orders", now).ConfigureAwait(false);
        _ = await tx.Insertgk_permissionAsync("perm-order-all", "order:*", "order", "*", "Full order access", now).ConfigureAwait(false);

        // Clinical roles
        _ = await tx.Insertgk_roleAsync("role-clinician", "clinician", "Clinical staff with patient access", 1, now, null).ConfigureAwait(false);
        _ = await tx.Insertgk_roleAsync("role-scheduler", "scheduler", "Scheduling staff with appointment access", 1, now, null).ConfigureAwait(false);
        _ = await tx.Insertgk_roleAsync("role-sync-client", "sync-client", "Sync service account", 1, now, null).ConfigureAwait(false);

        // Assign permissions to clinician role
        _ = await tx.Insertgk_role_permissionAsync("role-clinician", "perm-patient-all", now).ConfigureAwait(false);
        _ = await tx.Insertgk_role_permissionAsync("role-clinician", "perm-encounter-all", now).ConfigureAwait(false);
        _ = await tx.Insertgk_role_permissionAsync("role-clinician", "perm-condition-all", now).ConfigureAwait(false);
        _ = await tx.Insertgk_role_permissionAsync("role-clinician", "perm-medicationrequest-all", now).ConfigureAwait(false);
        _ = await tx.Insertgk_role_permissionAsync("role-clinician", "perm-practitioner-read", now).ConfigureAwait(false);
        _ = await tx.Insertgk_role_permissionAsync("role-clinician", "perm-appointment-read", now).ConfigureAwait(false);

        // Assign permissions to scheduler role
        _ = await tx.Insertgk_role_permissionAsync("role-scheduler", "perm-practitioner-all", now).ConfigureAwait(false);
        _ = await tx.Insertgk_role_permissionAsync("role-scheduler", "perm-appointment-all", now).ConfigureAwait(false);
        _ = await tx.Insertgk_role_permissionAsync("role-scheduler", "perm-patient-read", now).ConfigureAwait(false);

        // Assign permissions to sync-client role
        _ = await tx.Insertgk_role_permissionAsync("role-sync-client", "perm-sync-all", now).ConfigureAwait(false);

        logger.LogInformation("Clinical and Scheduling permissions seeded successfully");
    }
}
