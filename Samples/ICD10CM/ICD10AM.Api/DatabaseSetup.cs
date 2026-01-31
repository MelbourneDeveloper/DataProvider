using Migration;
using Migration.SQLite;

namespace ICD10AM.Api;

/// <summary>
/// Database initialization for ICD10AM.Api using Migration tool.
/// </summary>
internal static class DatabaseSetup
{
    /// <summary>
    /// Creates the database schema using Migration and populates from ICD-10-CM data.
    /// </summary>
    public static void Initialize(SqliteConnection connection, ILogger logger)
    {
        try
        {
            // Check if tables already exist (e.g., in test scenarios)
            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='icd10am_chapter'";
                var count = Convert.ToInt64(
                    checkCmd.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture
                );
                if (count > 0)
                {
                    logger.Log(
                        LogLevel.Information,
                        "ICD-10-AM database schema already exists, checking for data"
                    );
                    PopulateFromIcd10CmIfEmpty(connection, logger);
                    return;
                }
            }

            var yamlPath = Path.Combine(AppContext.BaseDirectory, "icd10am-schema.yaml");
            var schema = SchemaYamlSerializer.FromYamlFile(yamlPath);

            foreach (var table in schema.Tables)
            {
                var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(table));
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                cmd.ExecuteNonQuery();
                logger.Log(LogLevel.Debug, "Created table {TableName}", table.Name);
            }

            logger.Log(LogLevel.Information, "Created ICD-10-AM database schema from YAML");
            PopulateFromIcd10CmIfEmpty(connection, logger);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Failed to create ICD-10-AM database schema");
        }
    }

    /// <summary>
    /// Populates the API database from the ICD-10-CM source database if empty.
    /// </summary>
    private static void PopulateFromIcd10CmIfEmpty(SqliteConnection connection, ILogger logger)
    {
        try
        {
            // Check if we already have ICD-10-CM codes
            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM icd10cm_code";
            var codeCount = Convert.ToInt64(
                countCmd.ExecuteScalar(),
                System.Globalization.CultureInfo.InvariantCulture
            );

            if (codeCount > 0)
            {
                logger.Log(
                    LogLevel.Information,
                    "ICD-10-CM data already populated ({CodeCount} codes)",
                    codeCount
                );
                return;
            }

            // Find the source database - look for icd10cm.db in parent directories
            var sourceDbPath = FindSourceDatabase();
            if (sourceDbPath is null)
            {
                logger.Log(
                    LogLevel.Warning,
                    "ICD-10-CM source database not found - run import_icd10cm.py first"
                );
                return;
            }

            logger.Log(
                LogLevel.Information,
                "Copying ICD-10-CM data from {SourcePath}",
                sourceDbPath
            );

            // Attach source database and copy data
            using var attachCmd = connection.CreateCommand();
            attachCmd.CommandText = $"ATTACH DATABASE '{sourceDbPath}' AS source";
            attachCmd.ExecuteNonQuery();

            try
            {
                // Copy icd10cm_code table
                using var copyCodesCmd = connection.CreateCommand();
                copyCodesCmd.CommandText = """
                    INSERT INTO icd10cm_code
                    SELECT * FROM source.icd10cm_code
                    """;
                var codesInserted = copyCodesCmd.ExecuteNonQuery();
                logger.Log(LogLevel.Information, "Copied {Count} ICD-10-CM codes", codesInserted);

                // Copy icd10cm_code_embedding table
                using var copyEmbeddingsCmd = connection.CreateCommand();
                copyEmbeddingsCmd.CommandText = """
                    INSERT INTO icd10cm_code_embedding
                    SELECT * FROM source.icd10cm_code_embedding
                    """;
                var embeddingsInserted = copyEmbeddingsCmd.ExecuteNonQuery();
                logger.Log(
                    LogLevel.Information,
                    "Copied {Count} ICD-10-CM embeddings",
                    embeddingsInserted
                );
            }
            finally
            {
                using var detachCmd = connection.CreateCommand();
                detachCmd.CommandText = "DETACH DATABASE source";
                detachCmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Failed to populate ICD-10-CM data");
        }
    }

    /// <summary>
    /// Searches for the ICD-10-CM source database file.
    /// </summary>
    private static string? FindSourceDatabase()
    {
        // Check common locations relative to the API
        var searchPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "icd10cm.db"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "icd10cm.db"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "icd10cm.db"),
            Path.Combine(AppContext.BaseDirectory, "..", "icd10cm.db"),
            Path.Combine(AppContext.BaseDirectory, "icd10cm.db"),
        };

        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}
