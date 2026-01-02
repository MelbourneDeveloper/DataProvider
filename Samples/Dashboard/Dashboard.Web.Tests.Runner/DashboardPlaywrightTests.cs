using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace Dashboard.Web.Tests.Runner;

/// <summary>
/// Playwright-based test runner that executes H5 browser tests once and validates all results.
/// Runs browser once, executes tests once, then validates all test categories from the output.
/// </summary>
public sealed class DashboardPlaywrightTests : IAsyncLifetime
{
    private const int PageLoadTimeoutMs = 10000;
    private const int ElementTimeoutMs = 5000;
    private const int TestCompleteTimeoutMs = 30000;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private string _testOutput = string.Empty;
    private int _passedCount;
    private int _failedCount;
    private bool _testsExecuted;

    /// <summary>
    /// Initialize Playwright, browser, run tests ONCE and cache results.
    /// </summary>
    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = ["--allow-file-access-from-files", "--disable-web-security"],
            }
        );

        var testHtmlPath = FindTestHtml();
        if (testHtmlPath is null)
        {
            return;
        }

        var page = await _browser.NewPageAsync();

        await page.GotoAsync(
            $"file://{testHtmlPath}",
            new PageGotoOptions { Timeout = PageLoadTimeoutMs }
        );

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        var runButton = page.Locator("#run-btn");
        await runButton.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = ElementTimeoutMs,
            }
        );
        await runButton.ClickAsync(new LocatorClickOptions { Timeout = ElementTimeoutMs });

        await page.WaitForFunctionAsync(
            "() => document.getElementById('run-btn').textContent.includes('Again')",
            new PageWaitForFunctionOptions { Timeout = TestCompleteTimeoutMs }
        );

        var passedText = await page.TextContentAsync("#passed-count");
        var failedText = await page.TextContentAsync("#failed-count");

        _passedCount = int.TryParse(passedText, out var p) ? p : 0;
        _failedCount = int.TryParse(failedText, out var f) ? f : 0;
        _testOutput = await page.TextContentAsync("#output") ?? string.Empty;
        _testsExecuted = true;

        await page.CloseAsync();
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
    /// Verifies all H5 dashboard tests passed with zero failures.
    /// </summary>
    [Fact]
    public void AllDashboardTestsPass()
    {
        Assert.True(_testsExecuted, "Test HTML file not found");
        Assert.True(
            _failedCount == 0,
            $"Dashboard tests failed: {_failedCount} failures out of {_passedCount + _failedCount} tests.\n\nOutput:\n{_testOutput}"
        );
        Assert.True(_passedCount > 0, "No tests were executed");
    }

    /// <summary>
    /// Verifies navigation tests are present and passed.
    /// </summary>
    [Fact]
    public void NavigationTestsPass()
    {
        Assert.True(_testsExecuted, "Test HTML file not found");
        Assert.Contains("Navigation Tests", _testOutput);
        Assert.Contains("renders app with sidebar", _testOutput);
    }

    /// <summary>
    /// Verifies dashboard page tests are present and passed.
    /// </summary>
    [Fact]
    public void DashboardPageTestsPass()
    {
        Assert.True(_testsExecuted, "Test HTML file not found");
        Assert.Contains("Dashboard Page Tests", _testOutput);
        Assert.Contains("displays metric cards", _testOutput);
    }

    /// <summary>
    /// Verifies patients page tests are present and passed.
    /// </summary>
    [Fact]
    public void PatientsPageTestsPass()
    {
        Assert.True(_testsExecuted, "Test HTML file not found");
        Assert.Contains("Patients Page Tests", _testOutput);
        Assert.Contains("displays patient table", _testOutput);
    }

    /// <summary>
    /// Verifies sidebar tests are present and passed.
    /// </summary>
    [Fact]
    public void SidebarTestsPass()
    {
        Assert.True(_testsExecuted, "Test HTML file not found");
        Assert.Contains("Sidebar Tests", _testOutput);
        Assert.Contains("displays logo", _testOutput);
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
