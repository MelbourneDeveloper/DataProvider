using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// Core Dashboard E2E tests.
/// Uses EXACTLY the same ports as the real app.
/// </summary>
[Collection("E2E Tests Parallel")]
[Trait("Category", "E2E")]
public sealed class DashboardE2ETests
{
    private readonly E2EFixture _fixture;

    /// <summary>
    /// Constructor receives shared fixture.
    /// </summary>
    public DashboardE2ETests(E2EFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Dashboard main page shows stats from both APIs.
    /// </summary>
    [Fact]
    public async Task Dashboard_MainPage_ShowsStatsFromBothApis()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.WaitForSelectorAsync(
            ".metric-card",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var cards = await page.QuerySelectorAllAsync(".metric-card");
        Assert.True(cards.Count > 0, "Dashboard should display metric cards with API data");

        await page.CloseAsync();
    }
}
