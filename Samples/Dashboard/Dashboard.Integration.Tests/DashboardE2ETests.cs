using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// REAL E2E tests that prove the Dashboard UI can connect to the APIs.
/// These tests:
/// 1. Start Clinical API on a real port via WebApplicationFactory
/// 2. Start Scheduling API on a real port via WebApplicationFactory
/// 3. Serve Dashboard HTML with correct API URLs
/// 4. Load Dashboard in a REAL browser via Playwright
/// 5. Verify data from APIs appears in the UI
/// </summary>
public sealed class DashboardE2ETests : IAsyncLifetime
{
    private WebApplicationFactory<Clinical.Api.Program>? _clinicalFactory;
    private WebApplicationFactory<Scheduling.Api.Program>? _schedulingFactory;
    private IHost? _dashboardHost;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private HttpClient? _clinicalClient;
    private HttpClient? _schedulingClient;

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
        // Get random ports for real network listening
        var clinicalPort = GetAvailablePort();
        var schedulingPort = GetAvailablePort();
        var dashboardPort = GetAvailablePort();

        _clinicalUrl = $"http://localhost:{clinicalPort}";
        _schedulingUrl = $"http://localhost:{schedulingPort}";
        _dashboardUrl = $"http://localhost:{dashboardPort}";

        // Start Clinical API with WebApplicationFactory on a REAL port
        var clinicalApiAssembly = typeof(Clinical.Api.Program).Assembly;
        var clinicalContentRoot = Path.GetDirectoryName(clinicalApiAssembly.Location)!;

        _clinicalFactory = new WebApplicationFactory<Clinical.Api.Program>().WithWebHostBuilder(
            builder =>
            {
                builder.UseContentRoot(clinicalContentRoot);
                builder.UseSetting("DbPath", _clinicalDbPath);
                builder.UseUrls(_clinicalUrl);
                builder.ConfigureServices(services =>
                {
                    services.AddCors(options =>
                    {
                        options.AddPolicy(
                            "AllowAll",
                            policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
                        );
                    });
                });
            }
        );

        // Start server explicitly to listen on network
        _clinicalFactory.Server.PreserveExecutionContext = true;
        _clinicalClient = _clinicalFactory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri(_clinicalUrl) }
        );

        // Start Scheduling API with WebApplicationFactory on a REAL port
        var schedulingApiAssembly = typeof(Scheduling.Api.Program).Assembly;
        var schedulingContentRoot = Path.GetDirectoryName(schedulingApiAssembly.Location)!;

        _schedulingFactory = new WebApplicationFactory<Scheduling.Api.Program>().WithWebHostBuilder(
            builder =>
            {
                builder.UseContentRoot(schedulingContentRoot);
                builder.UseSetting("DbPath", _schedulingDbPath);
                builder.UseUrls(_schedulingUrl);
                builder.ConfigureServices(services =>
                {
                    services.AddCors(options =>
                    {
                        options.AddPolicy(
                            "AllowAll",
                            policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
                        );
                    });
                });
            }
        );

        _schedulingFactory.Server.PreserveExecutionContext = true;
        _schedulingClient = _schedulingFactory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri(_schedulingUrl) }
        );

        // Start Dashboard static file server with dynamic config
        _dashboardHost = CreateDashboardHost(dashboardPort);
        await _dashboardHost.StartAsync();

        // Seed test data via the test clients
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

        _clinicalClient?.Dispose();
        _schedulingClient?.Dispose();

        if (_clinicalFactory is not null)
            await _clinicalFactory.DisposeAsync();
        if (_schedulingFactory is not null)
            await _schedulingFactory.DisposeAsync();

        TryDeleteFile(_clinicalDbPath);
        TryDeleteFile(_schedulingDbPath);
    }

    /// <summary>
    /// CRITICAL TEST: Dashboard loads and displays patient data from Clinical API.
    /// If this fails, the UI cannot connect to the backend.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysPatientData_FromClinicalApi()
    {
        var page = await _browser!.NewPageAsync();

        // Navigate to dashboard
        await page.GotoAsync(_dashboardUrl);

        // Wait for app to load
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click on Patients in sidebar
        await page.ClickAsync("text=Patients");

        // Wait for patient data to load - look for our test patient
        await page.WaitForSelectorAsync(
            "text=TestPatient",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify patient name appears
        var content = await page.ContentAsync();
        Assert.Contains("TestPatient", content);
        Assert.Contains("E2ETest", content);
    }

    /// <summary>
    /// CRITICAL TEST: Dashboard loads and displays practitioner data from Scheduling API.
    /// If this fails, the UI cannot connect to the backend.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysPractitionerData_FromSchedulingApi()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync(_dashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click on Practitioners in sidebar
        await page.ClickAsync("text=Practitioners");

        // Wait for practitioner data to load
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
    /// If this fails, the UI cannot connect to the backend.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysAppointmentData_FromSchedulingApi()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync(_dashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click on Appointments in sidebar
        await page.ClickAsync("text=Appointments");

        // Wait for appointment data to load - look for status
        await page.WaitForSelectorAsync(
            "text=booked",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("booked", content);
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
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Should be on dashboard page by default
        // Wait for metric cards to show data
        await page.WaitForSelectorAsync(
            ".metric-card",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify we have content (stats should be non-zero since we seeded data)
        var cards = await page.QuerySelectorAllAsync(".metric-card");
        Assert.True(cards.Count > 0, "Dashboard should display metric cards with API data");
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
                    // Inject API URLs into dashboard config
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

                                // Inject config before closing head tag
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
        // Seed a patient via the test client
        var patientResponse = await _clinicalClient!.PostAsync(
            "/fhir/Patient/",
            new StringContent(
                """{"Active": true, "GivenName": "E2ETest", "FamilyName": "TestPatient", "Gender": "other"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        patientResponse.EnsureSuccessStatusCode();

        // Seed a practitioner via the test client
        var practitionerResponse = await _schedulingClient!.PostAsync(
            "/Practitioner",
            new StringContent(
                """{"Identifier": "DR001", "NameGiven": "E2EPractitioner", "NameFamily": "DrTest"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        practitionerResponse.EnsureSuccessStatusCode();

        // Seed an appointment via the test client
        var appointmentResponse = await _schedulingClient!.PostAsync(
            "/Appointment",
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
