namespace Scheduling.Api;

/// <summary>
/// Database initialization for Scheduling.Api.
/// All tables follow FHIR R4 resource structure with fhir_ prefix.
/// See: https://build.fhir.org/resourcelist.html
/// </summary>
internal static class DatabaseSetup
{
    /// <summary>
    /// Creates the database schema and sync infrastructure.
    /// Tables conform to FHIR R4 resources.
    /// </summary>
    public static void Initialize(SqliteConnection connection, ILogger logger)
    {
        // Create sync infrastructure
        var schemaResult = SyncSchema.CreateSchema(connection);
        if (schemaResult is BoolSyncError err)
        {
            logger.Log(
                LogLevel.Error,
                "Failed to create sync schema: {Error}",
                SyncHelpers.ToMessage(err.Value)
            );
            return;
        }

        _ = SyncSchema.SetOriginId(connection, Guid.NewGuid().ToString());

        // Execute schema.sql
        using var cmd = connection.CreateCommand();
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema.sql");

        if (File.Exists(schemaPath))
        {
            cmd.CommandText = File.ReadAllText(schemaPath);
            cmd.ExecuteNonQuery();
        }
        else
        {
            logger.Log(LogLevel.Warning, "schema.sql not found at {Path}", schemaPath);
        }

        // Create sync triggers for FHIR resources
        _ = TriggerGenerator.CreateTriggers(connection, "fhir_Practitioner", logger);
        _ = TriggerGenerator.CreateTriggers(connection, "fhir_Appointment", logger);
        _ = TriggerGenerator.CreateTriggers(connection, "fhir_Schedule", logger);
        _ = TriggerGenerator.CreateTriggers(connection, "fhir_Slot", logger);

        logger.Log(
            LogLevel.Information,
            "Scheduling.Api database initialized with FHIR tables and sync triggers"
        );
    }
}
