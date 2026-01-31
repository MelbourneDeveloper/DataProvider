namespace ICD10AM.Api.Tests;

/// <summary>
/// E2E tests for ICD-10-AM code lookup endpoints - REAL database, NO mocks.
/// </summary>
public sealed class CodeLookupTests : IClassFixture<ICD10AMApiFactory>
{
    private readonly HttpClient _client;

    public CodeLookupTests(ICD10AMApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCodeByCode_ReturnsOk_WhenCodeExists()
    {
        var response = await _client.GetAsync("/api/icd10am/codes/A00.0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCodeByCode_ReturnsCodeDetails()
    {
        var response = await _client.GetAsync("/api/icd10am/codes/A00.0");
        var code = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("A00.0", code.GetProperty("Code").GetString());
        Assert.Contains("Cholera", code.GetProperty("ShortDescription").GetString());
        Assert.Equal("I", code.GetProperty("ChapterNumber").GetString());
    }

    [Fact]
    public async Task GetCodeByCode_ReturnsNotFound_WhenCodeNotExists()
    {
        var response = await _client.GetAsync("/api/icd10am/codes/INVALID99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCodeByCode_ReturnsFhirFormat_WhenRequested()
    {
        var response = await _client.GetAsync("/api/icd10am/codes/R07.4?format=fhir");
        var content = await response.Content.ReadAsStringAsync();
        var fhir = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.Equal("CodeSystem", fhir.GetProperty("ResourceType").GetString());
        Assert.Equal("http://hl7.org/fhir/sid/icd-10-am", fhir.GetProperty("Url").GetString());
        Assert.Equal("R07.4", fhir.GetProperty("Concept").GetProperty("Code").GetString());
    }

    [Fact]
    public async Task SearchCodes_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/icd10am/codes?q=cholera");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SearchCodes_FindsMatchingCodes()
    {
        var response = await _client.GetAsync("/api/icd10am/codes?q=cholera");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.NotEmpty(codes);
        Assert.Contains(codes, c => c.GetProperty("Code").GetString() == "A00.0");
    }

    [Fact]
    public async Task SearchCodes_RespectsLimit()
    {
        var response = await _client.GetAsync("/api/icd10am/codes?q=a&limit=1");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.True(codes.Length <= 1, "Expected at most 1 result with limit=1");
    }

    [Fact]
    public async Task SearchCodes_SearchesByCode()
    {
        var response = await _client.GetAsync("/api/icd10am/codes?q=R07");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.Contains(codes, c => c.GetProperty("Code").GetString()!.StartsWith("R07", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SearchCodes_ReturnsEmptyArray_WhenNoMatch()
    {
        var response = await _client.GetAsync("/api/icd10am/codes?q=zzznomatch");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.Empty(codes);
    }

    [Fact]
    public async Task GetCategoriesByBlock_ReturnsCategories()
    {
        var response = await _client.GetAsync("/api/icd10am/blocks/blk-a00/categories");
        var categories = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(categories);
        Assert.NotEmpty(categories);
        Assert.Contains(categories, c => c.GetProperty("CategoryCode").GetString() == "A00");
    }

    [Fact]
    public async Task GetCodesByCategory_ReturnsCodes()
    {
        var response = await _client.GetAsync("/api/icd10am/categories/cat-a00/codes");
        var codes = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(codes);
        Assert.NotEmpty(codes);
        Assert.Contains(codes, c => c.GetProperty("Code").GetString() == "A00.0");
    }
}
