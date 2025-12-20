using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// REAL E2E tests that prove the Dashboard UI can connect to the APIs.
/// These tests:
/// 1. Start Clinical API as a REAL process on a real port
/// 2. Start Scheduling API as a REAL process on a real port
/// 3. Serve Dashboard HTML with correct API URLs
/// 4. Load Dashboard in a REAL browser via Playwright
/// 5. Verify data from APIs appears in the UI
/// </summary>
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public sealed class DashboardE2ETests : IAsyncLifetime
{
    private Process? _clinicalProcess;
    private Process? _schedulingProcess;
    private IHost? _dashboardHost;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string _clinicalUrl = string.Empty;
    private string _schedulingUrl = string.Empty;
    private string _dashboardUrl = string.Empty;

    private readonly string _clinicalDbPath = Path.Combine(
        Path.GetTempPath(),
        $"clinical_e2e_{Guid.NewGuid()}.db"
    );
    private readonly string _schedulingDbPath = Path.Combine(
        Path.GetTempPath(),
        $"scheduling_e2e_{Guid.NewGuid()}.db"
    );

    /// <summary>
    /// Start all services and Playwright browser.
    /// </summary>
    public async Task InitializeAsync()
    {
        var clinicalPort = GetAvailablePort();
        var schedulingPort = GetAvailablePort();
        var dashboardPort = GetAvailablePort();

        _clinicalUrl = $"http://localhost:{clinicalPort}";
        _schedulingUrl = $"http://localhost:{schedulingPort}";
        _dashboardUrl = $"http://localhost:{dashboardPort}";

        // Start Clinical API as a REAL process
        var clinicalDll = Path.Combine(
            Path.GetDirectoryName(typeof(Clinical.Api.Program).Assembly.Location)!,
            "Clinical.Api.dll"
        );
        _clinicalProcess = StartApiProcess(clinicalDll, _clinicalUrl, _clinicalDbPath);

        // Start Scheduling API as a REAL process
        var schedulingDll = Path.Combine(
            Path.GetDirectoryName(typeof(Scheduling.Api.Program).Assembly.Location)!,
            "Scheduling.Api.dll"
        );
        _schedulingProcess = StartApiProcess(schedulingDll, _schedulingUrl, _schedulingDbPath);

        // Wait for APIs to be ready
        await WaitForApiAsync(_clinicalUrl, "/fhir/Patient");
        await WaitForApiAsync(_schedulingUrl, "/Practitioner");

        // Start Dashboard static file server
        _dashboardHost = CreateDashboardHost(dashboardPort);
        await _dashboardHost.StartAsync();

        // Seed test data
        await SeedTestDataAsync();

        // Start Playwright
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true }
        );
    }

    /// <summary>
    /// Stop all services and cleanup.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_browser is not null)
            await _browser.CloseAsync();
        _playwright?.Dispose();

        if (_dashboardHost is not null)
            await _dashboardHost.StopAsync();
        _dashboardHost?.Dispose();

        StopProcess(_clinicalProcess);
        StopProcess(_schedulingProcess);

        TryDeleteFile(_clinicalDbPath);
        TryDeleteFile(_schedulingDbPath);
    }

    /// <summary>
    /// CRITICAL TEST: Dashboard loads and displays patient data from Clinical API.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysPatientData_FromClinicalApi()
    {
        var page = await _browser!.NewPageAsync();

        // Log all console messages
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");

        // Log network requests
        page.Request += (_, req) => Console.WriteLine($"[NET REQUEST] {req.Method} {req.Url}");
        page.Response += (_, res) => Console.WriteLine($"[NET RESPONSE] {res.Status} {res.Url}");
        page.RequestFailed += (_, req) => Console.WriteLine($"[NET FAILED] {req.Url} - {req.Failure}");

        Console.WriteLine($"[TEST] Dashboard URL: {_dashboardUrl}");
        Console.WriteLine($"[TEST] Clinical URL: {_clinicalUrl}");
        Console.WriteLine($"[TEST] Scheduling URL: {_schedulingUrl}");

        await page.GotoAsync(_dashboardUrl);

        // Wait for Dashboard.js to render
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Click on Patients
        await page.ClickAsync("text=Patients");

        // Wait for patient data
        await page.WaitForSelectorAsync(
            "text=TestPatient",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

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
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync(_dashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync(
            "text=DrTest",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

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
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync(_dashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        await page.ClickAsync("text=Appointments");
        await page.WaitForSelectorAsync(
            "text=Checkup",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("Checkup", content);
    }

    /// <summary>
    /// CRITICAL TEST: Dashboard main page shows stats from both APIs.
    /// </summary>
    [Fact]
    public async Task Dashboard_MainPage_ShowsStatsFromBothApis()
    {
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync(_dashboardUrl);
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
    }

    private static Process StartApiProcess(string dllPath, string url, string dbPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\" --urls \"{url}\"",
                Environment = { ["DbPath"] = dbPath },
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

    private static async Task WaitForApiAsync(string baseUrl, string healthEndpoint)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var maxRetries = 30;

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await client.GetAsync($"{baseUrl}{healthEndpoint}");
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                    return; // API is up
            }
            catch
            { /* not ready yet */
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"API at {baseUrl} did not start within {maxRetries * 500}ms");
    }

    private IHost CreateDashboardHost(int port)
    {
        var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");

        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls($"http://localhost:{port}");
                webBuilder.Configure(app =>
                {
                    // Serve dashboard with injected API URLs
                    app.Use(
                        async (context, next) =>
                        {
                            if (
                                context.Request.Path == "/"
                                || context.Request.Path == "/index.html"
                            )
                            {
                                var indexPath = Path.Combine(wwwrootPath, "index.html");
                                var html = await File.ReadAllTextAsync(indexPath);

                                // Inject real API URLs
                                var config =
                                    $@"<script>
                                window.dashboardConfig = {{
                                    CLINICAL_API_URL: '{_clinicalUrl}',
                                    SCHEDULING_API_URL: '{_schedulingUrl}'
                                }};
                            </script>";
                                html = html.Replace("</head>", config + "</head>");

                                context.Response.ContentType = "text/html";
                                await context.Response.WriteAsync(html);
                                return;
                            }
                            await next();
                        }
                    );

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

    private async Task SeedTestDataAsync()
    {
        using var client = new HttpClient();

        // Seed a patient
        var patientResponse = await client.PostAsync(
            $"{_clinicalUrl}/fhir/Patient/",
            new StringContent(
                """{"Active": true, "GivenName": "E2ETest", "FamilyName": "TestPatient", "Gender": "other"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        patientResponse.EnsureSuccessStatusCode();

        // Seed a practitioner
        var practitionerResponse = await client.PostAsync(
            $"{_schedulingUrl}/Practitioner",
            new StringContent(
                """{"Identifier": "DR001", "NameGiven": "E2EPractitioner", "NameFamily": "DrTest"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        practitionerResponse.EnsureSuccessStatusCode();

        // Seed an appointment
        var appointmentResponse = await client.PostAsync(
            $"{_schedulingUrl}/Appointment",
            new StringContent(
                """{"ServiceCategory": "General", "ServiceType": "Checkup", "Start": "2025-12-20T10:00:00Z", "End": "2025-12-20T11:00:00Z", "PatientReference": "Patient/1", "PractitionerReference": "Practitioner/1", "Priority": "routine"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        appointmentResponse.EnsureSuccessStatusCode();
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        { /* ignore */
        }
    }
}
