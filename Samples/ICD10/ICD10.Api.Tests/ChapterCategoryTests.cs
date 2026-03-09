namespace ICD10.Api.Tests;

/// <summary>
/// E2E tests proving Chapter and Category fields are returned in search results.
/// These tests verify the ICD-10 hierarchy information is properly included.
/// </summary>
public sealed class ChapterCategoryTests : IClassFixture<ICD10ApiFactory>
{
    private readonly HttpClient _client;
    private readonly ICD10ApiFactory _factory;

    /// <summary>
    /// Constructor receives shared API factory.
    /// </summary>
    public ChapterCategoryTests(ICD10ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// CRITICAL TEST: Search results include Chapter field for ICD-10 codes.
    /// </summary>
    [Fact]
    public async Task Search_ReturnsChapterField_ForAllResults()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "diabetes type 2", Limit = 5 }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var results = result.GetProperty("Results");

        Assert.True(results.GetArrayLength() > 0, "Should return at least one result");

        foreach (var item in results.EnumerateArray())
        {
            Assert.True(
                item.TryGetProperty("Chapter", out var chapter),
                "Each result should have Chapter field"
            );
            var chapterValue = chapter.GetString();
            Assert.False(
                string.IsNullOrEmpty(chapterValue),
                $"Chapter should not be empty for code {item.GetProperty("Code").GetString()}"
            );
        }
    }

    /// <summary>
    /// CRITICAL TEST: Search results include ChapterTitle field for ICD-10 codes.
    /// </summary>
    [Fact]
    public async Task Search_ReturnsChapterTitleField_ForAllResults()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "heart attack", Limit = 5 }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var results = result.GetProperty("Results");

        Assert.True(results.GetArrayLength() > 0, "Should return at least one result");

        foreach (var item in results.EnumerateArray())
        {
            Assert.True(
                item.TryGetProperty("ChapterTitle", out var chapterTitle),
                "Each result should have ChapterTitle field"
            );
            var titleValue = chapterTitle.GetString();
            Assert.False(
                string.IsNullOrEmpty(titleValue),
                $"ChapterTitle should not be empty for code {item.GetProperty("Code").GetString()}"
            );
        }
    }

    /// <summary>
    /// CRITICAL TEST: Search results include Category field for ICD-10 codes.
    /// </summary>
    [Fact]
    public async Task Search_ReturnsCategoryField_ForAllResults()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "pneumonia lung infection", Limit = 5 }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var results = result.GetProperty("Results");

        Assert.True(results.GetArrayLength() > 0, "Should return at least one result");

        foreach (var item in results.EnumerateArray())
        {
            Assert.True(
                item.TryGetProperty("Category", out var category),
                "Each result should have Category field"
            );
            var categoryValue = category.GetString();
            Assert.False(
                string.IsNullOrEmpty(categoryValue),
                $"Category should not be empty for code {item.GetProperty("Code").GetString()}"
            );
        }
    }

    /// <summary>
    /// CRITICAL TEST: Category is first 3 characters of ICD-10 code.
    /// </summary>
    [Fact]
    public async Task Search_CategoryMatchesCodePrefix()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "fracture broken bone", Limit = 10 }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var results = result.GetProperty("Results");

        foreach (var item in results.EnumerateArray())
        {
            var code = item.GetProperty("Code").GetString()!;
            var category = item.GetProperty("Category").GetString()!;

            // Category should be first 3 chars of code (uppercase)
            var expectedCategory =
                code.Length >= 3 ? code[..3].ToUpperInvariant() : code.ToUpperInvariant();

            Assert.True(
                expectedCategory == category,
                $"Category '{category}' should match first 3 chars of code '{code}'"
            );
        }
    }

    /// <summary>
    /// CRITICAL TEST: Chapter number is valid for known ICD-10 code prefixes.
    /// E codes (Endocrine) should be Chapter 4.
    /// </summary>
    [Fact]
    public async Task Search_DiabetesCodes_HaveChapter4()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "diabetes mellitus type 2", Limit = 10 }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var results = result.GetProperty("Results");

        var diabetesCodes = new List<(string Code, string Chapter)>();
        foreach (var item in results.EnumerateArray())
        {
            var code = item.GetProperty("Code").GetString()!;
            var chapter = item.GetProperty("Chapter").GetString()!;

            // E10-E14 are diabetes codes, should be Chapter 4
            if (code.StartsWith("E1", StringComparison.OrdinalIgnoreCase))
            {
                diabetesCodes.Add((code, chapter));
            }
        }

        Assert.True(
            diabetesCodes.Count > 0,
            "Should find at least one E1x (diabetes) code in results"
        );

        foreach (var (code, chapter) in diabetesCodes)
        {
            Assert.True(
                chapter == "4",
                $"Diabetes code {code} should be Chapter 4 (Endocrine), got Chapter {chapter}"
            );
        }
    }

    /// <summary>
    /// CRITICAL TEST: Chapter titles are descriptive and match WHO ICD-10 chapters.
    /// </summary>
    [Fact]
    public async Task Search_ChapterTitles_AreDescriptive()
    {
        SkipIfEmbeddingServiceUnavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/search",
            new { Query = "infection bacterial viral", Limit = 10 }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var results = result.GetProperty("Results");

        foreach (var item in results.EnumerateArray())
        {
            var code = item.GetProperty("Code").GetString()!;
            var chapterTitle = item.GetProperty("ChapterTitle").GetString()!;

            // Chapter titles should be descriptive (more than 5 chars)
            Assert.True(
                chapterTitle.Length > 5,
                $"ChapterTitle '{chapterTitle}' for code {code} should be descriptive"
            );

            // A/B codes (infections) should have "infectious" in chapter title
            if (
                code.StartsWith('A')
                || code.StartsWith('a')
                || code.StartsWith('B')
                || code.StartsWith('b')
            )
            {
                Assert.True(
                    chapterTitle.Contains("infectious", StringComparison.OrdinalIgnoreCase),
                    $"Code {code} chapter should mention 'infectious', got '{chapterTitle}'"
                );
            }
        }
    }

    private void SkipIfEmbeddingServiceUnavailable()
    {
        if (!_factory.EmbeddingServiceAvailable)
        {
            Assert.Fail(
                "EMBEDDING SERVICE NOT RUNNING! "
                    + "Start it with: ./scripts/Dependencies/start.sh "
                    + "(localhost:8000 must be available for RAG E2E tests)"
            );
        }
    }
}
