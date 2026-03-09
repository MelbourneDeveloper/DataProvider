using Migration;
using Migration.Postgres;
using InitError = Outcome.Result<bool, string>.Error<bool, string>;
using InitOk = Outcome.Result<bool, string>.Ok<bool, string>;
using InitResult = Outcome.Result<bool, string>;

namespace ICD10.Api;

/// <summary>
/// Database initialization for ICD10.Api using Migration tool.
/// </summary>
internal static class DatabaseSetup
{
    /// <summary>
    /// Creates the database schema using Migration.
    /// </summary>
    public static InitResult Initialize(NpgsqlConnection connection, ILogger logger)
    {
        try
        {
            // Enable pgvector extension for vector similarity search
            using (var extCmd = connection.CreateCommand())
            {
                extCmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector";
                extCmd.ExecuteNonQuery();
                logger.Log(LogLevel.Information, "Enabled pgvector extension");
            }

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

                    // Ensure vector indexes exist even if schema already created
                    EnsureVectorIndexes(connection, logger);
                    return new InitOk(true);
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

            // Create vector indexes for fast similarity search
            EnsureVectorIndexes(connection, logger);

            logger.Log(LogLevel.Information, "Created ICD-10 database schema from YAML");
            return new InitOk(true);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Failed to create ICD-10 database schema");
            return new InitError($"Failed to create ICD-10 database schema: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates vector indexes for fast similarity search using pgvector.
    /// Embeddings are stored as JSON text, cast to vector at query time.
    /// Index uses IVFFlat for approximate nearest neighbor search.
    /// </summary>
    private static void EnsureVectorIndexes(NpgsqlConnection connection, ILogger logger)
    {
        try
        {
            // ICD-10 embedding vector index (384 dimensions for MedEmbed-small)
            using (var cmd = connection.CreateCommand())
            {
                // Check if we have any embeddings to index
                cmd.CommandText = "SELECT COUNT(*) FROM icd10_code_embedding";
                var count = Convert.ToInt64(
                    cmd.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture
                );

                if (count > 0)
                {
                    // Create IVFFlat index for fast approximate nearest neighbor search
                    // lists = sqrt(row_count) is a good default
                    var lists = Math.Max(100, (int)Math.Sqrt(count));
                    cmd.CommandText = $"""
                        CREATE INDEX IF NOT EXISTS idx_icd10_embedding_vector
                        ON icd10_code_embedding
                        USING ivfflat (("embedding"::vector(384)) vector_cosine_ops)
                        WITH (lists = {lists})
                        """;
                    cmd.ExecuteNonQuery();
                    logger.Log(
                        LogLevel.Information,
                        "Created IVFFlat vector index on icd10_code_embedding ({Lists} lists)",
                        lists
                    );
                }
            }

            // ACHI embedding vector index
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM achi_code_embedding";
                var count = Convert.ToInt64(
                    cmd.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture
                );

                if (count > 0)
                {
                    var lists = Math.Max(10, (int)Math.Sqrt(count));
                    cmd.CommandText = $"""
                        CREATE INDEX IF NOT EXISTS idx_achi_embedding_vector
                        ON achi_code_embedding
                        USING ivfflat (("embedding"::vector(384)) vector_cosine_ops)
                        WITH (lists = {lists})
                        """;
                    cmd.ExecuteNonQuery();
                    logger.Log(
                        LogLevel.Information,
                        "Created IVFFlat vector index on achi_code_embedding ({Lists} lists)",
                        lists
                    );
                }
            }
        }
        catch (Exception ex)
        {
            // Vector indexes are optional - search will still work, just slower
            logger.Log(
                LogLevel.Warning,
                ex,
                "Could not create vector indexes (search will be slower)"
            );
        }
    }
}
