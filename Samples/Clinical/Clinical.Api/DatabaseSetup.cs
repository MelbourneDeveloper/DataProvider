using Migration;
using Migration.SQLite;

namespace Clinical.Api;

/// <summary>
/// Database initialization for Clinical.Api using Migration tool.
/// </summary>
internal static class DatabaseSetup
{
    /// <summary>
    /// Creates the database schema and sync infrastructure using Migration.
    /// </summary>
    public static void Initialize(SqliteConnection connection, ILogger logger)
    {
        var schemaResult = SyncSchema.CreateSchema(connection);
        var originResult = SyncSchema.SetOriginId(connection, Guid.NewGuid().ToString());

        if (schemaResult is Result<bool, SyncError>.Error<bool, SyncError> schemaErr)
        {
            logger.Log(
                LogLevel.Error,
                "Failed to create sync schema: {Message}",
                SyncHelpers.ToMessage(schemaErr.Value)
            );
            return;
        }

        if (originResult is Result<bool, SyncError>.Error<bool, SyncError> originErr)
        {
            logger.Log(
                LogLevel.Error,
                "Failed to set origin ID: {Message}",
                SyncHelpers.ToMessage(originErr.Value)
            );
            return;
        }

        // Use Migration tool to create schema from ClinicalSchema metadata
        try
        {
            foreach (var table in ClinicalSchema.Definition.Tables)
            {
                var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(table));
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                cmd.ExecuteNonQuery();
                logger.Log(LogLevel.Debug, "Created table {TableName}", table.Name);
            }

            logger.Log(
                LogLevel.Information,
                "Created Clinical database schema from ClinicalSchema metadata"
            );
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Failed to create Clinical database schema");
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
            if (triggerResult is Result<bool, SyncError>.Error<bool, SyncError> triggerErr)
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
