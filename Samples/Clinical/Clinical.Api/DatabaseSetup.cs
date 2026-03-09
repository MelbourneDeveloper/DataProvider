using Migration;
using Migration.Postgres;
using InitError = Outcome.Result<bool, string>.Error<bool, string>;
using InitOk = Outcome.Result<bool, string>.Ok<bool, string>;
using InitResult = Outcome.Result<bool, string>;

namespace Clinical.Api;

/// <summary>
/// Database initialization for Clinical.Api using Migration tool.
/// </summary>
internal static class DatabaseSetup
{
    /// <summary>
    /// Creates the database schema and sync infrastructure using Migration.
    /// </summary>
    public static InitResult Initialize(NpgsqlConnection connection, ILogger logger)
    {
        var schemaResult = PostgresSyncSchema.CreateSchema(connection);
        var originResult = PostgresSyncSchema.SetOriginId(connection, Guid.NewGuid().ToString());

        if (schemaResult is Result<bool, SyncError>.Error<bool, SyncError> schemaErr)
        {
            var msg = SyncHelpers.ToMessage(schemaErr.Value);
            logger.Log(LogLevel.Error, "Failed to create sync schema: {Message}", msg);
            return new InitError($"Failed to create sync schema: {msg}");
        }

        if (originResult is Result<bool, SyncError>.Error<bool, SyncError> originErr)
        {
            var msg = SyncHelpers.ToMessage(originErr.Value);
            logger.Log(LogLevel.Error, "Failed to set origin ID: {Message}", msg);
            return new InitError($"Failed to set origin ID: {msg}");
        }

        // Use Migration tool to create schema from YAML (source of truth)
        try
        {
            var yamlPath = Path.Combine(AppContext.BaseDirectory, "clinical-schema.yaml");
            var schema = SchemaYamlSerializer.FromYamlFile(yamlPath);

            foreach (var table in schema.Tables)
            {
                var ddl = PostgresDdlGenerator.Generate(new CreateTableOperation(table));
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                cmd.ExecuteNonQuery();
                logger.Log(LogLevel.Debug, "Created table {TableName}", table.Name);
            }

            logger.Log(LogLevel.Information, "Created Clinical database schema from YAML");
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Failed to create Clinical database schema");
            return new InitError($"Failed to create Clinical database schema: {ex.Message}");
        }

        var triggerTables = new[]
        {
            "fhir_patient",
            "fhir_encounter",
            "fhir_condition",
            "fhir_medicationrequest",
        };
        foreach (var table in triggerTables)
        {
            var triggerResult = PostgresTriggerGenerator.CreateTriggers(connection, table, logger);
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
        return new InitOk(true);
    }
}
