#pragma warning disable IDE0037 // Use inferred member name - prefer explicit for clarity in API responses
#pragma warning disable CA1812 // Avoid uninstantiated internal classes - records are instantiated by JSON deserialization

using System.Net.Http.Json;
using System.Text.Json;
using ICD10AM.Api;
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
var dbPath =
    builder.Configuration["DbPath"] ?? Path.Combine(AppContext.BaseDirectory, "icd10am.db");
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
// ICD-10-AM HIERARCHICAL BROWSE ENDPOINTS
// ============================================================================

var icdGroup = app.MapGroup("/api/icd10am").WithTags("ICD-10-AM");

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
// ICD-10-AM CODE LOOKUP ENDPOINTS
// ============================================================================

icdGroup.MapGet(
    "/codes/{code}",
    async (string code, string? format, Func<SqliteConnection> getConn) =>
    {
        using var conn = getConn();
        var result = await conn.GetCodeByCodeAsync(code).ConfigureAwait(false);
        return result switch
        {
            GetCodeByCodeOk(var codes) when codes.Count > 0 =>
                format == "fhir"
                    ? Results.Ok(ToFhirCodeSystem(codes[0]))
                    : Results.Ok(codes[0]),
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
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.Id, c.Code, c.ShortDescription, c.LongDescription, c.Billable,
                   cat.CategoryCode, b.BlockCode, ch.ChapterNumber, ch.Title AS ChapterTitle
            FROM icd10am_code c
            INNER JOIN icd10am_category cat ON c.CategoryId = cat.Id
            INNER JOIN icd10am_block b ON cat.BlockId = b.Id
            INNER JOIN icd10am_chapter ch ON b.ChapterId = ch.Id
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
            results.Add(new
            {
                Id = reader.GetString(0),
                Code = reader.GetString(1),
                ShortDescription = reader.GetString(2),
                LongDescription = reader.GetString(3),
                Billable = reader.GetInt64(4),
                CategoryCode = reader.GetString(5),
                BlockCode = reader.GetString(6),
                ChapterNumber = reader.GetString(7),
                ChapterTitle = reader.GetString(8),
            });
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
            GetAchiCodeByCodeOk(var codes) when codes.Count > 0 =>
                format == "fhir"
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
            results.Add(new
            {
                Id = reader.GetString(0),
                BlockId = reader.GetString(1),
                Code = reader.GetString(2),
                ShortDescription = reader.GetString(3),
                LongDescription = reader.GetString(4),
                Billable = reader.GetInt64(5),
            });
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

        // Get all embeddings and compute similarity (via LQL)
        var allEmbeddingsResult = await conn.GetAllCodeEmbeddingsAsync().ConfigureAwait(false);

        if (allEmbeddingsResult is GetAllCodeEmbeddingsError(var err))
        {
            return Results.Problem(err.Message);
        }

        var allEmbeddings = ((GetAllCodeEmbeddingsOk)allEmbeddingsResult).Value;

        // Compute cosine similarity for each code and rank
        var rankedResults = allEmbeddings
            .Select(e =>
            {
                var storedVector = ParseEmbedding(e.Embedding);
                var similarity = CosineSimilarity(queryVector, storedVector);
                return new
                {
                    Code = e.Code,
                    Description = e.ShortDescription,
                    LongDescription = e.LongDescription,
                    Confidence = similarity,
                };
            })
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
                                    Url = "http://hl7.org/fhir/sid/icd-10-am",
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

app.MapGet("/health", () => Results.Ok(new { Status = "healthy", Service = "ICD10AM.Api" }));

app.Run();

// ============================================================================
// HELPER METHODS
// ============================================================================

static object ToFhirCodeSystem(GetCodeByCode code) =>
    new
    {
        ResourceType = "CodeSystem",
        Url = "http://hl7.org/fhir/sid/icd-10-am",
        Version = code.Edition.ToString(System.Globalization.CultureInfo.InvariantCulture),
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

namespace ICD10AM.Api
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
    /// Program entry point marker for WebApplicationFactory.
    /// </summary>
    public partial class Program { }
}
