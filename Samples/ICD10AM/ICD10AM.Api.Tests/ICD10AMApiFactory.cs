using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Migration;
using Migration.SQLite;

namespace ICD10AM.Api.Tests;

/// <summary>
/// WebApplicationFactory for ICD10AM.Api e2e testing.
/// Creates isolated temp database with seeded test data.
/// </summary>
public sealed class ICD10AMApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;

    /// <summary>
    /// Creates a new instance with an isolated temp database.
    /// </summary>
    public ICD10AMApiFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"icd10am_test_{Guid.NewGuid()}.db");
    }

    /// <summary>
    /// Gets the database path for direct access in tests if needed.
    /// </summary>
    public string DbPath => _dbPath;

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DbPath", _dbPath);

        var apiAssembly = typeof(Program).Assembly;
        var contentRoot = Path.GetDirectoryName(apiAssembly.Location)!;
        builder.UseContentRoot(contentRoot);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        // Create schema from YAML
        var yamlPath = Path.Combine(
            Path.GetDirectoryName(typeof(Program).Assembly.Location)!,
            "icd10am-schema.yaml"
        );
        var schema = SchemaYamlSerializer.FromYamlFile(yamlPath);

        foreach (var table in schema.Tables)
        {
            var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(table));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = ddl;
            cmd.ExecuteNonQuery();
        }

        // Seed test chapters
        InsertChapter(
            conn,
            id: "ch-01",
            chapterNumber: "I",
            title: "Certain infectious and parasitic diseases",
            codeRangeStart: "A00",
            codeRangeEnd: "B99"
        );

        InsertChapter(
            conn,
            id: "ch-18",
            chapterNumber: "XVIII",
            title: "Symptoms, signs and abnormal clinical and laboratory findings",
            codeRangeStart: "R00",
            codeRangeEnd: "R99"
        );

        // Seed test blocks
        InsertBlock(
            conn,
            id: "blk-a00",
            chapterId: "ch-01",
            blockCode: "A00-A09",
            title: "Intestinal infectious diseases",
            codeRangeStart: "A00",
            codeRangeEnd: "A09"
        );

        InsertBlock(
            conn,
            id: "blk-r00",
            chapterId: "ch-18",
            blockCode: "R00-R09",
            title: "Symptoms and signs involving the circulatory and respiratory systems",
            codeRangeStart: "R00",
            codeRangeEnd: "R09"
        );

        // Seed test categories
        InsertCategory(conn, id: "cat-a00", blockId: "blk-a00", categoryCode: "A00", title: "Cholera");
        InsertCategory(
            conn,
            id: "cat-r07",
            blockId: "blk-r00",
            categoryCode: "R07",
            title: "Pain in throat and chest"
        );

        // Seed test codes
        InsertCode(
            conn,
            id: "code-a000",
            categoryId: "cat-a00",
            code: "A00.0",
            shortDescription: "Cholera due to Vibrio cholerae 01, biovar cholerae",
            longDescription: "Cholera due to Vibrio cholerae 01, biovar cholerae - classical cholera"
        );

        InsertCode(
            conn,
            id: "code-r074",
            categoryId: "cat-r07",
            code: "R07.4",
            shortDescription: "Chest pain, unspecified",
            longDescription: "Chest pain, unspecified - pain in the chest region"
        );

        // Seed ACHI test data
        InsertAchiBlock(
            conn,
            id: "achi-blk-1",
            blockNumber: "1820",
            title: "Procedures on heart",
            codeRangeStart: "38200",
            codeRangeEnd: "38999"
        );

        InsertAchiCode(
            conn,
            id: "achi-code-1",
            blockId: "achi-blk-1",
            code: "38497-00",
            shortDescription: "Coronary angiography",
            longDescription: "Coronary angiography - imaging of coronary arteries"
        );
    }

    private static void InsertChapter(
        SqliteConnection conn,
        string id,
        string chapterNumber,
        string title,
        string codeRangeStart,
        string codeRangeEnd
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO icd10am_chapter (Id, ChapterNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
            VALUES (@id, @chapterNumber, @title, @codeRangeStart, @codeRangeEnd, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@chapterNumber", chapterNumber);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@codeRangeStart", codeRangeStart);
        cmd.Parameters.AddWithValue("@codeRangeEnd", codeRangeEnd);
        cmd.ExecuteNonQuery();
    }

    private static void InsertBlock(
        SqliteConnection conn,
        string id,
        string chapterId,
        string blockCode,
        string title,
        string codeRangeStart,
        string codeRangeEnd
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO icd10am_block (Id, ChapterId, BlockCode, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
            VALUES (@id, @chapterId, @blockCode, @title, @codeRangeStart, @codeRangeEnd, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@chapterId", chapterId);
        cmd.Parameters.AddWithValue("@blockCode", blockCode);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@codeRangeStart", codeRangeStart);
        cmd.Parameters.AddWithValue("@codeRangeEnd", codeRangeEnd);
        cmd.ExecuteNonQuery();
    }

    private static void InsertCategory(
        SqliteConnection conn,
        string id,
        string blockId,
        string categoryCode,
        string title
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO icd10am_category (Id, BlockId, CategoryCode, Title, LastUpdated, VersionId)
            VALUES (@id, @blockId, @categoryCode, @title, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@blockId", blockId);
        cmd.Parameters.AddWithValue("@categoryCode", categoryCode);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.ExecuteNonQuery();
    }

    private static void InsertCode(
        SqliteConnection conn,
        string id,
        string categoryId,
        string code,
        string shortDescription,
        string longDescription
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO icd10am_code (Id, CategoryId, Code, ShortDescription, LongDescription, InclusionTerms, ExclusionTerms, CodeAlso, CodeFirst, Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated, VersionId)
            VALUES (@id, @categoryId, @code, @shortDescription, @longDescription, '', '', '', '', 1, '2025-07-01', '', 13, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@categoryId", categoryId);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@shortDescription", shortDescription);
        cmd.Parameters.AddWithValue("@longDescription", longDescription);
        cmd.ExecuteNonQuery();
    }

    private static void InsertAchiBlock(
        SqliteConnection conn,
        string id,
        string blockNumber,
        string title,
        string codeRangeStart,
        string codeRangeEnd
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO achi_block (Id, BlockNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
            VALUES (@id, @blockNumber, @title, @codeRangeStart, @codeRangeEnd, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@blockNumber", blockNumber);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@codeRangeStart", codeRangeStart);
        cmd.Parameters.AddWithValue("@codeRangeEnd", codeRangeEnd);
        cmd.ExecuteNonQuery();
    }

    private static void InsertAchiCode(
        SqliteConnection conn,
        string id,
        string blockId,
        string code,
        string shortDescription,
        string longDescription
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO achi_code (Id, BlockId, Code, ShortDescription, LongDescription, Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated, VersionId)
            VALUES (@id, @blockId, @code, @shortDescription, @longDescription, 1, '2025-07-01', '', 13, datetime('now'), 1)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@blockId", blockId);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@shortDescription", shortDescription);
        cmd.Parameters.AddWithValue("@longDescription", longDescription);
        cmd.ExecuteNonQuery();
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
}
