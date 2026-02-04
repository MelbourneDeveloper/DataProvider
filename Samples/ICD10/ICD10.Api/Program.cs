#pragma warning disable IDE0037 // Use inferred member name - prefer explicit for clarity in API responses
#pragma warning disable CA1812 // Avoid uninstantiated internal classes - records are instantiated by JSON deserialization

using System.Collections.Frozen;
using System.Text.Json;
using ICD10.Api;
using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON to use PascalCase property names
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});

// Add CORS for dashboard - allow any origin for testing
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Dashboard",
        policy =>
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    );
});

// Always use a real SQLite file - NEVER in-memory
// Look for icd10.db (populated by Python import script) in parent directories
var dbPath = builder.Configuration["DbPath"] ?? FindDatabaseFile();

static string FindDatabaseFile()
{
    var searchPaths = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "icd10.db"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "icd10.db"),
        Path.Combine(AppContext.BaseDirectory, "..", "icd10.db"),
        Path.Combine(AppContext.BaseDirectory, "icd10.db"),
    };

    foreach (var path in searchPaths)
    {
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            return fullPath;
        }
    }

    // Fallback to default location
    return Path.Combine(AppContext.BaseDirectory, "icd10.db");
}
var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    ForeignKeys = true,
}.ToString();

builder.Services.AddSingleton(() =>
{
    var conn = new SqliteConnection(connectionString);
    conn.Open();
    return conn;
});

// Embedding service (Docker container)
var embeddingServiceUrl =
    builder.Configuration["EmbeddingService:BaseUrl"] ?? "http://localhost:8000";
builder.Services.AddHttpClient(
    "EmbeddingService",
    client =>
    {
        client.BaseAddress = new Uri(embeddingServiceUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    }
);

// Gatekeeper configuration for authorization
var gatekeeperUrl = builder.Configuration["Gatekeeper:BaseUrl"] ?? "http://localhost:5002";
var signingKeyBase64 = builder.Configuration["Jwt:SigningKey"];
var signingKey = string.IsNullOrEmpty(signingKeyBase64)
    ? ImmutableArray.Create(new byte[32])
    : ImmutableArray.Create(Convert.FromBase64String(signingKeyBase64));

builder.Services.AddHttpClient(
    "Gatekeeper",
    client =>
    {
        client.BaseAddress = new Uri(gatekeeperUrl);
        client.Timeout = TimeSpan.FromSeconds(5);
    }
);

var app = builder.Build();

using (var conn = new SqliteConnection(connectionString))
{
    conn.Open();
    DatabaseSetup.Initialize(conn, app.Logger);
}

app.UseCors("Dashboard");

// Note: Func<HttpClient> for embedding service is registered in DI below

// ============================================================================
// ICD-10 HIERARCHICAL BROWSE ENDPOINTS
// ============================================================================

var icdGroup = app.MapGroup("/api/icd10").WithTags("ICD-10");

icdGroup.MapGet(
    "/chapters",
    async (Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetChaptersAsync().ConfigureAwait(false);
        return result switch
        {
            GetChaptersOk(var chapters) => Results.Ok(chapters),
            GetChaptersError(var err) => Results.Problem(err.Message),
        };
    }
);

icdGroup.MapGet(
    "/chapters/{chapterId}/blocks",
    async (string chapterId, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetBlocksByChapterAsync(chapterId).ConfigureAwait(false);
        return result switch
        {
            GetBlocksByChapterOk(var blocks) => Results.Ok(blocks),
            GetBlocksByChapterError(var err) => Results.Problem(err.Message),
        };
    }
);

icdGroup.MapGet(
    "/blocks/{blockId}/categories",
    async (string blockId, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetCategoriesByBlockAsync(blockId).ConfigureAwait(false);
        return result switch
        {
            GetCategoriesByBlockOk(var categories) => Results.Ok(categories),
            GetCategoriesByBlockError(var err) => Results.Problem(err.Message),
        };
    }
);

icdGroup.MapGet(
    "/categories/{categoryId}/codes",
    async (string categoryId, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetCodesByCategoryAsync(categoryId).ConfigureAwait(false);
        return result switch
        {
            GetCodesByCategoryOk(var codes) => Results.Ok(codes),
            GetCodesByCategoryError(var err) => Results.Problem(err.Message),
        };
    }
);

// ============================================================================
// ICD-10 CODE LOOKUP ENDPOINTS
// ============================================================================

icdGroup.MapGet(
    "/codes/{code}",
    async (string code, string? format, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetCodeByCodeAsync(code).ConfigureAwait(false);
        return result switch
        {
            GetCodeByCodeOk(var codes) when codes.Count > 0 => format == "fhir"
                ? Results.Ok(ToFhirCodeSystem(codes[0]))
                : Results.Ok(EnrichCodeWithDerivedHierarchy(codes[0])),
            GetCodeByCodeOk => Results.NotFound(),
            GetCodeByCodeError(var err) => Results.Problem(err.Message),
        };
    }
);

icdGroup.MapGet(
    "/codes",
    async (string q, int? limit, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var searchLimit = limit ?? 20;
        var searchTerm = $"%{q}%";

        // Manual search implementation (LIKE queries not supported by generator)
        // Use LEFT JOINs to handle databases with codes but no hierarchy
        // Returns all fields that CLI expects for proper deserialization
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT c.Id, c.Code, c.ShortDescription, c.LongDescription, c.Billable,
               cat.CategoryCode, cat.Title AS CategoryTitle,
               b.BlockCode, b.Title AS BlockTitle,
               ch.ChapterNumber, ch.Title AS ChapterTitle,
               c.InclusionTerms, c.ExclusionTerms, c.CodeAlso, c.CodeFirst, c.Synonyms, c.Edition
        FROM icd10_code c
        LEFT JOIN icd10_category cat ON c.CategoryId = cat.Id
        LEFT JOIN icd10_block b ON cat.BlockId = b.Id
        LEFT JOIN icd10_chapter ch ON b.ChapterId = ch.Id
        WHERE c.Code LIKE @term OR c.ShortDescription LIKE @term OR c.LongDescription LIKE @term
        ORDER BY c.Code
        LIMIT @limit
        """;
        cmd.Parameters.AddWithValue("@term", searchTerm);
        cmd.Parameters.AddWithValue("@limit", searchLimit);

        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        var results = new List<object>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var code = reader.GetString(1);

            // Read nullable hierarchy fields
            var catCodeNull = await reader.IsDBNullAsync(5).ConfigureAwait(false);
            var catTitleNull = await reader.IsDBNullAsync(6).ConfigureAwait(false);
            var blockCodeNull = await reader.IsDBNullAsync(7).ConfigureAwait(false);
            var blockTitleNull = await reader.IsDBNullAsync(8).ConfigureAwait(false);
            var chapNumNull = await reader.IsDBNullAsync(9).ConfigureAwait(false);
            var chapTitleNull = await reader.IsDBNullAsync(10).ConfigureAwait(false);

            // Derive hierarchy from code prefix when DB values are null
            var (derivedChapNum, derivedChapTitle) = chapNumNull
                ? Icd10Chapters.GetChapter(code)
                : (reader.GetString(9), chapTitleNull ? "" : reader.GetString(10));
            var derivedCatCode = catCodeNull ? Icd10Chapters.GetCategory(code) : reader.GetString(5);
            var (derivedBlockCode, derivedBlockTitle) = blockCodeNull
                ? Icd10Chapters.GetBlock(code)
                : (reader.GetString(7), blockTitleNull ? "" : reader.GetString(8));

            results.Add(
                new
                {
                    Id = reader.GetString(0),
                    Code = code,
                    ShortDescription = reader.GetString(2),
                    LongDescription = reader.GetString(3),
                    Billable = reader.GetInt64(4),
                    CategoryCode = derivedCatCode,
                    CategoryTitle = catTitleNull ? "" : reader.GetString(6),
                    BlockCode = derivedBlockCode,
                    BlockTitle = derivedBlockTitle,
                    ChapterNumber = derivedChapNum,
                    ChapterTitle = derivedChapTitle,
                    InclusionTerms = await reader.IsDBNullAsync(11).ConfigureAwait(false) ? "" : reader.GetString(11),
                    ExclusionTerms = await reader.IsDBNullAsync(12).ConfigureAwait(false) ? "" : reader.GetString(12),
                    CodeAlso = await reader.IsDBNullAsync(13).ConfigureAwait(false) ? "" : reader.GetString(13),
                    CodeFirst = await reader.IsDBNullAsync(14).ConfigureAwait(false) ? "" : reader.GetString(14),
                    Synonyms = await reader.IsDBNullAsync(15).ConfigureAwait(false) ? "" : reader.GetString(15),
                    Edition = await reader.IsDBNullAsync(16).ConfigureAwait(false) ? "" : reader.GetString(16),
                }
            );
        }
        return Results.Ok(results);
    }
);

// ============================================================================
// ACHI PROCEDURE ENDPOINTS
// ============================================================================

var achiGroup = app.MapGroup("/api/achi").WithTags("ACHI");

achiGroup.MapGet(
    "/blocks",
    async (Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetAchiBlocksAsync().ConfigureAwait(false);
        return result switch
        {
            GetAchiBlocksOk(var blocks) => Results.Ok(blocks),
            GetAchiBlocksError(var err) => Results.Problem(err.Message),
        };
    }
);

achiGroup.MapGet(
    "/blocks/{blockId}/codes",
    async (string blockId, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetAchiCodesByBlockAsync(blockId).ConfigureAwait(false);
        return result switch
        {
            GetAchiCodesByBlockOk(var codes) => Results.Ok(codes),
            GetAchiCodesByBlockError(var err) => Results.Problem(err.Message),
        };
    }
);

achiGroup.MapGet(
    "/codes/{code}",
    async (string code, string? format, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetAchiCodeByCodeAsync(code).ConfigureAwait(false);
        return result switch
        {
            GetAchiCodeByCodeOk(var codes) when codes.Count > 0 => format == "fhir"
                ? Results.Ok(ToFhirProcedure(codes[0]))
                : Results.Ok(codes[0]),
            GetAchiCodeByCodeOk => Results.NotFound(),
            GetAchiCodeByCodeError(var err) => Results.Problem(err.Message),
        };
    }
);

achiGroup.MapGet(
    "/codes",
    async (string q, int? limit, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var searchLimit = limit ?? 20;
        var searchTerm = $"%{q}%";

        // Manual search implementation (LIKE queries not supported by generator)
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT Id, BlockId, Code, ShortDescription, LongDescription, Billable
        FROM achi_code
        WHERE Code LIKE @term OR ShortDescription LIKE @term OR LongDescription LIKE @term
        ORDER BY Code
        LIMIT @limit
        """;
        cmd.Parameters.AddWithValue("@term", searchTerm);
        cmd.Parameters.AddWithValue("@limit", searchLimit);

        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        var results = new List<object>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            results.Add(
                new
                {
                    Id = reader.GetString(0),
                    BlockId = reader.GetString(1),
                    Code = reader.GetString(2),
                    ShortDescription = reader.GetString(3),
                    LongDescription = reader.GetString(4),
                    Billable = reader.GetInt64(5),
                }
            );
        }
        return Results.Ok(results);
    }
);

// ============================================================================
// RAG SEARCH ENDPOINT - CALLS DOCKER EMBEDDING SERVICE
// ============================================================================

app.MapPost(
    "/api/search",
    async (
        RagSearchRequest request,
        Func<SqliteConnection> getConn,
        IHttpClientFactory httpClientFactory
    ) =>
    {
        var limit = request.Limit ?? 10;

        // Get embedding from Docker service
        var embeddingClient = httpClientFactory.CreateClient("EmbeddingService");
        var embeddingResponse = await embeddingClient
            .PostAsJsonAsync("/embed", new { text = request.Query })
            .ConfigureAwait(false);

        if (!embeddingResponse.IsSuccessStatusCode)
        {
            return Results.Problem("Embedding service unavailable");
        }

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var embeddingResult = await embeddingResponse
            .Content.ReadFromJsonAsync<EmbeddingResponse>(jsonOptions)
            .ConfigureAwait(false);

        if (embeddingResult is null)
        {
            return Results.Problem("Invalid embedding response");
        }

        var queryVector = embeddingResult.Embedding.ToImmutableArray();

        using var conn = getConn();

        // Get ICD-10 embeddings
        var allEmbeddingsResult = await conn.GetAllCodeEmbeddingsAsync().ConfigureAwait(false);

        if (allEmbeddingsResult is GetAllCodeEmbeddingsError(var err))
        {
            return Results.Problem(err.Message);
        }

        var allEmbeddings = ((GetAllCodeEmbeddingsOk)allEmbeddingsResult).Value;

        // Compute cosine similarity for ICD codes
        var icdResults = allEmbeddings.Select(e =>
        {
            var storedVector = ParseEmbedding(e.Embedding);
            var similarity = CosineSimilarity(queryVector, storedVector);
            var (chapterNum, chapterTitle) = Icd10Chapters.GetChapter(e.Code);
            var category = Icd10Chapters.GetCategory(e.Code);
            return new SearchResult(
                Code: e.Code,
                Description: e.ShortDescription,
                LongDescription: e.LongDescription,
                Confidence: similarity,
                CodeType: "ICD10",
                Chapter: chapterNum,
                ChapterTitle: chapterTitle,
                Category: category,
                InclusionTerms: e.InclusionTerms,
                ExclusionTerms: e.ExclusionTerms,
                CodeAlso: e.CodeAlso,
                CodeFirst: e.CodeFirst
            );
        });

        // Include ACHI if requested
        var achiResults = Enumerable.Empty<SearchResult>();
        if (request.IncludeAchi)
        {
            var cmd = conn.CreateCommand();
            await using (cmd.ConfigureAwait(false))
            {
                cmd.CommandText = """
                SELECT e.Embedding, c.Code, c.ShortDescription, c.LongDescription
                FROM achi_code_embedding e
                INNER JOIN achi_code c ON e.CodeId = c.Id
                """;
                var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var achiEmbeddings =
                        new List<(
                            string Embedding,
                            string Code,
                            string ShortDesc,
                            string LongDesc
                        )>();
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        achiEmbeddings.Add(
                            (
                                reader.GetString(0),
                                reader.GetString(1),
                                reader.GetString(2),
                                reader.GetString(3)
                            )
                        );
                    }

                    achiResults = achiEmbeddings.Select(e =>
                    {
                        var storedVector = ParseEmbedding(e.Embedding);
                        var similarity = CosineSimilarity(queryVector, storedVector);
                        return new SearchResult(
                            Code: e.Code,
                            Description: e.ShortDesc,
                            LongDescription: e.LongDesc,
                            Confidence: similarity,
                            CodeType: "ACHI",
                            Chapter: "",
                            ChapterTitle: "",
                            Category: "",
                            InclusionTerms: "",
                            ExclusionTerms: "",
                            CodeAlso: "",
                            CodeFirst: ""
                        );
                    });
                }
            }
        }

        // Combine and rank all results
        var rankedResults = icdResults
            .Concat(achiResults)
            .OrderByDescending(r => r.Confidence)
            .Take(limit)
            .ToList();

        var response =
            request.Format == "fhir"
                ? (object)
                    new
                    {
                        ResourceType = "Bundle",
                        Type = "searchset",
                        Total = rankedResults.Count,
                        Entry = rankedResults
                            .Select(r => new
                            {
                                Resource = new
                                {
                                    ResourceType = "CodeSystem",
                                    Url = "http://hl7.org/fhir/sid/icd-10",
                                    Concept = new { Code = r.Code, Display = r.Description },
                                },
                                Search = new { Score = r.Confidence },
                            })
                            .ToList(),
                    }
                : new
                {
                    Results = rankedResults,
                    Query = request.Query,
                    Model = "MedEmbed-Small-v0.1",
                };

        return Results.Ok(response);
    }
);

app.MapGet(
    "/health",
    (Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM icd10_code";
        var count = Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

        return count > 0
            ? Results.Ok(new { Status = "healthy", Service = "ICD10.Api", CodesLoaded = count })
            : Results.Json(
                new { Status = "unhealthy", Service = "ICD10.Api", Error = "No ICD-10 codes loaded" },
                statusCode: 503
            );
    }
);

app.Run();

// ============================================================================
// HELPER METHODS
// ============================================================================

/// <summary>
/// Enriches a code record with derived hierarchy info when DB values are null.
/// Uses Icd10Chapters to derive chapter/category from code prefix.
/// </summary>
static GetCodeByCode EnrichCodeWithDerivedHierarchy(GetCodeByCode code)
{
    var (chapterNum, chapterTitle) = string.IsNullOrEmpty(code.ChapterNumber)
        ? ICD10.Api.Icd10Chapters.GetChapter(code.Code)
        : (code.ChapterNumber, code.ChapterTitle ?? "");

    var categoryCode = string.IsNullOrEmpty(code.CategoryCode)
        ? ICD10.Api.Icd10Chapters.GetCategory(code.Code)
        : code.CategoryCode;

    // Derive block from category when not in DB - use category code as pseudo-block
    var (blockCode, blockTitle) = string.IsNullOrEmpty(code.BlockCode)
        ? ICD10.Api.Icd10Chapters.GetBlock(code.Code)
        : (code.BlockCode, code.BlockTitle ?? "");

    return code with
    {
        CategoryCode = categoryCode,
        CategoryTitle = code.CategoryTitle ?? "",
        BlockCode = blockCode,
        BlockTitle = blockTitle,
        ChapterNumber = chapterNum,
        ChapterTitle = chapterTitle,
    };
}

static object ToFhirCodeSystem(GetCodeByCode code) =>
    new
    {
        ResourceType = "CodeSystem",
        Url = "http://hl7.org/fhir/sid/icd-10",
        Version = "13",
        Concept = new
        {
            Code = code.Code,
            Display = code.ShortDescription,
            Definition = code.LongDescription,
        },
        Property = new[]
        {
            new { Code = "chapter", ValueString = code.ChapterNumber },
            new { Code = "block", ValueString = code.BlockCode },
            new { Code = "category", ValueString = code.CategoryCode },
        },
    };

static object ToFhirProcedure(GetAchiCodeByCode code) =>
    new
    {
        ResourceType = "CodeSystem",
        Url = "http://hl7.org/fhir/sid/achi",
        Concept = new
        {
            Code = code.Code,
            Display = code.ShortDescription,
            Definition = code.LongDescription,
        },
        Property = new[] { new { Code = "block", ValueString = code.BlockNumber } },
    };

static ImmutableArray<float> ParseEmbedding(string embeddingJson)
{
    try
    {
        var values = JsonSerializer.Deserialize<float[]>(embeddingJson);
        return values is null ? [] : [.. values];
    }
    catch (JsonException)
    {
        // Invalid JSON for embedding, return empty array
        return [];
    }
}

static double CosineSimilarity(ImmutableArray<float> a, ImmutableArray<float> b)
{
    if (a.Length != b.Length || a.Length == 0)
    {
        return 0;
    }

    double dotProduct = 0;
    double normA = 0;
    double normB = 0;

    for (var i = 0; i < a.Length; i++)
    {
        dotProduct += a[i] * b[i];
        normA += a[i] * a[i];
        normB += b[i] * b[i];
    }

    var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
    return denominator == 0 ? 0 : dotProduct / denominator;
}

namespace ICD10.Api
{
    /// <summary>
    /// Request for RAG search.
    /// </summary>
    internal sealed record RagSearchRequest(
        string Query,
        int? Limit,
        bool IncludeAchi,
        string? Format
    );

    /// <summary>
    /// Response from embedding service.
    /// </summary>
    internal sealed record EmbeddingResponse(
        ImmutableArray<float> Embedding,
        string Model,
        int Dimensions
    );

    /// <summary>
    /// Semantic search result with code type, hierarchy, and clinical details.
    /// </summary>
    internal sealed record SearchResult(
        string Code,
        string Description,
        string LongDescription,
        double Confidence,
        string CodeType,
        string Chapter,
        string ChapterTitle,
        string Category,
        string InclusionTerms,
        string ExclusionTerms,
        string CodeAlso,
        string CodeFirst
    );

    /// <summary>
    /// ICD-10 chapter lookup based on code prefix.
    /// Official WHO/CDC chapter ranges.
    /// </summary>
    internal static class Icd10Chapters
    {
        private static readonly FrozenDictionary<
            string,
            (string Number, string Title)
        > ChapterLookup = new Dictionary<string, (string, string)>
        {
            { "A", ("1", "Certain infectious and parasitic diseases") },
            { "B", ("1", "Certain infectious and parasitic diseases") },
            { "C", ("2", "Neoplasms") },
            { "D0", ("2", "Neoplasms") },
            { "D1", ("2", "Neoplasms") },
            { "D2", ("2", "Neoplasms") },
            { "D3", ("2", "Neoplasms") },
            { "D4", ("2", "Neoplasms") },
            { "D5", ("3", "Diseases of the blood and blood-forming organs") },
            { "D6", ("3", "Diseases of the blood and blood-forming organs") },
            { "D7", ("3", "Diseases of the blood and blood-forming organs") },
            { "D8", ("3", "Diseases of the blood and blood-forming organs") },
            { "D89", ("3", "Diseases of the blood and blood-forming organs") },
            { "E", ("4", "Endocrine, nutritional and metabolic diseases") },
            { "F", ("5", "Mental, behavioral and neurodevelopmental disorders") },
            { "G", ("6", "Diseases of the nervous system") },
            { "H0", ("7", "Diseases of the eye and adnexa") },
            { "H1", ("7", "Diseases of the eye and adnexa") },
            { "H2", ("7", "Diseases of the eye and adnexa") },
            { "H3", ("7", "Diseases of the eye and adnexa") },
            { "H4", ("7", "Diseases of the eye and adnexa") },
            { "H5", ("7", "Diseases of the eye and adnexa") },
            { "H6", ("8", "Diseases of the ear and mastoid process") },
            { "H7", ("8", "Diseases of the ear and mastoid process") },
            { "H8", ("8", "Diseases of the ear and mastoid process") },
            { "H9", ("8", "Diseases of the ear and mastoid process") },
            { "I", ("9", "Diseases of the circulatory system") },
            { "J", ("10", "Diseases of the respiratory system") },
            { "K", ("11", "Diseases of the digestive system") },
            { "L", ("12", "Diseases of the skin and subcutaneous tissue") },
            { "M", ("13", "Diseases of the musculoskeletal system and connective tissue") },
            { "N", ("14", "Diseases of the genitourinary system") },
            { "O", ("15", "Pregnancy, childbirth and the puerperium") },
            { "P", ("16", "Certain conditions originating in the perinatal period") },
            { "Q", ("17", "Congenital malformations and chromosomal abnormalities") },
            { "R", ("18", "Symptoms, signs and abnormal clinical findings") },
            { "S", ("19", "Injury, poisoning and external causes") },
            { "T", ("19", "Injury, poisoning and external causes") },
            { "V", ("20", "External causes of morbidity") },
            { "W", ("20", "External causes of morbidity") },
            { "X", ("20", "External causes of morbidity") },
            { "Y", ("20", "External causes of morbidity") },
            { "Z", ("21", "Factors influencing health status and contact with health services") },
        }.ToFrozenDictionary();

        /// <summary>
        /// Gets the chapter number and title for an ICD-10 code.
        /// </summary>
        public static (string Number, string Title) GetChapter(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return ("", "");
            }

            // Try 2-character prefix first (for D codes with specific ranges)
            if (
                code.Length >= 2
                && ChapterLookup.TryGetValue(code[..2].ToUpperInvariant(), out var chapter2)
            )
            {
                return chapter2;
            }

            // Fall back to 1-character prefix
            if (
                code.Length >= 1
                && ChapterLookup.TryGetValue(code[..1].ToUpperInvariant(), out var chapter1)
            )
            {
                return chapter1;
            }

            return ("", "");
        }

        /// <summary>
        /// Gets the category (first 3 characters) for an ICD-10 code.
        /// </summary>
        public static string GetCategory(string code) =>
            string.IsNullOrEmpty(code) ? ""
            : code.Length >= 3 ? code[..3].ToUpperInvariant()
            : code.ToUpperInvariant();

        /// <summary>
        /// Gets the block code and title for an ICD-10 code.
        /// Derives block range from category prefix.
        /// </summary>
        public static (string Code, string Title) GetBlock(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length < 3)
            {
                return ("", "");
            }

            var category = code[..3].ToUpperInvariant();

            // Common ICD-10-CM block ranges (simplified)
            return category switch
            {
                // Eye blocks (Chapter 7)
                var c when c.StartsWith("H53", StringComparison.Ordinal) || c.StartsWith("H54", StringComparison.Ordinal)
                    => ("H53-H54", "Visual disturbances and blindness"),
                var c when c.StartsWith("H49", StringComparison.Ordinal) || c.StartsWith("H50", StringComparison.Ordinal)
                    || c.StartsWith("H51", StringComparison.Ordinal) || c.StartsWith("H52", StringComparison.Ordinal)
                    => ("H49-H52", "Disorders of ocular muscles and binocular movement"),
                // Congenital malformations (Chapter 17)
                var c when c.StartsWith("Q50", StringComparison.Ordinal) || c.StartsWith("Q51", StringComparison.Ordinal)
                    || c.StartsWith("Q52", StringComparison.Ordinal) || c.StartsWith("Q53", StringComparison.Ordinal)
                    || c.StartsWith("Q54", StringComparison.Ordinal) || c.StartsWith("Q55", StringComparison.Ordinal)
                    || c.StartsWith("Q56", StringComparison.Ordinal)
                    => ("Q50-Q56", "Congenital malformations of genital organs"),
                // Default: use category as pseudo-block
                _ => (category, ""),
            };
        }
    }

    /// <summary>
    /// Program entry point marker for WebApplicationFactory.
    /// </summary>
    public partial class Program { }
}
