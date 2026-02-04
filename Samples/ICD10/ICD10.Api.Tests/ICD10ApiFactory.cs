using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace ICD10.Api.Tests;

/// <summary>
/// WebApplicationFactory for ICD10.Api e2e testing.
/// Seeds minimal test data for integration tests.
/// </summary>
public sealed class ICD10ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;

    /// <summary>
    /// Creates a new instance with seeded test data.
    /// </summary>
    public ICD10ApiFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"icd10_test_{Guid.NewGuid()}.db");

        var sourceDb = Path.Combine(
            Path.GetDirectoryName(typeof(Program).Assembly.Location)!,
            "icd10.db"
        );

        if (!File.Exists(sourceDb))
        {
            throw new FileNotFoundException(
                $"Schema database not found at {sourceDb}. Build the API project first."
            );
        }

        File.Copy(sourceDb, _dbPath);
        SeedTestData();
    }

    /// <summary>
    /// Gets the database path for direct access in tests if needed.
    /// </summary>
    public string DbPath => _dbPath;

    /// <summary>
    /// Checks if the embedding service at localhost:8000 is available.
    /// </summary>
    public bool EmbeddingServiceAvailable
    {
        get
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var response = client.GetAsync("http://localhost:8000/health").Result;
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DbPath", _dbPath);

        var apiAssembly = typeof(Program).Assembly;
        var contentRoot = Path.GetDirectoryName(apiAssembly.Location)!;
        builder.UseContentRoot(contentRoot);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private void SeedTestData()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        // Clear ALL existing ICD-10 data so tests only see seeded test data
        // Delete in reverse FK order: embeddings -> codes -> categories -> blocks -> chapters
        Execute(conn, "DELETE FROM icd10_code_embedding");
        Execute(conn, "DELETE FROM icd10_code");
        Execute(conn, "DELETE FROM icd10_category");
        Execute(conn, "DELETE FROM icd10_block");
        Execute(conn, "DELETE FROM icd10_chapter");

        // Seed chapters with predictable IDs matching test expectations
        var chapters = new (string id, string num, string title, string start, string end)[]
        {
            ("ch-01", "I", "Certain infectious and parasitic diseases", "A00", "B99"),
            ("ch-04", "IV", "Endocrine, nutritional and metabolic diseases", "E00", "E90"),
            ("ch-06", "VI", "Diseases of the nervous system", "G00", "G99"),
            ("ch-09", "IX", "Diseases of the circulatory system", "I00", "I99"),
            ("ch-10", "X", "Diseases of the respiratory system", "J00", "J99"),
            ("ch-13", "XIII", "Diseases of the musculoskeletal system", "M00", "M99"),
            ("ch-18", "XVIII", "Symptoms, signs and abnormal findings", "R00", "R99"),
        };

        foreach (var (id, num, title, start, end) in chapters)
        {
            Execute(
                conn,
                """
                INSERT INTO icd10_chapter (Id, ChapterNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
                VALUES (@id, @num, @title, @start, @end, datetime('now'), 1)
                """,
                ("@id", id),
                ("@num", num),
                ("@title", title),
                ("@start", start),
                ("@end", end)
            );
        }

        // Seed blocks with predictable IDs matching test expectations
        var blocks = new (string id, string chapterNum, string code, string title)[]
        {
            ("blk-a00", "I", "A00-A09", "Intestinal infectious diseases"),
            ("blk-e10", "IV", "E10-E14", "Diabetes mellitus"),
            ("blk-g40", "VI", "G40-G47", "Episodic and paroxysmal disorders"),
            ("blk-i10", "IX", "I10-I15", "Hypertensive diseases"),
            ("blk-i20", "IX", "I20-I25", "Ischaemic heart diseases"),
            ("blk-j00", "X", "J00-J06", "Acute upper respiratory infections"),
            ("blk-j12", "X", "J12-J18", "Pneumonia"),
            ("blk-j20", "X", "J20-J22", "Other acute lower respiratory infections"),
            ("blk-m54", "XIII", "M54", "Dorsalgia"),
            (
                "blk-r00",
                "XVIII",
                "R00-R09",
                "Symptoms involving circulatory and respiratory systems"
            ),
        };

        foreach (var (id, chapterNum, code, title) in blocks)
        {
            var chapterId = chapters.First(c => c.num == chapterNum).id;
            var parts = code.Split('-');
            Execute(
                conn,
                """
                INSERT INTO icd10_block (Id, ChapterId, BlockCode, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
                VALUES (@id, @chapterId, @code, @title, @start, @end, datetime('now'), 1)
                """,
                ("@id", id),
                ("@chapterId", chapterId),
                ("@code", code),
                ("@title", title),
                ("@start", parts[0]),
                ("@end", parts.Length > 1 ? parts[1] : parts[0])
            );
        }

        // Seed categories with predictable IDs (blockCode is still the range for lookup)
        var categories = new (string id, string blockCode, string code, string title)[]
        {
            ("cat-a00", "A00-A09", "A00", "Cholera"),
            ("cat-e10", "E10-E14", "E10", "Type 1 diabetes mellitus"),
            ("cat-e11", "E10-E14", "E11", "Type 2 diabetes mellitus"),
            ("cat-g43", "G40-G47", "G43", "Migraine"),
            ("cat-i10", "I10-I15", "I10", "Essential hypertension"),
            ("cat-i21", "I20-I25", "I21", "Acute myocardial infarction"),
            ("cat-j06", "J00-J06", "J06", "Acute upper respiratory infections"),
            ("cat-j18", "J12-J18", "J18", "Pneumonia, organism unspecified"),
            ("cat-j20", "J20-J22", "J20", "Acute bronchitis"),
            ("cat-m54", "M54", "M54", "Dorsalgia"),
            ("cat-r06", "R00-R09", "R06", "Abnormalities of breathing"),
            ("cat-r07", "R00-R09", "R07", "Pain in throat and chest"),
        };

        foreach (var (id, blockCode, code, title) in categories)
        {
            var blockId = blocks.First(b => b.code == blockCode).id;
            Execute(
                conn,
                """
                INSERT INTO icd10_category (Id, BlockId, CategoryCode, Title, LastUpdated, VersionId)
                VALUES (@id, @blockId, @code, @title, datetime('now'), 1)
                """,
                ("@id", id),
                ("@blockId", blockId),
                ("@code", code),
                ("@title", title)
            );
        }

        // Seed codes with embeddings (descriptions use American spelling to match tests)
        var codes = new (string code, string catCode, string shortDesc, string longDesc, string? synonyms)[]
        {
            (
                "A00.0",
                "A00",
                "Cholera due to Vibrio cholerae",
                "Cholera due to Vibrio cholerae 01, biovar cholerae",
                null
            ),
            (
                "E10.9",
                "E10",
                "Type 1 diabetes without complications",
                "Type 1 diabetes mellitus without complications",
                "juvenile diabetes; insulin-dependent diabetes"
            ),
            (
                "E11.9",
                "E11",
                "Type 2 diabetes without complications",
                "Type 2 diabetes mellitus without complications",
                "adult-onset diabetes; non-insulin-dependent diabetes"
            ),
            (
                "G43.909",
                "G43",
                "Migraine, unspecified, not intractable",
                "Migraine, unspecified, not intractable, without status migrainosus",
                "sick headache; hemicrania"
            ),
            ("I10", "I10", "Essential (primary) hypertension", "Essential (primary) hypertension", "high blood pressure"),
            (
                "I21.0",
                "I21",
                "Acute transmural myocardial infarction of anterior wall",
                "Acute transmural myocardial infarction of anterior wall",
                "heart attack; AMI"
            ),
            (
                "I21.11",
                "I21",
                "ST elevation myocardial infarction",
                "ST elevation myocardial infarction involving diagonal coronary artery",
                "STEMI"
            ),
            (
                "I21.4",
                "I21",
                "Acute subendocardial myocardial infarction",
                "Acute subendocardial myocardial infarction",
                "NSTEMI"
            ),
            (
                "J06.9",
                "J06",
                "Acute upper respiratory infection",
                "Acute upper respiratory infection, unspecified",
                "common cold; URI"
            ),
            ("J18.9", "J18", "Pneumonia, unspecified", "Pneumonia, unspecified organism", "lung infection"),
            ("J20.9", "J20", "Acute bronchitis, unspecified", "Acute bronchitis, unspecified", "chest cold"),
            ("M54.5", "M54", "Low back pain", "Low back pain", "lumbago; backache"),
            ("R06.00", "R06", "Dyspnea, unspecified", "Dyspnea, unspecified", "difficulty breathing"),
            ("R06.02", "R06", "Shortness of breath", "Shortness of breath", "breathlessness; SOB"),
            ("R07.4", "R07", "Chest pain, unspecified", "Chest pain, unspecified", "thoracic pain"),
            ("R07.89", "R07", "Other chest pain", "Other chest pain", null),
        };

        foreach (var (code, catCode, shortDesc, longDesc, synonyms) in codes)
        {
            var codeId = Guid.NewGuid().ToString();
            var categoryId = categories.First(c => c.code == catCode).id;
            Execute(
                conn,
                """
                INSERT INTO icd10_code (Id, CategoryId, Code, ShortDescription, LongDescription, Synonyms, Billable, Edition, LastUpdated, VersionId)
                VALUES (@id, @categoryId, @code, @short, @long, @synonyms, 1, '2025', datetime('now'), 1)
                """,
                ("@id", codeId),
                ("@categoryId", categoryId),
                ("@code", code),
                ("@short", shortDesc),
                ("@long", longDesc),
                ("@synonyms", (object?)synonyms ?? DBNull.Value)
            );

            // Insert fake embedding (384 dimensions for MedEmbed)
            var embeddingId = Guid.NewGuid().ToString();
            var fakeEmbedding = string.Join(",", Enumerable.Range(0, 384).Select(_ => "0.01"));
            Execute(
                conn,
                """
                INSERT INTO icd10_code_embedding (Id, CodeId, Embedding, EmbeddingModel, LastUpdated)
                VALUES (@id, @codeId, @embedding, 'MedEmbed-Small-v0.1', datetime('now'))
                """,
                ("@id", embeddingId),
                ("@codeId", codeId),
                ("@embedding", fakeEmbedding)
            );
        }

        // Seed ACHI blocks and codes with predictable IDs
        var achiBlockId = "achi-blk-1";
        Execute(
            conn,
            """
            INSERT INTO achi_block (Id, BlockNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
            VALUES (@id, '1820', 'Procedures on heart', '38400', '38599', datetime('now'), 1)
            """,
            ("@id", achiBlockId)
        );

        var achiCodes = new (string code, string shortDesc)[]
        {
            ("38497-00", "Coronary angiography"),
            ("38500-00", "Percutaneous transluminal coronary angioplasty"),
        };

        foreach (var (code, shortDesc) in achiCodes)
        {
            var codeId = Guid.NewGuid().ToString();
            Execute(
                conn,
                """
                INSERT INTO achi_code (Id, BlockId, Code, ShortDescription, LongDescription, Billable, Edition, LastUpdated, VersionId)
                VALUES (@id, @blockId, @code, @short, @short, 1, '13', datetime('now'), 1)
                """,
                ("@id", codeId),
                ("@blockId", achiBlockId),
                ("@code", code),
                ("@short", shortDesc)
            );
        }
    }

    private static void Execute(
        SqliteConnection conn,
        string sql,
        params (string name, object value)[] parameters
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        cmd.ExecuteNonQuery();
    }
}
