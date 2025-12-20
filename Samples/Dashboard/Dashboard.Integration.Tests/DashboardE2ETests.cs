using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// Shared fixture that starts all services ONCE for all E2E tests.
/// </summary>
public sealed class E2EFixture : IAsyncLifetime
{
    private Process? _clinicalProcess;
    private Process? _schedulingProcess;
    private IHost? _dashboardHost;

    /// <summary>
    /// Playwright instance shared by all tests.
    /// </summary>
    public IPlaywright? Playwright { get; private set; }

    /// <summary>
    /// Browser instance shared by all tests.
    /// </summary>
    public IBrowser? Browser { get; private set; }

    /// <summary>
    /// Clinical API URL - SAME as real app default.
    /// </summary>
    public const string ClinicalUrl = "http://localhost:5080";

    /// <summary>
    /// Scheduling API URL - SAME as real app default.
    /// </summary>
    public const string SchedulingUrl = "http://localhost:5001";

    /// <summary>
    /// Dashboard URL - SAME as real app default.
    /// </summary>
    public const string DashboardUrl = "http://localhost:5173";

    /// <summary>
    /// Start all services ONCE for all tests.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Kill any existing processes on our ports first
        await KillProcessOnPortAsync(5080);
        await KillProcessOnPortAsync(5001);
        await KillProcessOnPortAsync(5173);

        var solutionDir = FindSolutionDirectory();

        // Start Clinical API with dotnet run - EXACTLY like you would manually
        _clinicalProcess = StartApi(Path.Combine(solutionDir, "Samples", "Clinical", "Clinical.Api"));

        // Start Scheduling API with dotnet run - EXACTLY like you would manually
        _schedulingProcess = StartApi(Path.Combine(solutionDir, "Samples", "Scheduling", "Scheduling.Api"));

        // Wait for APIs to be ready
        await WaitForApiAsync(ClinicalUrl, "/fhir/Patient/");
        await WaitForApiAsync(SchedulingUrl, "/Practitioner");

        // Start Dashboard static file server on port 5173 - NO config injection
        _dashboardHost = CreateDashboardHost();
        await _dashboardHost.StartAsync();

        // Seed test data
        await SeedTestDataAsync();

        // Start Playwright
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true }
        );
    }

    /// <summary>
    /// Stop all services ONCE after all tests.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (Browser is not null)
            await Browser.CloseAsync();
        Playwright?.Dispose();

        if (_dashboardHost is not null)
            await _dashboardHost.StopAsync();
        _dashboardHost?.Dispose();

        StopProcess(_clinicalProcess);
        StopProcess(_schedulingProcess);
    }

    private static Process StartApi(string projectDir)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        process.Start();
        return process;
    }

    private static void StopProcess(Process? process)
    {
        if (process is null || process.HasExited)
            return;

        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch
        { /* ignore */
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task KillProcessOnPortAsync(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "lsof",
                Arguments = $"-ti :{port}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
                return;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            foreach (var pidStr in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(pidStr.Trim(), out var pid))
                {
                    try
                    {
                        Process.GetProcessById(pid).Kill();
                    }
                    catch
                    { /* ignore */
                    }
                }
            }

            await Task.Delay(500);
        }
        catch
        { /* ignore */
        }
    }

    private static async Task WaitForApiAsync(string baseUrl, string healthEndpoint)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var maxRetries = 60; // Give dotnet run more time to build and start

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await client.GetAsync($"{baseUrl}{healthEndpoint}");
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                    return;
            }
            catch
            { /* not ready yet */
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"API at {baseUrl} did not start within {maxRetries * 500}ms");
    }

    private static IHost CreateDashboardHost()
    {
        var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");

        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://localhost:5173");
                webBuilder.Configure(app =>
                {
                    // Serve static files directly - NO config injection
                    // Dashboard index.html uses default ports which match our APIs
                    app.UseDefaultFiles();
                    app.UseStaticFiles(
                        new StaticFileOptions
                        {
                            FileProvider = new PhysicalFileProvider(wwwrootPath),
                        }
                    );
                });
            })
            .Build();
    }

    private static async Task SeedTestDataAsync()
    {
        using var client = new HttpClient();

        var patientResponse = await client.PostAsync(
            $"{ClinicalUrl}/fhir/Patient/",
            new StringContent(
                """{"Active": true, "GivenName": "E2ETest", "FamilyName": "TestPatient", "Gender": "other"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        patientResponse.EnsureSuccessStatusCode();

        var practitionerResponse = await client.PostAsync(
            $"{SchedulingUrl}/Practitioner",
            new StringContent(
                """{"Identifier": "DR001", "NameGiven": "E2EPractitioner", "NameFamily": "DrTest"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        practitionerResponse.EnsureSuccessStatusCode();

        var appointmentResponse = await client.PostAsync(
            $"{SchedulingUrl}/Appointment",
            new StringContent(
                """{"ServiceCategory": "General", "ServiceType": "Checkup", "Start": "2025-12-20T10:00:00Z", "End": "2025-12-20T11:00:00Z", "PatientReference": "Patient/1", "PractitionerReference": "Practitioner/1", "Priority": "routine"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        appointmentResponse.EnsureSuccessStatusCode();
    }

    private static string FindSolutionDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not find solution directory");
    }
}

/// <summary>
/// Collection definition that ensures all E2E tests share the same fixture.
/// </summary>
[CollectionDefinition("E2E Tests")]
public sealed class E2ECollection : ICollectionFixture<E2EFixture>;

/// <summary>
/// REAL E2E tests that prove the Dashboard UI can connect to the APIs.
/// Uses EXACTLY the same ports as the real app - no dynamic port bullshit.
/// </summary>
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public sealed class DashboardE2ETests
{
    private readonly E2EFixture _fixture;

    /// <summary>
    /// Constructor receives shared fixture.
    /// </summary>
    public DashboardE2ETests(E2EFixture fixture) => _fixture = fixture;

    /// <summary>
    /// CRITICAL TEST: Dashboard loads and displays patient data from Clinical API.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysPatientData_FromClinicalApi()
    {
        var page = await _fixture.Browser!.NewPageAsync();

        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");
        page.RequestFailed += (_, req) => Console.WriteLine($"[NET FAILED] {req.Url} - {req.Failure}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });

        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync("text=TestPatient", new PageWaitForSelectorOptions { Timeout = 10000 });

        var content = await page.ContentAsync();
        Assert.Contains("TestPatient", content);
        Assert.Contains("E2ETest", content);
    }

    /// <summary>
    /// CRITICAL TEST: Dashboard loads and displays practitioner data from Scheduling API.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysPractitionerData_FromSchedulingApi()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });

        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync("text=DrTest", new PageWaitForSelectorOptions { Timeout = 10000 });

        var content = await page.ContentAsync();
        Assert.Contains("DrTest", content);
        Assert.Contains("E2EPractitioner", content);
    }

    /// <summary>
    /// CRITICAL TEST: Dashboard loads and displays appointment data from Scheduling API.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysAppointmentData_FromSchedulingApi()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });

        await page.ClickAsync("text=Appointments");
        await page.WaitForSelectorAsync("text=Checkup", new PageWaitForSelectorOptions { Timeout = 10000 });

        var content = await page.ContentAsync();
        Assert.Contains("Checkup", content);
    }

    /// <summary>
    /// CRITICAL TEST: Dashboard main page shows stats from both APIs.
    /// </summary>
    [Fact]
    public async Task Dashboard_MainPage_ShowsStatsFromBothApis()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(".sidebar", new PageWaitForSelectorOptions { Timeout = 20000 });
        await page.WaitForSelectorAsync(".metric-card", new PageWaitForSelectorOptions { Timeout = 10000 });

        var cards = await page.QuerySelectorAllAsync(".metric-card");
        Assert.True(cards.Count > 0, "Dashboard should display metric cards with API data");
    }
}
