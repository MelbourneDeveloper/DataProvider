using Migration;
using Migration.SQLite;

namespace Scheduling.Api;

/// <summary>
/// Database initialization for Scheduling.Api using Migration tool.
/// All tables follow FHIR R4 resource structure with fhir_ prefix.
/// See: https://build.fhir.org/resourcelist.html
/// </summary>
internal static class DatabaseSetup
{
    /// <summary>
    /// Creates the database schema and sync infrastructure using Migration.
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

        // Use Migration tool to create schema from SchedulingSchema metadata
        try
        {
            foreach (var table in SchedulingSchema.Definition.Tables)
            {
                var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(table));
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                cmd.ExecuteNonQuery();
                logger.Log(LogLevel.Debug, "Created table {TableName}", table.Name);
            }

            logger.Log(
                LogLevel.Information,
                "Created Scheduling database schema from SchedulingSchema metadata"
            );
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Failed to create Scheduling database schema");
            return;
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
