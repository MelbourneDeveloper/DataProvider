using Migration;
using Migration.Postgres;
using InitError = Outcome.Result<bool, string>.Error<bool, string>;
using InitOk = Outcome.Result<bool, string>.Ok<bool, string>;
using InitResult = Outcome.Result<bool, string>;

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
    public static InitResult Initialize(NpgsqlConnection connection, ILogger logger)
    {
        // Create sync infrastructure
        var schemaResult = PostgresSyncSchema.CreateSchema(connection);
        if (schemaResult is BoolSyncError err)
        {
            var msg = SyncHelpers.ToMessage(err.Value);
            logger.Log(LogLevel.Error, "Failed to create sync schema: {Error}", msg);
            return new InitError($"Failed to create sync schema: {msg}");
        }

        _ = PostgresSyncSchema.SetOriginId(connection, Guid.NewGuid().ToString());

        // Use Migration tool to create schema from YAML (source of truth)
        try
        {
            var yamlPath = Path.Combine(AppContext.BaseDirectory, "scheduling-schema.yaml");
            var schema = SchemaYamlSerializer.FromYamlFile(yamlPath);

            foreach (var table in schema.Tables)
            {
                var ddl = PostgresDdlGenerator.Generate(new CreateTableOperation(table));
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                cmd.ExecuteNonQuery();
                logger.Log(LogLevel.Debug, "Created table {TableName}", table.Name);
            }

            logger.Log(LogLevel.Information, "Created Scheduling database schema from YAML");
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Failed to create Scheduling database schema");
            return new InitError($"Failed to create Scheduling database schema: {ex.Message}");
        }

        // Create sync triggers for FHIR resources
        _ = PostgresTriggerGenerator.CreateTriggers(connection, "fhir_Practitioner", logger);
        _ = PostgresTriggerGenerator.CreateTriggers(connection, "fhir_Appointment", logger);
        _ = PostgresTriggerGenerator.CreateTriggers(connection, "fhir_Schedule", logger);
        _ = PostgresTriggerGenerator.CreateTriggers(connection, "fhir_Slot", logger);

        logger.Log(
            LogLevel.Information,
            "Scheduling.Api database initialized with FHIR tables and sync triggers"
        );
        return new InitOk(true);
    }
}
