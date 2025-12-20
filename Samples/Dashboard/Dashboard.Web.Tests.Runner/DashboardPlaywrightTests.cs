namespace Dashboard.Web.Tests.Runner;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

/// <summary>
/// Playwright-based test runner that executes H5 browser tests and reports results to xUnit.
/// </summary>
public sealed class DashboardPlaywrightTests : IAsyncLifetime
{
    private const int PageLoadTimeoutMs = 10000;
    private const int ElementTimeoutMs = 5000;
    private const int TestCompleteTimeoutMs = 30000;

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>
    /// Initialize Playwright and browser.
    /// </summary>
    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true }
        );
    }

    /// <summary>
    /// Cleanup browser and Playwright.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }

    /// <summary>
    /// Navigates to test page, runs tests, and returns the output.
    /// </summary>
    /// <param name="testHtmlPath">Path to the test HTML file.</param>
    /// <returns>Test output string and pass/fail counts.</returns>
    private async Task<(string Output, int Passed, int Failed)> RunTestsAndGetOutputAsync(
        string testHtmlPath
    )
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync(
            $"file://{testHtmlPath}",
            new PageGotoOptions { Timeout = PageLoadTimeoutMs }
        );

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        var runButton = page.Locator("#run-btn");
        await runButton.WaitForAsync(
            new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = ElementTimeoutMs }
        );
        await runButton.ClickAsync(new LocatorClickOptions { Timeout = ElementTimeoutMs });

        await page.WaitForFunctionAsync(
            "() => document.getElementById('run-btn').textContent.includes('Again')",
            new PageWaitForFunctionOptions { Timeout = TestCompleteTimeoutMs }
        );

        var passedText = await page.TextContentAsync("#passed-count");
        var failedText = await page.TextContentAsync("#failed-count");

        var passed = int.TryParse(passedText, out var p) ? p : 0;
        var failed = int.TryParse(failedText, out var f) ? f : 0;

        var output = await page.TextContentAsync("#output") ?? string.Empty;

        await page.CloseAsync();

        return (output, passed, failed);
    }

    /// <summary>
    /// Runs all H5 dashboard tests in a headless browser and verifies they all pass.
    /// </summary>
    [Fact]
    public async Task AllDashboardTestsPass()
    {
        var testHtmlPath = FindTestHtml();
        Assert.NotNull(testHtmlPath);

        var (output, passed, failed) = await RunTestsAndGetOutputAsync(testHtmlPath);

        Assert.True(
            failed == 0,
            $"Dashboard tests failed: {failed} failures out of {passed + failed} tests.\n\nOutput:\n{output}"
        );
        Assert.True(passed > 0, "No tests were executed");
    }

    /// <summary>
    /// Runs navigation tests and verifies they pass.
    /// </summary>
    [Fact]
    public async Task NavigationTestsPass()
    {
        var testHtmlPath = FindTestHtml();
        Assert.NotNull(testHtmlPath);

        var (output, _, _) = await RunTestsAndGetOutputAsync(testHtmlPath);

        Assert.Contains("Navigation Tests", output);
        Assert.Contains("renders app with sidebar", output);
    }

    /// <summary>
    /// Runs dashboard page tests and verifies they pass.
    /// </summary>
    [Fact]
    public async Task DashboardPageTestsPass()
    {
        var testHtmlPath = FindTestHtml();
        Assert.NotNull(testHtmlPath);

        var (output, _, _) = await RunTestsAndGetOutputAsync(testHtmlPath);

        Assert.Contains("Dashboard Page Tests", output);
        Assert.Contains("displays metric cards", output);
    }

    /// <summary>
    /// Runs patients page tests and verifies they pass.
    /// </summary>
    [Fact]
    public async Task PatientsPageTestsPass()
    {
        var testHtmlPath = FindTestHtml();
        Assert.NotNull(testHtmlPath);

        var (output, _, _) = await RunTestsAndGetOutputAsync(testHtmlPath);

        Assert.Contains("Patients Page Tests", output);
        Assert.Contains("displays patient table", output);
    }

    /// <summary>
    /// Runs sidebar tests and verifies they pass.
    /// </summary>
    [Fact]
    public async Task SidebarTestsPass()
    {
        var testHtmlPath = FindTestHtml();
        Assert.NotNull(testHtmlPath);

        var (output, _, _) = await RunTestsAndGetOutputAsync(testHtmlPath);

        Assert.Contains("Sidebar Tests", output);
        Assert.Contains("displays logo", output);
    }

    private static string? FindTestHtml()
    {
        var currentDir = AppContext.BaseDirectory;
        var searchPaths = new[]
        {
            Path.Combine(currentDir, "wwwroot", "test.html"),
            Path.Combine(currentDir, "..", "Dashboard.Web.Tests", "wwwroot", "test.html"),
            Path.Combine(
                currentDir,
                "..",
                "..",
                "..",
                "Dashboard.Web.Tests",
                "wwwroot",
                "test.html"
            ),
            Path.Combine(
                currentDir,
                "..",
                "..",
                "..",
                "..",
                "Dashboard.Web.Tests",
                "wwwroot",
                "test.html"
            ),
            Path.Combine(
                currentDir,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Dashboard.Web.Tests",
                "wwwroot",
                "test.html"
            ),
        };

        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        var projectRoot = FindProjectRoot(currentDir);
        if (projectRoot is not null)
        {
            var testHtml = Path.Combine(
                projectRoot,
                "Samples",
                "Dashboard",
                "Dashboard.Web.Tests",
                "wwwroot",
                "test.html"
            );
            if (File.Exists(testHtml))
            {
                return testHtml;
            }
        }

        return null;
    }

    private static string? FindProjectRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DataProvider.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
