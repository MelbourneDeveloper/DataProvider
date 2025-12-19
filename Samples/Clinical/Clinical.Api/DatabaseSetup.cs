namespace Clinical.Api;

/// <summary>
/// Database initialization for Clinical.Api.
/// </summary>
internal static class DatabaseSetup
{
    /// <summary>
    /// Creates the database schema and sync infrastructure.
    /// </summary>
    public static void Initialize(SqliteConnection connection, ILogger logger)
    {
        var schemaResult = SyncSchema.CreateSchema(connection);
        var originResult = SyncSchema.SetOriginId(connection, Guid.NewGuid().ToString());

        if (schemaResult is Result<bool, Sync.SyncError>.Error<bool, Sync.SyncError> schemaErr)
        {
            logger.Log(
                LogLevel.Error,
                "Failed to create sync schema: {Message}",
                SyncHelpers.ToMessage(schemaErr.Value)
            );
            return;
        }

        if (originResult is Result<bool, Sync.SyncError>.Error<bool, Sync.SyncError> originErr)
        {
            logger.Log(
                LogLevel.Error,
                "Failed to set origin ID: {Message}",
                SyncHelpers.ToMessage(originErr.Value)
            );
            return;
        }

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema.sql");
        if (System.IO.File.Exists(schemaPath))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = System.IO.File.ReadAllText(schemaPath);
            cmd.ExecuteNonQuery();
            logger.Log(LogLevel.Information, "Executed schema.sql for Clinical.Api setup");
        }
        else
        {
            logger.Log(LogLevel.Error, "schema.sql not found at {SchemaPath}", schemaPath);
            return;
        }

        var triggerTables = new[]
        {
            "fhir_Patient",
            "fhir_Encounter",
            "fhir_Condition",
            "fhir_MedicationRequest",
        };
        foreach (var table in triggerTables)
        {
            var triggerResult = TriggerGenerator.CreateTriggers(connection, table, logger);
            if (
                triggerResult is Result<bool, Sync.SyncError>.Error<bool, Sync.SyncError> triggerErr
            )
            {
                logger.Log(
                    LogLevel.Error,
                    "Failed to create triggers for {Table}: {Message}",
                    table,
                    SyncHelpers.ToMessage(triggerErr.Value)
                );
            }
        }

        logger.Log(LogLevel.Information, "Clinical.Api database initialized with sync triggers");
    }
}
