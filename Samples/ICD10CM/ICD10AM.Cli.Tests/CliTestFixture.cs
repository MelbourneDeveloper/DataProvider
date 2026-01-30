using Microsoft.Data.Sqlite;

namespace ICD10AM.Cli.Tests;

/// <summary>
/// Test fixture that creates an isolated temp database with seeded test data.
/// </summary>
public sealed class CliTestFixture : IDisposable
{
    /// <summary>
    /// Gets the path to the test database.
    /// </summary>
    public string DbPath { get; }

    /// <summary>
    /// Creates a new test fixture with seeded data.
    /// </summary>
    public CliTestFixture()
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"icd10cm_cli_test_{Guid.NewGuid()}.db");
        SeedTestData();
    }

    void SeedTestData()
    {
        using var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();

        CreateSchema(conn);
        SeedIcd10CmCodes(conn);
        SeedEmbeddings(conn);
    }

    static void CreateSchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE icd10cm_code (
                Id TEXT PRIMARY KEY,
                CategoryId TEXT NOT NULL,
                Code TEXT NOT NULL,
                ShortDescription TEXT NOT NULL,
                LongDescription TEXT NOT NULL,
                InclusionTerms TEXT NOT NULL,
                ExclusionTerms TEXT NOT NULL,
                CodeAlso TEXT NOT NULL,
                CodeFirst TEXT NOT NULL,
                Billable INTEGER NOT NULL,
                EffectiveFrom TEXT NOT NULL,
                EffectiveTo TEXT NOT NULL,
                Edition INTEGER NOT NULL,
                LastUpdated TEXT NOT NULL,
                VersionId INTEGER NOT NULL
            );

            CREATE TABLE icd10cm_code_embedding (
                Id TEXT PRIMARY KEY,
                CodeId TEXT NOT NULL,
                Embedding TEXT NOT NULL,
                EmbeddingModel TEXT NOT NULL,
                LastUpdated TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    static void SeedIcd10CmCodes(SqliteConnection conn)
    {
        var testCodes = new (
            string id,
            string code,
            string shortDesc,
            string longDesc,
            bool billable
        )[]
        {
            (
                "cm-r074",
                "R07.4",
                "Chest pain, unspecified",
                "Chest pain, unspecified - pain in the thoracic region",
                true
            ),
            (
                "cm-r0789",
                "R07.89",
                "Other chest pain",
                "Other chest pain including pleuritic pain and intercostal pain",
                true
            ),
            (
                "cm-r0600",
                "R06.00",
                "Dyspnea, unspecified",
                "Dyspnea unspecified - shortness of breath, difficulty breathing",
                true
            ),
            (
                "cm-r0602",
                "R06.02",
                "Shortness of breath",
                "Shortness of breath - breathlessness, respiratory distress",
                true
            ),
            (
                "cm-i2111",
                "I21.11",
                "ST elevation myocardial infarction involving RCA",
                "ST elevation myocardial infarction involving right coronary artery - heart attack",
                true
            ),
            (
                "cm-i2119",
                "I21.19",
                "ST elevation MI involving other coronary artery",
                "ST elevation myocardial infarction involving other coronary artery of inferior wall",
                true
            ),
            (
                "cm-j189",
                "J18.9",
                "Pneumonia, unspecified organism",
                "Pneumonia unspecified organism - lung infection",
                true
            ),
            (
                "cm-j209",
                "J20.9",
                "Acute bronchitis, unspecified",
                "Acute bronchitis unspecified - inflammation of bronchial tubes",
                true
            ),
            (
                "cm-e119",
                "E11.9",
                "Type 2 diabetes mellitus without complications",
                "Type 2 diabetes mellitus without complications - adult onset diabetes",
                true
            ),
            (
                "cm-i10",
                "I10",
                "Essential hypertension",
                "Essential primary hypertension - high blood pressure",
                false
            ),
            ("cm-m545", "M54.5", "Low back pain", "Low back pain - lumbago, lower back ache", true),
            (
                "cm-g43909",
                "G43.909",
                "Migraine, unspecified",
                "Migraine unspecified not intractable without status migrainosus - severe headache",
                true
            ),
            (
                "cm-a000",
                "A00.0",
                "Cholera due to Vibrio cholerae 01, biovar cholerae",
                "Classical cholera caused by Vibrio cholerae",
                true
            ),
        };

        foreach (var (id, code, shortDesc, longDesc, billable) in testCodes)
        {
            InsertCode(conn, id, code, shortDesc, longDesc, billable);
        }
    }

    static void InsertCode(
        SqliteConnection conn,
        string id,
        string code,
        string shortDesc,
        string longDesc,
        bool billable
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO icd10cm_code (Id, CategoryId, Code, ShortDescription, LongDescription, InclusionTerms, ExclusionTerms, CodeAlso, CodeFirst, Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated, VersionId)
            VALUES (@id, '', @code, @shortDesc, @longDesc, '', '', '', '', @billable, '2025-07-01', '', 2025, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@shortDesc", shortDesc);
        cmd.Parameters.AddWithValue("@longDesc", longDesc);
        cmd.Parameters.AddWithValue("@billable", billable ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    static void SeedEmbeddings(SqliteConnection conn)
    {
        // Create fake embeddings - just enough to test RAG search functionality
        var codes = new[] { "cm-r074", "cm-r0789", "cm-r0600", "cm-r0602", "cm-i2111" };
        var embeddingDim = 384; // Typical small model dimension

        foreach (var codeId in codes)
        {
            var embedding = CreateFakeEmbedding(embeddingDim, codeId.GetHashCode());
            InsertEmbedding(conn, codeId, embedding);
        }
    }

    static float[] CreateFakeEmbedding(int dim, int seed)
    {
        var rng = new Random(seed);
        var embedding = new float[dim];
        for (var i = 0; i < dim; i++)
        {
            embedding[i] = (float)(rng.NextDouble() * 2 - 1);
        }

        // Normalize
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (var i = 0; i < dim; i++)
        {
            embedding[i] /= (float)magnitude;
        }

        return embedding;
    }

    static void InsertEmbedding(SqliteConnection conn, string codeId, float[] embedding)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO icd10cm_code_embedding (Id, CodeId, Embedding, EmbeddingModel, LastUpdated)
            VALUES (@id, @codeId, @embedding, 'test-model', datetime('now'))
            """;
        cmd.Parameters.AddWithValue("@id", $"emb-{codeId}");
        cmd.Parameters.AddWithValue("@codeId", codeId);
        cmd.Parameters.AddWithValue("@embedding", JsonSerializer.Serialize(embedding));
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (File.Exists(DbPath))
            {
                File.Delete(DbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
