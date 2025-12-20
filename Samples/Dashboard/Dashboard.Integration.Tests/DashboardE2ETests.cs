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

        // Find the project directories relative to the test assembly
        var testAssemblyDir = Path.GetDirectoryName(typeof(E2EFixture).Assembly.Location)!;
        var samplesDir = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", ".."));
        var clinicalProjectDir = Path.Combine(samplesDir, "Clinical", "Clinical.Api");
        var schedulingProjectDir = Path.Combine(samplesDir, "Scheduling", "Scheduling.Api");

        Console.WriteLine($"[E2E] Test assembly dir: {testAssemblyDir}");
        Console.WriteLine($"[E2E] Samples dir: {samplesDir}");
        Console.WriteLine($"[E2E] Clinical dir: {clinicalProjectDir}");
        Console.WriteLine($"[E2E] Clinical dir exists: {Directory.Exists(clinicalProjectDir)}");

        // Start Clinical API using pre-built DLL with correct content root
        var clinicalDll = Path.Combine(clinicalProjectDir, "bin", "Debug", "net9.0", "Clinical.Api.dll");
        Console.WriteLine($"[E2E] Clinical DLL: {clinicalDll}");
        Console.WriteLine($"[E2E] Clinical DLL exists: {File.Exists(clinicalDll)}");
        _clinicalProcess = StartApiFromDll(clinicalDll, clinicalProjectDir, ClinicalUrl);

        // Start Scheduling API using pre-built DLL with correct content root
        var schedulingDll = Path.Combine(schedulingProjectDir, "bin", "Debug", "net9.0", "Scheduling.Api.dll");
        Console.WriteLine($"[E2E] Scheduling DLL: {schedulingDll}");
        Console.WriteLine($"[E2E] Scheduling DLL exists: {File.Exists(schedulingDll)}");
        _schedulingProcess = StartApiFromDll(schedulingDll, schedulingProjectDir, SchedulingUrl);

        // Give the processes a moment to start before polling
        await Task.Delay(2000);

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

    private static Process StartApiFromDll(string dllPath, string contentRoot, string url)
    {
        Console.WriteLine($"[E2E] Starting API: dotnet \"{dllPath}\" --urls \"{url}\" --contentRoot \"{contentRoot}\"");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\" --urls \"{url}\" --contentRoot \"{contentRoot}\"",
                WorkingDirectory = contentRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine($"[API {url}] {e.Data}");
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine($"[API {url} ERR] {e.Data}");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
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
            // Use shell to kill -9 any process on the port
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = $"-c \"lsof -ti :{port} | xargs kill -9 2>/dev/null || true\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is not null)
                await process.WaitForExitAsync();

            await Task.Delay(500);
        }
        catch
        { /* ignore */
        }
    }

    private static async Task WaitForApiAsync(string baseUrl, string healthEndpoint)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var maxRetries = 120; // Give dotnet run more time to build and start (60 seconds)

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

        // Seed FHIR-compliant Practitioner with all required fields
        var practitionerResponse = await client.PostAsync(
            $"{SchedulingUrl}/Practitioner",
            new StringContent(
                """{"Identifier": "DR001", "Active": true, "NameGiven": "E2EPractitioner", "NameFamily": "DrTest", "Qualification": "MD", "Specialty": "General Practice", "TelecomEmail": "drtest@hospital.org", "TelecomPhone": "+1-555-0123"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        practitionerResponse.EnsureSuccessStatusCode();

        // Seed additional practitioners for realistic data
        var practitioner2Response = await client.PostAsync(
            $"{SchedulingUrl}/Practitioner",
            new StringContent(
                """{"Identifier": "DR002", "Active": true, "NameGiven": "Sarah", "NameFamily": "Johnson", "Qualification": "DO", "Specialty": "Cardiology", "TelecomEmail": "sjohnson@hospital.org", "TelecomPhone": "+1-555-0124"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        practitioner2Response.EnsureSuccessStatusCode();

        var practitioner3Response = await client.PostAsync(
            $"{SchedulingUrl}/Practitioner",
            new StringContent(
                """{"Identifier": "DR003", "Active": true, "NameGiven": "Michael", "NameFamily": "Chen", "Qualification": "MD", "Specialty": "Neurology", "TelecomEmail": "mchen@hospital.org", "TelecomPhone": "+1-555-0125"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        practitioner3Response.EnsureSuccessStatusCode();

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
    /// Playwright browser loads Dashboard at localhost:5173 and verifies data from localhost:5080.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysPatientData_FromClinicalApi()
    {
        var page = await _fixture.Browser!.NewPageAsync();

        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");
        page.RequestFailed += (_, req) =>
            Console.WriteLine($"[NET FAILED] {req.Url} - {req.Failure}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "text=TestPatient",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("TestPatient", content);
        Assert.Contains("E2ETest", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Dashboard loads and displays practitioner data from Scheduling API.
    /// Verifies FHIR-compliant practitioner data including Qualification and Specialty.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysPractitionerData_FromSchedulingApi()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Practitioners page
        await page.ClickAsync("text=Practitioners");

        // Wait for practitioner cards to load - should show all seeded practitioners
        await page.WaitForSelectorAsync(
            "text=DrTest",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.WaitForSelectorAsync(
            ".practitioner-card",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        var content = await page.ContentAsync();

        // Verify first practitioner (E2EPractitioner DrTest)
        Assert.Contains("DrTest", content);
        Assert.Contains("E2EPractitioner", content);

        // Verify additional practitioners
        Assert.Contains("Johnson", content);
        Assert.Contains("Sarah", content);
        Assert.Contains("Chen", content);
        Assert.Contains("Michael", content);

        // Verify FHIR qualification data displays
        Assert.Contains("MD", content);

        // Verify FHIR specialty data displays
        Assert.Contains("General Practice", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Practitioners page data comes from REAL Scheduling API.
    /// Directly verifies the API returns FHIR-compliant data.
    /// </summary>
    [Fact]
    public async Task PractitionersPage_LoadsFromSchedulingApi_WithFhirCompliantData()
    {
        // First verify the API directly returns FHIR data
        using var client = new HttpClient();
        var apiResponse = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Practitioner");

        // API should return all seeded practitioners with FHIR fields
        Assert.Contains("DR001", apiResponse);
        Assert.Contains("E2EPractitioner", apiResponse);
        Assert.Contains("DrTest", apiResponse);
        Assert.Contains("MD", apiResponse);
        Assert.Contains("General Practice", apiResponse);

        // Now verify the Dashboard UI displays this data
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync(
            ".practitioner-card",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Count practitioner cards - should have at least 3 from seeded data
        var cards = await page.QuerySelectorAllAsync(".practitioner-card");
        Assert.True(cards.Count >= 3, $"Expected at least 3 practitioner cards, got {cards.Count}");

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Dashboard loads and displays appointment data from Scheduling API.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysAppointmentData_FromSchedulingApi()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        await page.GotoAsync(E2EFixture.DashboardUrl);
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

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Dashboard main page shows stats from both APIs.
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

    /// <summary>
    /// CRITICAL TEST: Add Patient button opens modal and creates patient via API.
    /// Uses Playwright to load REAL Dashboard, click Add Patient, fill form, and POST to REAL API.
    /// </summary>
    [Fact]
    public async Task AddPatientButton_OpensModal_AndCreatesPatient()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Patients page
        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click Add Patient button
        await page.ClickAsync("[data-testid='add-patient-btn']");

        // Wait for modal to appear
        await page.WaitForSelectorAsync(
            ".modal",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Fill in patient details
        var uniqueName = $"E2ECreated{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='patient-given-name']", uniqueName);
        await page.FillAsync("[data-testid='patient-family-name']", "TestCreated");
        await page.SelectOptionAsync("[data-testid='patient-gender']", "male");

        // Submit the form
        await page.ClickAsync("[data-testid='submit-patient']");

        // Wait for modal to close and patient to appear in list
        await page.WaitForSelectorAsync(
            $"text={uniqueName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify via API that patient was actually created
        using var client = new HttpClient();
        var response = await client.GetStringAsync($"{E2EFixture.ClinicalUrl}/fhir/Patient/");
        Assert.Contains(uniqueName, response);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Add Appointment button opens modal and creates appointment via API.
    /// Uses Playwright to load REAL Dashboard, click Add Appointment, fill form, and POST to REAL API.
    /// </summary>
    [Fact]
    public async Task AddAppointmentButton_OpensModal_AndCreatesAppointment()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Appointments page
        await page.ClickAsync("text=Appointments");
        await page.WaitForSelectorAsync(
            "[data-testid='add-appointment-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click Add Appointment button
        await page.ClickAsync("[data-testid='add-appointment-btn']");

        // Wait for modal to appear
        await page.WaitForSelectorAsync(
            ".modal",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Fill in appointment details
        var uniqueServiceType = $"E2EConsult{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='appointment-service-type']", uniqueServiceType);

        // Submit the form
        await page.ClickAsync("[data-testid='submit-appointment']");

        // Wait for modal to close and appointment to appear in list
        await page.WaitForSelectorAsync(
            $"text={uniqueServiceType}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify via API that appointment was actually created
        using var client = new HttpClient();
        var response = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Appointment");
        Assert.Contains(uniqueServiceType, response);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Patient Search button navigates to search and finds patients.
    /// </summary>
    [Fact]
    public async Task PatientSearchButton_NavigatesToSearch_AndFindsPatients()
    {
        var page = await _fixture.Browser!.NewPageAsync();

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Click the Patient Search button
        await page.ClickAsync("text=Patient Search");

        // Should navigate to patients page with search focused
        await page.WaitForSelectorAsync(
            "input[placeholder*='Search']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Type a search query
        await page.FillAsync("input[placeholder*='Search']", "E2ETest");

        // Wait for filtered results
        await page.WaitForSelectorAsync(
            "text=TestPatient",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("TestPatient", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: View Schedule button navigates to appointments view.
    /// </summary>
    [Fact]
    public async Task ViewScheduleButton_NavigatesToAppointments()
    {
        var page = await _fixture.Browser!.NewPageAsync();

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Click the View Schedule button
        await page.ClickAsync("text=View Schedule");

        // Should navigate to appointments page
        await page.WaitForSelectorAsync(
            "text=Appointments",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Should show the seeded appointment
        await page.WaitForSelectorAsync(
            "text=Checkup",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("Checkup", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Proves patient creation API works end-to-end.
    /// This test hits the real Clinical API directly without Playwright.
    /// </summary>
    [Fact]
    public async Task PatientCreationApi_WorksEndToEnd()
    {
        using var client = new HttpClient();

        // Create a patient with a unique name
        var uniqueName = $"ApiTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "ApiCreated", "Gender": "female"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();

        // Verify patient was created by fetching all patients
        var listResponse = await client.GetStringAsync($"{E2EFixture.ClinicalUrl}/fhir/Patient/");
        Assert.Contains(uniqueName, listResponse);
        Assert.Contains("ApiCreated", listResponse);
    }

    /// <summary>
    /// CRITICAL TEST: Proves practitioner creation API works end-to-end.
    /// This test hits the real Scheduling API directly without Playwright.
    /// </summary>
    [Fact]
    public async Task PractitionerCreationApi_WorksEndToEnd()
    {
        using var client = new HttpClient();

        // Create a practitioner with a unique identifier
        var uniqueId = $"DR{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueId}}}", "Active": true, "NameGiven": "ApiDoctor", "NameFamily": "TestDoc", "Qualification": "MD", "Specialty": "Testing", "TelecomEmail": "test@hospital.org", "TelecomPhone": "+1-555-9999"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();

        // Verify practitioner was created
        var listResponse = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Practitioner");
        Assert.Contains(uniqueId, listResponse);
        Assert.Contains("ApiDoctor", listResponse);
    }

    /// <summary>
    /// CRITICAL TEST: Edit Patient button opens edit page and updates patient via API.
    /// Uses Playwright to load REAL Dashboard, click Edit, modify form, and PUT to REAL API.
    /// </summary>
    [Fact]
    public async Task EditPatientButton_OpensEditPage_AndUpdatesPatient()
    {
        using var client = new HttpClient();

        // First create a patient to edit
        var uniqueName = $"EditTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "ToBeEdited", "Gender": "female"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdPatientJson = await createResponse.Content.ReadAsStringAsync();

        // Extract patient ID from response
        var patientIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdPatientJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        Assert.True(patientIdMatch.Success, "Should get patient ID from creation response");
        var patientId = patientIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Patients page
        await page.ClickAsync("text=Patients");

        // Wait for the page to load (add-patient-btn is a good indicator)
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Search for the patient to make sure it appears
        await page.FillAsync("input[placeholder*='Search']", uniqueName);

        // Wait for the patient to appear in filtered results
        await page.WaitForSelectorAsync(
            $"text={uniqueName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click Edit button for the created patient
        await page.ClickAsync($"[data-testid='edit-patient-{patientId}']");

        // Wait for edit page to load
        await page.WaitForSelectorAsync(
            "[data-testid='edit-patient-page']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Verify we're on the edit page with the correct patient data
        await page.WaitForSelectorAsync(
            "[data-testid='edit-given-name']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Modify the patient's name
        var newFamilyName = $"Edited{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='edit-family-name']", newFamilyName);

        // Submit the form
        await page.ClickAsync("[data-testid='save-patient']");

        // Wait for success message
        await page.WaitForSelectorAsync(
            "[data-testid='edit-success']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify via API that patient was actually updated
        var updatedPatientJson = await client.GetStringAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/{patientId}"
        );
        Assert.Contains(newFamilyName, updatedPatientJson);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Browser back button navigates to previous view.
    /// Proves history.pushState/popstate integration works correctly.
    /// </summary>
    [Fact]
    public async Task BrowserBackButton_NavigatesToPreviousView()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Start on dashboard (default)
        Assert.Contains("#dashboard", page.Url);

        // Navigate to Patients
        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        // Navigate to Appointments
        await page.ClickAsync("text=Appointments");
        await page.WaitForSelectorAsync(
            "[data-testid='add-appointment-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#appointments", page.Url);

        // Press browser back - should go to Patients
        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        // Press browser back again - should go to Dashboard
        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            ".metric-card",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#dashboard", page.Url);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Deep linking works - navigating directly to a hash URL loads correct view.
    /// </summary>
    [Fact]
    public async Task DeepLinking_LoadsCorrectView()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Navigate directly to patients page via hash
        await page.GotoAsync($"{E2EFixture.DashboardUrl}#patients");
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify we're on patients page
        var content = await page.ContentAsync();
        Assert.Contains("Patients", content);

        // Navigate directly to appointments via hash
        await page.GotoAsync($"{E2EFixture.DashboardUrl}#appointments");
        await page.WaitForSelectorAsync(
            "[data-testid='add-appointment-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        content = await page.ContentAsync();
        Assert.Contains("Appointments", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Cancel button on edit page uses history.back() - same behavior as browser back.
    /// </summary>
    [Fact]
    public async Task EditPatientCancelButton_UsesHistoryBack()
    {
        using var client = new HttpClient();

        // Create a patient to edit
        var uniqueName = $"CancelTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "CancelTestPatient", "Gender": "male"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdJson = await createResponse.Content.ReadAsStringAsync();
        var patientIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        var patientId = patientIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Patients
        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        // Search for and click edit on the patient
        await page.FillAsync("input[placeholder*='Search']", uniqueName);
        await page.WaitForSelectorAsync(
            $"text={uniqueName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.ClickAsync($"[data-testid='edit-patient-{patientId}']");

        // Wait for edit page
        await page.WaitForSelectorAsync(
            "[data-testid='edit-patient-page']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        Assert.Contains($"#patients/edit/{patientId}", page.Url);

        // Click Cancel button - should use history.back() and return to patients list
        await page.ClickAsync("button:has-text('Cancel')");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Should be back on patients page
        Assert.Contains("#patients", page.Url);
        Assert.DoesNotContain("/edit/", page.Url);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Browser back button works from Edit Patient page.
    /// This is THE test that proves the original bug is fixed - pressing browser back
    /// from an edit page should return to patients list, NOT show a blank page.
    /// </summary>
    [Fact]
    public async Task BrowserBackButton_FromEditPage_ReturnsToPatientsPage()
    {
        using var client = new HttpClient();

        // Create a patient to edit
        var uniqueName = $"BackBtnTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "BackButtonTest", "Gender": "female"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdJson = await createResponse.Content.ReadAsStringAsync();
        var patientIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        var patientId = patientIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Start at dashboard
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        Assert.Contains("#dashboard", page.Url);

        // Navigate to Patients
        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        // Search for the patient
        await page.FillAsync("input[placeholder*='Search']", uniqueName);
        await page.WaitForSelectorAsync(
            $"text={uniqueName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click edit to go to edit page
        await page.ClickAsync($"[data-testid='edit-patient-{patientId}']");
        await page.WaitForSelectorAsync(
            "[data-testid='edit-patient-page']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        Assert.Contains($"#patients/edit/{patientId}", page.Url);

        // THE CRITICAL TEST: Press browser back button
        // Before the fix, this would show a blank "Guest browsing" page
        // After the fix, it should return to the patients list
        await page.GoBackAsync();

        // Should be back on patients page with sidebar visible (NOT a blank page)
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);
        Assert.DoesNotContain("/edit/", page.Url);

        // Verify the page content is actually the patients page, not blank
        var content = await page.ContentAsync();
        Assert.Contains("Patients", content);
        Assert.Contains("Add Patient", content);

        // Press back again - should go to dashboard
        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            ".metric-card",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#dashboard", page.Url);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Forward button works after going back.
    /// Proves full history navigation (back AND forward) works correctly.
    /// </summary>
    [Fact]
    public async Task BrowserForwardButton_WorksAfterGoingBack()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate: Dashboard -> Patients -> Practitioners
        await page.ClickAsync("text=Patients");
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync(
            ".practitioner-card, .empty-state",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#practitioners", page.Url);

        // Go back to Patients
        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            "[data-testid='add-patient-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#patients", page.Url);

        // Go forward to Practitioners
        await page.GoForwardAsync();
        await page.WaitForSelectorAsync(
            ".practitioner-card, .empty-state",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#practitioners", page.Url);

        // Verify page content is actually practitioners page
        var content = await page.ContentAsync();
        Assert.Contains("Practitioners", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Proves patient update API works end-to-end.
    /// This test hits the real Clinical API directly without Playwright.
    /// </summary>
    [Fact]
    public async Task PatientUpdateApi_WorksEndToEnd()
    {
        using var client = new HttpClient();

        // Create a patient first
        var uniqueName = $"UpdateApiTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/",
            new StringContent(
                $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "Original", "Gender": "male"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdPatientJson = await createResponse.Content.ReadAsStringAsync();

        // Extract patient ID
        var patientIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdPatientJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        Assert.True(patientIdMatch.Success, "Should get patient ID from creation response");
        var patientId = patientIdMatch.Groups[1].Value;

        // Update the patient
        var updatedFamilyName = $"Updated{DateTime.UtcNow.Ticks % 100000}";
        var updateResponse = await client.PutAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/{patientId}",
            new StringContent(
                $$$"""{"Active": true, "GivenName": "{{{uniqueName}}}", "FamilyName": "{{{updatedFamilyName}}}", "Gender": "male", "Email": "updated@test.com"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        updateResponse.EnsureSuccessStatusCode();

        // Verify patient was updated
        var getResponse = await client.GetStringAsync(
            $"{E2EFixture.ClinicalUrl}/fhir/Patient/{patientId}"
        );
        Assert.Contains(updatedFamilyName, getResponse);
        Assert.Contains("updated@test.com", getResponse);
    }

    /// <summary>
    /// CRITICAL TEST: Add Practitioner button opens modal and creates practitioner via API.
    /// Uses Playwright to load REAL Dashboard, click Add Practitioner, fill form, and POST to REAL API.
    /// </summary>
    [Fact]
    public async Task AddPractitionerButton_OpensModal_AndCreatesPractitioner()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Practitioners page
        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync(
            "[data-testid='add-practitioner-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click Add Practitioner button
        await page.ClickAsync("[data-testid='add-practitioner-btn']");

        // Wait for modal to appear
        await page.WaitForSelectorAsync(
            ".modal",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        // Fill in practitioner details
        var uniqueIdentifier = $"DR{DateTime.UtcNow.Ticks % 100000}";
        var uniqueGivenName = $"E2EDoc{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='practitioner-identifier']", uniqueIdentifier);
        await page.FillAsync("[data-testid='practitioner-given-name']", uniqueGivenName);
        await page.FillAsync("[data-testid='practitioner-family-name']", "TestCreated");
        await page.FillAsync("[data-testid='practitioner-specialty']", "E2E Testing");

        // Submit the form
        await page.ClickAsync("[data-testid='submit-practitioner']");

        // Wait for modal to close and practitioner to appear in list
        await page.WaitForSelectorAsync(
            $"text={uniqueGivenName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify via API that practitioner was actually created
        using var client = new HttpClient();
        var response = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Practitioner");
        Assert.Contains(uniqueIdentifier, response);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Edit Practitioner button navigates to edit page and updates practitioner.
    /// Uses Playwright to load REAL Dashboard, click Edit, modify data, and PUT to REAL API.
    /// </summary>
    [Fact]
    public async Task EditPractitionerButton_OpensEditPage_AndUpdatesPractitioner()
    {
        using var client = new HttpClient();

        // Create a practitioner to edit
        var uniqueIdentifier = $"DREdit{DateTime.UtcNow.Ticks % 100000}";
        var uniqueGivenName = $"EditTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueIdentifier}}}", "NameFamily": "OriginalFamily", "NameGiven": "{{{uniqueGivenName}}}", "Qualification": "MD", "Specialty": "Original Specialty"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdJson = await createResponse.Content.ReadAsStringAsync();
        var practitionerIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        var practitionerId = practitionerIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        // Navigate to Practitioners page
        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync(
            "[data-testid='add-practitioner-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Wait for our practitioner to appear
        await page.WaitForSelectorAsync(
            $"text={uniqueGivenName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Hover over the card to show the edit button, then click it
        var editButton = page.Locator($"[data-testid='edit-practitioner-{practitionerId}']");
        await editButton.HoverAsync();
        await editButton.ClickAsync();

        // Wait for edit page
        await page.WaitForSelectorAsync(
            "[data-testid='edit-practitioner-page']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        Assert.Contains($"#practitioners/edit/{practitionerId}", page.Url);

        // Update the practitioner's specialty
        var newSpecialty = $"Updated Specialty {DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='edit-practitioner-specialty']", newSpecialty);

        // Save changes
        await page.ClickAsync("[data-testid='save-practitioner']");

        // Wait for success message
        await page.WaitForSelectorAsync(
            "[data-testid='edit-practitioner-success']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Verify via API that practitioner was actually updated
        var updatedPractitionerJson = await client.GetStringAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner/{practitionerId}"
        );
        Assert.Contains(newSpecialty, updatedPractitionerJson);

        await page.CloseAsync();
    }

    /// <summary>
    /// CRITICAL TEST: Proves practitioner update API works end-to-end.
    /// This test hits the real Scheduling API directly without Playwright.
    /// </summary>
    [Fact]
    public async Task PractitionerUpdateApi_WorksEndToEnd()
    {
        using var client = new HttpClient();

        // Create a practitioner first
        var uniqueIdentifier = $"DRApi{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueIdentifier}}}", "NameFamily": "ApiOriginal", "NameGiven": "TestDoc", "Qualification": "MD", "Specialty": "Original"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdPractitionerJson = await createResponse.Content.ReadAsStringAsync();

        // Extract practitioner ID
        var practitionerIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdPractitionerJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        Assert.True(practitionerIdMatch.Success, "Should get practitioner ID from creation response");
        var practitionerId = practitionerIdMatch.Groups[1].Value;

        // Update the practitioner
        var updatedSpecialty = $"ApiUpdated{DateTime.UtcNow.Ticks % 100000}";
        var updateResponse = await client.PutAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner/{practitionerId}",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueIdentifier}}}", "Active": true, "NameFamily": "ApiUpdated", "NameGiven": "TestDoc", "Qualification": "DO", "Specialty": "{{{updatedSpecialty}}}", "TelecomEmail": "updated@hospital.com", "TelecomPhone": "555-1234"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        updateResponse.EnsureSuccessStatusCode();

        // Verify practitioner was updated
        var getResponse = await client.GetStringAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner/{practitionerId}"
        );
        Assert.Contains(updatedSpecialty, getResponse);
        Assert.Contains("ApiUpdated", getResponse);
        Assert.Contains("DO", getResponse);
        Assert.Contains("updated@hospital.com", getResponse);
    }

    /// <summary>
    /// CRITICAL TEST: Browser back button works from Edit Practitioner page.
    /// Proves navigation between practitioners list and edit page works correctly.
    /// </summary>
    [Fact]
    public async Task BrowserBackButton_FromEditPractitionerPage_ReturnsToPractitionersPage()
    {
        using var client = new HttpClient();

        // Create a practitioner to edit
        var uniqueIdentifier = $"DRBack{DateTime.UtcNow.Ticks % 100000}";
        var uniqueGivenName = $"BackTest{DateTime.UtcNow.Ticks % 100000}";
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Practitioner",
            new StringContent(
                $$$"""{"Identifier": "{{{uniqueIdentifier}}}", "NameFamily": "BackButtonTest", "NameGiven": "{{{uniqueGivenName}}}", "Qualification": "MD", "Specialty": "Testing"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdJson = await createResponse.Content.ReadAsStringAsync();
        var practitionerIdMatch = System.Text.RegularExpressions.Regex.Match(
            createdJson,
            "\"Id\"\\s*:\\s*\"([^\"]+)\""
        );
        var practitionerId = practitionerIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        // Start at dashboard
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        Assert.Contains("#dashboard", page.Url);

        // Navigate to Practitioners
        await page.ClickAsync("text=Practitioners");
        await page.WaitForSelectorAsync(
            "[data-testid='add-practitioner-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#practitioners", page.Url);

        // Wait for our practitioner to appear
        await page.WaitForSelectorAsync(
            $"text={uniqueGivenName}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        // Click edit to go to edit page
        var editButton = page.Locator($"[data-testid='edit-practitioner-{practitionerId}']");
        await editButton.HoverAsync();
        await editButton.ClickAsync();
        await page.WaitForSelectorAsync(
            "[data-testid='edit-practitioner-page']",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        Assert.Contains($"#practitioners/edit/{practitionerId}", page.Url);

        // Press browser back button
        await page.GoBackAsync();

        // Should be back on practitioners page with sidebar visible
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.WaitForSelectorAsync(
            "[data-testid='add-practitioner-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#practitioners", page.Url);
        Assert.DoesNotContain("/edit/", page.Url);

        // Verify the page content is actually the practitioners page
        var content = await page.ContentAsync();
        Assert.Contains("Practitioners", content);
        Assert.Contains("Add Practitioner", content);

        // Press back again - should go to dashboard
        await page.GoBackAsync();
        await page.WaitForSelectorAsync(
            ".metric-card",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        Assert.Contains("#dashboard", page.Url);

        await page.CloseAsync();
    }
}
