using Migration;
using Migration.Postgres;

namespace ICD10.Api;

/// <summary>
/// Database initialization for ICD10.Api using Migration tool.
/// </summary>
internal static class DatabaseSetup
{
    /// <summary>
    /// Creates the database schema using Migration.
    /// </summary>
    public static void Initialize(NpgsqlConnection connection, ILogger logger)
    {
        try
        {
            // Check if tables already exist (e.g., in test scenarios)
            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText =
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'icd10_chapter'";
                var count = Convert.ToInt64(
                    checkCmd.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture
                );
                if (count > 0)
                {
                    logger.Log(
                        LogLevel.Information,
                        "ICD-10 database schema already exists, skipping initialization"
                    );
                    return;
                }
            }

            var yamlPath = Path.Combine(AppContext.BaseDirectory, "icd10-schema.yaml");
            var schema = SchemaYamlSerializer.FromYamlFile(yamlPath);

            foreach (var table in schema.Tables)
            {
                var ddl = PostgresDdlGenerator.Generate(new CreateTableOperation(table));
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                cmd.ExecuteNonQuery();
                logger.Log(LogLevel.Debug, "Created table {TableName}", table.Name);
            }

            logger.Log(LogLevel.Information, "Created ICD-10 database schema from YAML");
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Failed to create ICD-10 database schema");
        }
    }
}
