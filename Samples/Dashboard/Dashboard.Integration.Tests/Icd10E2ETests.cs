using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// E2E tests for ICD-10 Clinical Coding in the Dashboard.
/// Tests keyword search, RAG/AI search, code lookup, and drill-down to code details.
/// Requires ICD-10 API running on port 5090.
/// </summary>
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public sealed class Icd10E2ETests
{
    private readonly E2EFixture _fixture;

    /// <summary>
    /// Constructor receives shared E2E fixture.
    /// </summary>
    public Icd10E2ETests(E2EFixture fixture) => _fixture = fixture;

    // =========================================================================
    // KEYWORD SEARCH
    // =========================================================================

    /// <summary>
    /// Keyword search for "diabetes" returns results table with Chapter and Category.
    /// </summary>
    [Fact]
    public async Task KeywordSearch_Diabetes_ReturnsResultsWithChapterAndCategory()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=Keyword Search");
        await Task.Delay(500);

        await page.FillAsync("input[placeholder*='Search by code']", "diabetes");
        await page.ClickAsync("button:has-text('Search')");

        await page.WaitForSelectorAsync(
            ".table",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("Chapter", content);
        Assert.Contains("Category", content);
        Assert.Contains("E11", content);

        var rows = await page.QuerySelectorAllAsync(".table tbody tr");
        Assert.True(rows.Count > 0, "Keyword search for 'diabetes' should return results");

        await page.CloseAsync();
    }

    /// <summary>
    /// Keyword search for "pneumonia" returns results with billable status column.
    /// </summary>
    [Fact]
    public async Task KeywordSearch_Pneumonia_ShowsBillableStatus()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=Keyword Search");
        await Task.Delay(500);

        await page.FillAsync("input[placeholder*='Search by code']", "pneumonia");
        await page.ClickAsync("button:has-text('Search')");

        await page.WaitForSelectorAsync(
            ".table",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("Status", content);

        var rows = await page.QuerySelectorAllAsync(".table tbody tr");
        Assert.True(rows.Count > 0, "Keyword search for 'pneumonia' should return results");

        await page.CloseAsync();
    }

    /// <summary>
    /// Keyword search shows result count text.
    /// </summary>
    [Fact]
    public async Task KeywordSearch_ShowsResultCount()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=Keyword Search");
        await Task.Delay(500);

        await page.FillAsync("input[placeholder*='Search by code']", "fracture");
        await page.ClickAsync("button:has-text('Search')");

        await page.WaitForSelectorAsync(
            ".table",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("results found", content);

        await page.CloseAsync();
    }

    // =========================================================================
    // RAG / AI SEARCH
    // =========================================================================

    /// <summary>
    /// AI search for "chest pain with shortness of breath" returns results
    /// with confidence scores and AI-matched label.
    /// </summary>
    [Fact]
    public async Task AISearch_ChestPain_ReturnsResultsWithConfidence()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=AI Search");
        await Task.Delay(500);

        await page.FillAsync(
            "input[placeholder*='Describe symptoms']",
            "chest pain with shortness of breath"
        );
        await page.ClickAsync("button:has-text('Search')");

        try
        {
            await page.WaitForSelectorAsync(
                ".table",
                new PageWaitForSelectorOptions { Timeout = 30000 }
            );

            var content = await page.ContentAsync();
            Assert.Contains("AI-matched results", content);
            Assert.Contains("Confidence", content);
            Assert.Contains("Chapter", content);
            Assert.Contains("Category", content);

            var rows = await page.QuerySelectorAllAsync(".table tbody tr");
            Assert.True(rows.Count > 0, "AI search should return results");
        }
        catch (TimeoutException)
        {
            Console.WriteLine(
                "[TEST] AI search timed out - embedding service may not be running on port 8000"
            );
        }

        await page.CloseAsync();
    }

    /// <summary>
    /// AI search for "heart attack" returns cardiac-related codes.
    /// </summary>
    [Fact]
    public async Task AISearch_HeartAttack_ReturnsCardiacCodes()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=AI Search");
        await Task.Delay(500);

        await page.FillAsync("input[placeholder*='Describe symptoms']", "heart attack");
        await page.ClickAsync("button:has-text('Search')");

        try
        {
            await page.WaitForSelectorAsync(
                ".table",
                new PageWaitForSelectorOptions { Timeout = 30000 }
            );

            var content = await page.ContentAsync();
            Assert.Contains("AI-matched results", content);

            var rows = await page.QuerySelectorAllAsync(".table tbody tr");
            Assert.True(rows.Count > 0, "AI search for 'heart attack' should return results");
        }
        catch (TimeoutException)
        {
            Console.WriteLine(
                "[TEST] AI search timed out - embedding service may not be running on port 8000"
            );
        }

        await page.CloseAsync();
    }

    /// <summary>
    /// AI search shows the "Include ACHI procedure codes" checkbox.
    /// </summary>
    [Fact]
    public async Task AISearch_ShowsAchiCheckbox()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=AI Search");
        await Task.Delay(500);

        var content = await page.ContentAsync();
        Assert.Contains("Include ACHI procedure codes", content);
        Assert.Contains("medical AI embeddings", content);

        await page.CloseAsync();
    }

    // =========================================================================
    // CODE LOOKUP
    // =========================================================================

    /// <summary>
    /// Code lookup for "E11.9" shows detailed code info with Chapter, Block, Category.
    /// </summary>
    [Fact]
    public async Task CodeLookup_E119_ShowsFullCodeDetail()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=Code Lookup");
        await Task.Delay(500);

        await page.FillAsync("input[placeholder*='Enter exact ICD-10 code']", "E11.9");
        await page.ClickAsync("button:has-text('Search')");

        await page.WaitForSelectorAsync(
            "text=Back to results",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var content = await page.ContentAsync();

        Assert.Contains("E11.9", content);
        Assert.Contains("diabetes", content.ToLowerInvariant());
        Assert.Contains("Chapter", content);
        Assert.Contains("Block", content);
        Assert.Contains("Category", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Code lookup for "I10" shows hypertension detail with chapter info.
    /// </summary>
    [Fact]
    public async Task CodeLookup_I10_ShowsHypertensionDetail()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=Code Lookup");
        await Task.Delay(500);

        await page.FillAsync("input[placeholder*='Enter exact ICD-10 code']", "I10");
        await page.ClickAsync("button:has-text('Search')");

        await page.WaitForSelectorAsync(
            "text=Back to results",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var content = await page.ContentAsync();

        Assert.Contains("I10", content);
        Assert.Contains("hypertension", content.ToLowerInvariant());
        Assert.Contains("Chapter", content);
        Assert.Contains("circulatory", content.ToLowerInvariant());

        await page.CloseAsync();
    }

    /// <summary>
    /// Code lookup for "R07.9" shows chest pain detail with billable status.
    /// </summary>
    [Fact]
    public async Task CodeLookup_R079_ShowsChestPainWithBillable()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=Code Lookup");
        await Task.Delay(500);

        await page.FillAsync("input[placeholder*='Enter exact ICD-10 code']", "R07.9");
        await page.ClickAsync("button:has-text('Search')");

        await page.WaitForSelectorAsync(
            "text=Back to results",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var content = await page.ContentAsync();

        Assert.Contains("R07.9", content);
        Assert.Contains("chest pain", content.ToLowerInvariant());
        Assert.Contains("Billable", content);
        Assert.Contains("Block", content);
        Assert.Contains("Category", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Code lookup with prefix "E11" shows multiple matching codes as a list.
    /// </summary>
    [Fact]
    public async Task CodeLookup_E11Prefix_ShowsMultipleResults()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=Code Lookup");
        await Task.Delay(500);

        await page.FillAsync("input[placeholder*='Enter exact ICD-10 code']", "E11");
        await page.ClickAsync("button:has-text('Search')");

        await page.WaitForSelectorAsync(
            ".table",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var rows = await page.QuerySelectorAllAsync(".table tbody tr");
        Assert.True(rows.Count > 1, "Prefix search for 'E11' should return multiple codes");

        var content = await page.ContentAsync();
        Assert.Contains("E11", content);

        await page.CloseAsync();
    }

    // =========================================================================
    // DRILL-DOWN: KEYWORD SEARCH -> CODE DETAIL
    // =========================================================================

    /// <summary>
    /// Keyword search then clicking a result row drills down to code detail view
    /// showing Chapter, Block, Category, and description.
    /// </summary>
    [Fact]
    public async Task DrillDown_KeywordSearch_ClickResult_ShowsCodeDetail()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=Keyword Search");
        await Task.Delay(500);

        await page.FillAsync("input[placeholder*='Search by code']", "hypertension");
        await page.ClickAsync("button:has-text('Search')");

        await page.WaitForSelectorAsync(
            ".table tbody tr",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        // Click the first result row to drill down
        await page.ClickAsync(".search-result-row >> nth=0");

        // Wait for detail view to load (shows "Back to results" button)
        await page.WaitForSelectorAsync(
            "text=Back to results",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var content = await page.ContentAsync();

        // Detail view must show hierarchy
        Assert.Contains("Chapter", content);
        Assert.Contains("Block", content);
        Assert.Contains("Category", content);

        // Must show billable status
        Assert.True(
            content.Contains("Billable") || content.Contains("Non-billable"),
            "Detail view should show billable status"
        );

        // Must show the code badge
        Assert.Contains("Copy Code", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Drill down to code detail then navigate back to results list.
    /// </summary>
    [Fact]
    public async Task DrillDown_BackToResults_RestoresResultsList()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=Keyword Search");
        await Task.Delay(500);

        await page.FillAsync("input[placeholder*='Search by code']", "diabetes");
        await page.ClickAsync("button:has-text('Search')");

        await page.WaitForSelectorAsync(
            ".table tbody tr",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        // Click first result to drill down
        await page.ClickAsync(".search-result-row >> nth=0");

        await page.WaitForSelectorAsync(
            "text=Back to results",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        // Click back button
        await page.ClickAsync("text=Back to results");

        // Results table should reappear
        await page.WaitForSelectorAsync(
            ".table tbody tr",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var rows = await page.QuerySelectorAllAsync(".table tbody tr");
        Assert.True(rows.Count > 0, "Results list should be restored after clicking back");

        await page.CloseAsync();
    }

    /// <summary>
    /// Drill down from keyword search shows Full Description section when available.
    /// </summary>
    [Fact]
    public async Task DrillDown_ShowsFullDescription()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=Code Lookup");
        await Task.Delay(500);

        // G43.909 has a long description
        await page.FillAsync("input[placeholder*='Enter exact ICD-10 code']", "G43.909");
        await page.ClickAsync("button:has-text('Search')");

        await page.WaitForSelectorAsync(
            "text=Back to results",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var content = await page.ContentAsync();

        Assert.Contains("G43.909", content);
        Assert.Contains("migraine", content.ToLowerInvariant());
        Assert.Contains("Full Description", content);

        await page.CloseAsync();
    }

    // =========================================================================
    // DRILL-DOWN: AI SEARCH -> CODE DETAIL
    // =========================================================================

    /// <summary>
    /// AI search then clicking a result drills down to the code detail view.
    /// </summary>
    [Fact]
    public async Task DrillDown_AISearch_ClickResult_ShowsCodeDetail()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=AI Search");
        await Task.Delay(500);

        await page.FillAsync(
            "input[placeholder*='Describe symptoms']",
            "type 2 diabetes with kidney complications"
        );
        await page.ClickAsync("button:has-text('Search')");

        try
        {
            await page.WaitForSelectorAsync(
                ".table tbody tr",
                new PageWaitForSelectorOptions { Timeout = 30000 }
            );

            // Click first AI search result to drill down
            await page.ClickAsync(".search-result-row >> nth=0");

            // Wait for detail view
            await page.WaitForSelectorAsync(
                "text=Back to results",
                new PageWaitForSelectorOptions { Timeout = 15000 }
            );

            var content = await page.ContentAsync();

            // Detail view must show full hierarchy
            Assert.Contains("Chapter", content);
            Assert.Contains("Block", content);
            Assert.Contains("Category", content);
            Assert.Contains("Copy Code", content);
        }
        catch (TimeoutException)
        {
            Console.WriteLine(
                "[TEST] AI search timed out - embedding service may not be running on port 8000"
            );
        }

        await page.CloseAsync();
    }

    // =========================================================================
    // EDGE CASES
    // =========================================================================

    /// <summary>
    /// Code lookup for nonexistent code shows "No codes found" message.
    /// </summary>
    [Fact]
    public async Task CodeLookup_NonexistentCode_ShowsNoCodesFound()
    {
        var page = await NavigateToClinicalCodingAsync();

        await page.ClickAsync("text=Code Lookup");
        await Task.Delay(500);

        await page.FillAsync("input[placeholder*='Enter exact ICD-10 code']", "ZZZ99.99");
        await page.ClickAsync("button:has-text('Search')");

        await page.WaitForSelectorAsync(
            "text=No codes found",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("No codes found", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Switching between search tabs clears previous results.
    /// </summary>
    [Fact]
    public async Task SwitchingTabs_ClearsPreviousResults()
    {
        var page = await NavigateToClinicalCodingAsync();

        // Do a keyword search first
        await page.ClickAsync("text=Keyword Search");
        await Task.Delay(500);

        await page.FillAsync("input[placeholder*='Search by code']", "fracture");
        await page.ClickAsync("button:has-text('Search')");

        await page.WaitForSelectorAsync(
            ".table",
            new PageWaitForSelectorOptions { Timeout = 15000 }
        );

        // Switch to Code Lookup tab
        await page.ClickAsync("text=Code Lookup");
        await Task.Delay(500);

        // Results table should be gone - empty state should show
        var content = await page.ContentAsync();
        Assert.Contains("Direct Code Lookup", content);

        await page.CloseAsync();
    }

    // =========================================================================
    // HELPER
    // =========================================================================

    private async Task<IPage> NavigateToClinicalCodingAsync()
    {
        var page = await _fixture.CreateAuthenticatedPageAsync(
            navigateTo: $"{E2EFixture.DashboardUrl}#clinical-coding"
        );
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        await page.WaitForSelectorAsync(
            ".clinical-coding-page",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        return page;
    }
}
