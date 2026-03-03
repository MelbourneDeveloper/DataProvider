using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace Reporting.Integration.Tests;

/// <summary>
/// API integration tests: real HTTP requests -> real Reporting.Api -> real SQLite database.
/// No mocks. No fakes. The full API stack running end to end.
/// </summary>
[Collection("Reporting E2E")]
[Trait("Category", "E2E")]
public sealed class ReportingApiTests
{
    private readonly ReportingE2EFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ReportingApiTests(ReportingE2EFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListReports_ReturnsReportList_FromRealApi()
    {
        // Arrange
        using var client = new HttpClient();

        // Act
        var response = await client.GetAsync($"{_fixture.ApiUrl}/api/reports");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var reports = JsonSerializer.Deserialize<JsonElement[]>(json, JsonOptions);
        Assert.NotNull(reports);
        Assert.True(reports.Length > 0, "API should return at least one report");

        var firstReport = reports[0];
        Assert.True(firstReport.TryGetProperty("id", out var id));
        Assert.Equal("e2e-products", id.GetString());
        Assert.True(firstReport.TryGetProperty("title", out var title));
        Assert.Equal("E2E Product Report", title.GetString());
    }

    [Fact]
    public async Task GetReport_ReturnsReportMetadata_WithoutConnectionStrings()
    {
        // Arrange
        using var client = new HttpClient();

        // Act
        var response = await client.GetAsync($"{_fixture.ApiUrl}/api/reports/e2e-products");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var report = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);

        // Verify metadata fields are present
        Assert.Equal("e2e-products", report.GetProperty("id").GetString());
        Assert.Equal("E2E Product Report", report.GetProperty("title").GetString());

        // Verify data source IDs are present
        var dsIds = report.GetProperty("dataSourceIds");
        Assert.Equal(3, dsIds.GetArrayLength());

        // Verify layout is present
        var layout = report.GetProperty("layout");
        Assert.Equal(12, layout.GetProperty("columns").GetInt32());

        // CRITICAL: connection strings must NOT be exposed
        Assert.DoesNotContain("connectionRef", json);
        Assert.DoesNotContain("Data Source", json);
        Assert.DoesNotContain("reporting-db", json);
    }

    [Fact]
    public async Task GetReport_NonexistentId_Returns404()
    {
        // Arrange
        using var client = new HttpClient();

        // Act
        var response = await client.GetAsync($"{_fixture.ApiUrl}/api/reports/nonexistent");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteReport_ReturnsRealDataFromSQLite()
    {
        // Arrange
        using var client = new HttpClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/e2e-products/execute",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);

        Assert.Equal("e2e-products", result.GetProperty("reportId").GetString());

        var dataSources = result.GetProperty("dataSources");

        // Verify allProducts data source returned real data
        var allProducts = dataSources.GetProperty("allProducts");
        var productRows = allProducts.GetProperty("rows");
        Assert.Equal(6, allProducts.GetProperty("totalRows").GetInt32());
        Assert.Equal(6, productRows.GetArrayLength());

        // Verify column names from real SQLite query
        var columns = allProducts.GetProperty("columnNames");
        Assert.Contains("Id", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("Name", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("Category", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("Price", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("Stock", columns.EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task ExecuteReport_CategorySummary_ReturnsAggregatedData()
    {
        // Arrange
        using var client = new HttpClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/e2e-products/execute",
            request
        );

        // Assert
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        var categorySummary = result.GetProperty("dataSources").GetProperty("categorySummary");

        // We seeded 3 Widgets, 2 Gadgets, 1 Doohickey = 3 categories
        Assert.Equal(3, categorySummary.GetProperty("totalRows").GetInt32());

        var columns = categorySummary.GetProperty("columnNames");
        Assert.Contains("Category", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("ProductCount", columns.EnumerateArray().Select(c => c.GetString()));
        Assert.Contains("TotalStock", columns.EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task ExecuteReport_Totals_ReturnsCorrectAggregates()
    {
        // Arrange
        using var client = new HttpClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/e2e-products/execute",
            request
        );

        // Assert
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        var totals = result.GetProperty("dataSources").GetProperty("totals");

        Assert.Equal(1, totals.GetProperty("totalRows").GetInt32());

        // Verify the totals row contains aggregate values
        var row = totals.GetProperty("rows")[0];
        // 6 products total
        Assert.Equal(6, row[0].GetInt64());
    }

    [Fact]
    public async Task ExecuteReport_NonexistentId_Returns404()
    {
        // Arrange
        using var client = new HttpClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/does-not-exist/execute",
            request
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteReport_AllDataSourcesPresent_InResponse()
    {
        // Arrange
        using var client = new HttpClient();
        var request = new StringContent(
            """{"parameters": {}, "format": "json"}""",
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await client.PostAsync(
            $"{_fixture.ApiUrl}/api/reports/e2e-products/execute",
            request
        );

        // Assert
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        var dataSources = result.GetProperty("dataSources");

        // All 3 data sources from the report definition should be present
        Assert.True(dataSources.TryGetProperty("allProducts", out _));
        Assert.True(dataSources.TryGetProperty("categorySummary", out _));
        Assert.True(dataSources.TryGetProperty("totals", out _));
    }
}
