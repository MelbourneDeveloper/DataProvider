namespace ICD10.Api.Tests;

/// <summary>
/// E2E tests for ICD-10-CM chapter endpoints - REAL database, NO mocks.
/// </summary>
public sealed class ChapterEndpointTests : IClassFixture<ICD10ApiFactory>
{
    private readonly HttpClient _client;

    public ChapterEndpointTests(ICD10ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetChapters_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/icd10/chapters");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetChapters_ReturnsChaptersFromDatabase()
    {
        var response = await _client.GetAsync("/api/icd10/chapters");
        var chapters = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(chapters);
        Assert.True(
            chapters.Length >= 20,
            $"Expected at least 20 chapters from ICD-10-CM, got {chapters.Length}"
        );
        // ICD-10-CM uses numeric chapter numbers (1, 2, 3... not Roman numerals)
        Assert.Contains(chapters, c => c.GetProperty("ChapterNumber").GetString() == "1");
        Assert.Contains(chapters, c => c.GetProperty("ChapterNumber").GetString() == "18");
    }

    [Fact]
    public async Task GetChapters_ChaptersHaveRequiredFields()
    {
        var response = await _client.GetAsync("/api/icd10/chapters");
        var chapters = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(chapters);
        Assert.NotEmpty(chapters);

        var chapter = chapters[0];
        Assert.True(chapter.TryGetProperty("Id", out _), "Missing Id");
        Assert.True(chapter.TryGetProperty("ChapterNumber", out _), "Missing ChapterNumber");
        Assert.True(chapter.TryGetProperty("Title", out _), "Missing Title");
        Assert.True(chapter.TryGetProperty("CodeRangeStart", out _), "Missing CodeRangeStart");
        Assert.True(chapter.TryGetProperty("CodeRangeEnd", out _), "Missing CodeRangeEnd");
    }

    [Fact]
    public async Task GetBlocksByChapter_ReturnsOk_WhenChapterExists()
    {
        // First get a real chapter ID from the database
        var chaptersResponse = await _client.GetAsync("/api/icd10/chapters");
        var chapters = await chaptersResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(chapters);
        Assert.NotEmpty(chapters);

        var chapterId = chapters[0].GetProperty("Id").GetString();
        var response = await _client.GetAsync($"/api/icd10/chapters/{chapterId}/blocks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBlocksByChapter_ReturnsBlocks()
    {
        // Get chapter 1 (Infectious diseases) which has block A00-A09
        var chaptersResponse = await _client.GetAsync("/api/icd10/chapters");
        var chapters = await chaptersResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        var chapter1 = chapters!.First(c => c.GetProperty("ChapterNumber").GetString() == "1");
        var chapterId = chapter1.GetProperty("Id").GetString();

        var response = await _client.GetAsync($"/api/icd10/chapters/{chapterId}/blocks");
        var blocks = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(blocks);
        Assert.NotEmpty(blocks);
        Assert.Contains(blocks, b => b.GetProperty("BlockCode").GetString() == "A00-A09");
    }

    [Fact]
    public async Task GetBlocksByChapter_ReturnsEmptyArray_WhenChapterNotExists()
    {
        var response = await _client.GetAsync("/api/icd10/chapters/nonexistent-uuid/blocks");
        var blocks = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(blocks);
        Assert.Empty(blocks);
    }
}
