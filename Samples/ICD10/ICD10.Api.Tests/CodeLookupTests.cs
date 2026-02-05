namespace ICD10.Api.Tests;

/// <summary>
/// E2E tests for ICD-10-CM code lookup endpoints - REAL database, NO mocks.
/// </summary>
public sealed class CodeLookupTests : IClassFixture<ICD10ApiFactory>
{
    private readonly HttpClient _client;

    public CodeLookupTests(ICD10ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCodeByCode_ReturnsOk_WhenCodeExists()
    {
        var response = await _client.GetAsync("/api/icd10/codes/A00.0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCodeByCode_ReturnsCodeDetails()
    {
        var response = await _client.GetAsync("/api/icd10/codes/A00.0");
        var code = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("A00.0", code.GetProperty("Code").GetString());
        Assert.Contains("Cholera", code.GetProperty("ShortDescription").GetString());
        // Chapter number is numeric in ICD-10-CM XML (1, not I)
        Assert.Equal("1", code.GetProperty("ChapterNumber").GetString());
    }

    [Fact]
    public async Task GetCodeByCode_ReturnsNotFound_WhenCodeNotExists()
    {
        var response = await _client.GetAsync("/api/icd10/codes/INVALID99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCodeByCode_ReturnsFhirFormat_WhenRequested()
    {
        // Use A00.0 which exists in the database (R07.4 doesn't exist in ICD-10-CM 2025)
        var response = await _client.GetAsync("/api/icd10/codes/A00.0?format=fhir");
        var content = await response.Content.ReadAsStringAsync();
        var fhir = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.Equal("CodeSystem", fhir.GetProperty("ResourceType").GetString());
        Assert.Equal("http://hl7.org/fhir/sid/icd-10", fhir.GetProperty("Url").GetString());
        Assert.Equal("A00.0", fhir.GetProperty("Concept").GetProperty("Code").GetString());
    }

    [Fact]
    public async Task SearchCodes_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/icd10/codes?q=cholera");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SearchCodes_FindsMatchingCodes()
    {
        var response = await _client.GetAsync("/api/icd10/codes?q=cholera");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.NotEmpty(codes);
        Assert.Contains(codes, c => c.GetProperty("Code").GetString() == "A00.0");
    }

    [Fact]
    public async Task SearchCodes_RespectsLimit()
    {
        var response = await _client.GetAsync("/api/icd10/codes?q=a&limit=1");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.True(codes.Length <= 1, "Expected at most 1 result with limit=1");
    }

    [Fact]
    public async Task SearchCodes_SearchesByCode()
    {
        var response = await _client.GetAsync("/api/icd10/codes?q=R07");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.Contains(
            codes,
            c => c.GetProperty("Code").GetString()!.StartsWith("R07", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task SearchCodes_ReturnsEmptyArray_WhenNoMatch()
    {
        var response = await _client.GetAsync("/api/icd10/codes?q=zzznomatch");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.Empty(codes);
    }

    [Fact]
    public async Task GetCategoriesByBlock_ReturnsCategories()
    {
        // First get a real block ID from the database
        var chaptersResponse = await _client.GetAsync("/api/icd10/chapters");
        var chapters = await chaptersResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        var chapter1 = chapters!.First(c => c.GetProperty("ChapterNumber").GetString() == "1");
        var chapterId = chapter1.GetProperty("Id").GetString();

        var blocksResponse = await _client.GetAsync($"/api/icd10/chapters/{chapterId}/blocks");
        var blocks = await blocksResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        var blockA00 = blocks!.First(b => b.GetProperty("BlockCode").GetString() == "A00-A09");
        var blockId = blockA00.GetProperty("Id").GetString();

        var response = await _client.GetAsync($"/api/icd10/blocks/{blockId}/categories");
        var categories = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(categories);
        Assert.NotEmpty(categories);
        Assert.Contains(categories, c => c.GetProperty("CategoryCode").GetString() == "A00");
    }

    [Fact]
    public async Task GetCodesByCategory_ReturnsCodes()
    {
        // First get a real category ID from the database
        var chaptersResponse = await _client.GetAsync("/api/icd10/chapters");
        var chapters = await chaptersResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        var chapter1 = chapters!.First(c => c.GetProperty("ChapterNumber").GetString() == "1");
        var chapterId = chapter1.GetProperty("Id").GetString();

        var blocksResponse = await _client.GetAsync($"/api/icd10/chapters/{chapterId}/blocks");
        var blocks = await blocksResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        var blockA00 = blocks!.First(b => b.GetProperty("BlockCode").GetString() == "A00-A09");
        var blockId = blockA00.GetProperty("Id").GetString();

        var categoriesResponse = await _client.GetAsync($"/api/icd10/blocks/{blockId}/categories");
        var categories = await categoriesResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        var catA00 = categories!.First(c => c.GetProperty("CategoryCode").GetString() == "A00");
        var categoryId = catA00.GetProperty("Id").GetString();

        var response = await _client.GetAsync($"/api/icd10/categories/{categoryId}/codes");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.NotEmpty(codes);
        Assert.Contains(codes, c => c.GetProperty("Code").GetString() == "A00.0");
    }

    // =========================================================================
    // ICD-10-CM LOOKUP TESTS (codes returned by RAG search)
    // =========================================================================

    [Fact]
    public async Task Icd10Cm_GetCodeByCode_ReturnsOk_WhenCodeExists()
    {
        var response = await _client.GetAsync("/api/icd10/codes/I10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Icd10Cm_GetCodeByCode_ReturnsAllDetails()
    {
        var response = await _client.GetAsync("/api/icd10/codes/I10");
        var code = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Verify ALL required fields are present and populated
        Assert.Equal("I10", code.GetProperty("Code").GetString());
        Assert.False(
            string.IsNullOrEmpty(code.GetProperty("ShortDescription").GetString()),
            "ShortDescription must not be empty"
        );
        Assert.False(
            string.IsNullOrEmpty(code.GetProperty("LongDescription").GetString()),
            "LongDescription must not be empty"
        );
        Assert.True(code.TryGetProperty("Billable", out _), "Billable property must exist");
        Assert.True(code.TryGetProperty("Id", out _), "Id property must exist");

        // Verify specific content
        Assert.Contains(
            "hypertension",
            code.GetProperty("ShortDescription").GetString(),
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task Icd10Cm_GetCodeByCode_ReturnsNotFound_WhenCodeNotExists()
    {
        var response = await _client.GetAsync("/api/icd10/codes/INVALID99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Icd10Cm_GetCodeByCode_ReturnsFhirFormat_WhenRequested()
    {
        var response = await _client.GetAsync("/api/icd10/codes/I10?format=fhir");
        var fhir = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("CodeSystem", fhir.GetProperty("ResourceType").GetString());
        Assert.Equal("http://hl7.org/fhir/sid/icd-10", fhir.GetProperty("Url").GetString());
        Assert.Equal("I10", fhir.GetProperty("Concept").GetProperty("Code").GetString());
    }

    [Fact]
    public async Task Icd10Cm_SearchCodes_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/icd10/codes?q=hypertension");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Icd10Cm_SearchCodes_FindsMatchingCodes()
    {
        var response = await _client.GetAsync("/api/icd10/codes?q=hypertension");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.NotEmpty(codes);
        Assert.Contains(codes, c => c.GetProperty("Code").GetString() == "I10");
    }

    [Fact]
    public async Task Icd10Cm_SearchCodes_RespectsLimit()
    {
        var response = await _client.GetAsync("/api/icd10/codes?q=a&limit=1");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.True(codes.Length <= 1, "Expected at most 1 result with limit=1");
    }

    [Fact]
    public async Task Icd10Cm_LookupCodeReturnedByRagSearch_Succeeds()
    {
        // This test verifies the critical bug fix:
        // Codes returned by RAG search (from icd10_code_embedding) MUST be lookupable
        // via /api/icd10/codes/{code}

        // I21.11 exists in the database
        var response = await _client.GetAsync("/api/icd10/codes/I21.11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var code = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("I21.11", code.GetProperty("Code").GetString());
        Assert.Contains(
            "myocardial infarction",
            code.GetProperty("ShortDescription").GetString(),
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task Icd10Cm_LookupCommonCodes_AllSucceed()
    {
        // Verify common ICD-10-CM codes can be looked up (these all exist in CDC 2025 data)
        var commonCodes = new[]
        {
            ("A00.0", "cholera"),
            ("E10.9", "diabetes"),
            ("E11.9", "diabetes"),
            ("I10", "hypertension"),
            ("I21.0", "myocardial infarction"),
            ("I21.11", "myocardial infarction"),
            ("I21.4", "myocardial infarction"),
            ("J06.9", "respiratory infection"),
            ("R06.00", "dyspnea"),
            ("R07.9", "chest pain"),  // R07.4 doesn't exist, R07.9 does
            ("R07.89", "chest pain"),
        };

        foreach (var (code, expectedDescription) in commonCodes)
        {
            var response = await _client.GetAsync($"/api/icd10/codes/{code}");
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"Lookup failed for code {code}: {response.StatusCode}"
            );

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(code, result.GetProperty("Code").GetString());
            Assert.Contains(
                expectedDescription,
                result.GetProperty("ShortDescription").GetString(),
                StringComparison.OrdinalIgnoreCase
            );
        }
    }
}
