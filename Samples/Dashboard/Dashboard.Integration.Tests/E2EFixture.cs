using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private Process? _gatekeeperProcess;
    private Process? _clinicalSyncProcess;
    private Process? _schedulingSyncProcess;
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
    /// Gatekeeper Auth API URL - SAME as real app default.
    /// </summary>
    public const string GatekeeperUrl = "http://localhost:5002";

    /// <summary>
    /// Dashboard URL - SAME as real app default.
    /// Uses testMode=true to bypass authentication in tests.
    /// </summary>
    public const string DashboardUrl = "http://localhost:5173?testMode=true";

    /// <summary>
    /// Dashboard URL without test mode - for auth tests.
    /// </summary>
    public const string DashboardUrlNoTestMode = "http://localhost:5173";

    /// <summary>
    /// Start all services ONCE for all tests.
    /// </summary>
    public async Task InitializeAsync()
    {
        await KillProcessOnPortAsync(5080);
        await KillProcessOnPortAsync(5001);
        await KillProcessOnPortAsync(5002);
        await KillProcessOnPortAsync(5173);
        await Task.Delay(2000);

        var testAssemblyDir = Path.GetDirectoryName(typeof(E2EFixture).Assembly.Location)!;
        var samplesDir = Path.GetFullPath(
            Path.Combine(testAssemblyDir, "..", "..", "..", "..", "..")
        );
        var rootDir = Path.GetFullPath(Path.Combine(samplesDir, ".."));
        var clinicalProjectDir = Path.Combine(samplesDir, "Clinical", "Clinical.Api");
        var schedulingProjectDir = Path.Combine(samplesDir, "Scheduling", "Scheduling.Api");
        var gatekeeperProjectDir = Path.Combine(rootDir, "Gatekeeper", "Gatekeeper.Api");

        // Delete existing databases to ensure fresh state for each test run
        // This prevents sync version mismatch issues between runs
        DeleteDatabaseIfExists(clinicalProjectDir, "clinical.db");
        DeleteDatabaseIfExists(schedulingProjectDir, "scheduling.db");
        DeleteDatabaseIfExists(gatekeeperProjectDir, "gatekeeper.db");

        Console.WriteLine($"[E2E] Test assembly dir: {testAssemblyDir}");
        Console.WriteLine($"[E2E] Samples dir: {samplesDir}");
        Console.WriteLine($"[E2E] Clinical dir: {clinicalProjectDir}");
        Console.WriteLine($"[E2E] Gatekeeper dir: {gatekeeperProjectDir}");

        var clinicalDll = Path.Combine(
            clinicalProjectDir,
            "bin",
            "Debug",
            "net9.0",
            "Clinical.Api.dll"
        );
        _clinicalProcess = StartApiFromDll(clinicalDll, clinicalProjectDir, ClinicalUrl);

        var schedulingDll = Path.Combine(
            schedulingProjectDir,
            "bin",
            "Debug",
            "net9.0",
            "Scheduling.Api.dll"
        );
        _schedulingProcess = StartApiFromDll(schedulingDll, schedulingProjectDir, SchedulingUrl);

        var gatekeeperDll = Path.Combine(
            gatekeeperProjectDir,
            "bin",
            "Debug",
            "net9.0",
            "Gatekeeper.Api.dll"
        );
        _gatekeeperProcess = StartApiFromDll(gatekeeperDll, gatekeeperProjectDir, GatekeeperUrl);

        await Task.Delay(2000);

        await WaitForApiAsync(ClinicalUrl, "/fhir/Patient/");
        await WaitForApiAsync(SchedulingUrl, "/Practitioner");
        await WaitForGatekeeperApiAsync();

        var clinicalDbPath = Path.Combine(
            clinicalProjectDir,
            "bin",
            "Debug",
            "net9.0",
            "clinical.db"
        );
        var schedulingDbPath = Path.Combine(
            schedulingProjectDir,
            "bin",
            "Debug",
            "net9.0",
            "scheduling.db"
        );

        var clinicalSyncDir = Path.Combine(samplesDir, "Clinical", "Clinical.Sync");
        var clinicalSyncDll = Path.Combine(
            clinicalSyncDir,
            "bin",
            "Debug",
            "net9.0",
            "Clinical.Sync.dll"
        );
        if (File.Exists(clinicalSyncDll))
        {
            var clinicalSyncEnv = new Dictionary<string, string>
            {
                ["CLINICAL_DB_PATH"] = clinicalDbPath,
                ["SCHEDULING_API_URL"] = SchedulingUrl,
                ["POLL_INTERVAL_SECONDS"] = "5", // Fast polling for E2E tests
            };
            _clinicalSyncProcess = StartSyncWorker(
                clinicalSyncDll,
                clinicalSyncDir,
                clinicalSyncEnv
            );
        }

        var schedulingSyncDir = Path.Combine(samplesDir, "Scheduling", "Scheduling.Sync");
        var schedulingSyncDll = Path.Combine(
            schedulingSyncDir,
            "bin",
            "Debug",
            "net9.0",
            "Scheduling.Sync.dll"
        );
        if (File.Exists(schedulingSyncDll))
        {
            var schedulingSyncEnv = new Dictionary<string, string>
            {
                ["SCHEDULING_DB_PATH"] = schedulingDbPath,
                ["CLINICAL_API_URL"] = ClinicalUrl,
                ["POLL_INTERVAL_SECONDS"] = "5", // Fast polling for E2E tests
            };
            _schedulingSyncProcess = StartSyncWorker(
                schedulingSyncDll,
                schedulingSyncDir,
                schedulingSyncEnv
            );
        }

        await Task.Delay(2000);

        _dashboardHost = CreateDashboardHost();
        await _dashboardHost.StartAsync();

        await SeedTestDataAsync();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true }
        );
    }

    /// <summary>
    /// Stop all services ONCE after all tests.
    /// Order matters: stop sync workers FIRST to prevent connection errors.
    /// </summary>
    public async Task DisposeAsync()
    {
        try
        {
            if (Browser is not null)
                await Browser.CloseAsync();
        }
        catch { }
        Playwright?.Dispose();

        StopProcess(_clinicalSyncProcess);
        StopProcess(_schedulingSyncProcess);
        await Task.Delay(1000);

        try
        {
            if (_dashboardHost is not null)
                await _dashboardHost.StopAsync(TimeSpan.FromSeconds(5));
        }
        catch { }
        _dashboardHost?.Dispose();

        StopProcess(_clinicalProcess);
        StopProcess(_schedulingProcess);
        StopProcess(_gatekeeperProcess);

        await KillProcessOnPortAsync(5080);
        await KillProcessOnPortAsync(5001);
        await KillProcessOnPortAsync(5002);
        await KillProcessOnPortAsync(5173);
    }

    private static Process StartApiFromDll(string dllPath, string contentRoot, string url)
    {
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

    private static Process StartSyncWorker(
        string dllPath,
        string workingDir,
        Dictionary<string, string>? envVars = null
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\"",
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (envVars is not null)
        {
            foreach (var kvp in envVars)
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
        }

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine($"[SYNC] {e.Data}");
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine($"[SYNC ERR] {e.Data}");
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
        catch { }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task KillProcessOnPortAsync(int port)
    {
        // Try multiple times to ensure port is released
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
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
            }
            catch { }
            await Task.Delay(1000);

            // Verify port is free
            if (await IsPortAvailableAsync(port))
                return;
        }
    }

    private static Task<bool> IsPortAvailableAsync(int port)
    {
        try
        {
            using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static void DeleteDatabaseIfExists(string projectDir, string dbName)
    {
        var dbPath = Path.Combine(projectDir, "bin", "Debug", "net9.0", dbName);
        if (File.Exists(dbPath))
        {
            try
            {
                File.Delete(dbPath);
                Console.WriteLine($"[E2E] Deleted database: {dbPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[E2E] Could not delete {dbPath}: {ex.Message}");
            }
        }
    }

    private static async Task WaitForApiAsync(string baseUrl, string healthEndpoint)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var i = 0; i < 120; i++)
        {
            try
            {
                var response = await client.GetAsync($"{baseUrl}{healthEndpoint}");
                if (
                    response.IsSuccessStatusCode
                    || response.StatusCode == HttpStatusCode.NotFound
                    || response.StatusCode == HttpStatusCode.Unauthorized
                    || response.StatusCode == HttpStatusCode.Forbidden
                )
                    return;
            }
            catch { }
            await Task.Delay(500);
        }
        throw new TimeoutException($"API at {baseUrl} did not start");
    }

    private static async Task WaitForGatekeeperApiAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var i = 0; i < 120; i++)
        {
            try
            {
                var response = await client.PostAsync(
                    $"{GatekeeperUrl}/auth/login/begin",
                    new StringContent("{}", Encoding.UTF8, "application/json")
                );
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch { }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Gatekeeper API did not start");
    }

    /// <summary>
    /// Creates an authenticated HTTP client with test JWT token.
    /// </summary>
    public static HttpClient CreateAuthenticatedClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateTestToken());
        return client;
    }

    /// <summary>
    /// Generates a test JWT token with the specified user details.
    /// Uses the same all-zeros signing key that the APIs use in dev mode.
    /// </summary>
    public static string GenerateTestToken(
        string userId = "e2e-test-user",
        string displayName = "E2E Test User",
        string email = "e2etest@example.com"
    )
    {
        var signingKey = new byte[32];
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));
        var expiration = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var payload = Base64UrlEncode(
            Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(
                    new
                    {
                        sub = userId,
                        name = displayName,
                        email,
                        jti = Guid.NewGuid().ToString(),
                        exp = expiration,
                        roles = new[] { "admin", "user" },
                    }
                )
            )
        );
        var signature = ComputeHmacSignature(header, payload, signingKey);
        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).Replace("+", "-").Replace("/", "_").TrimEnd('=');

    private static string ComputeHmacSignature(string header, string payload, byte[] key)
    {
        var data = Encoding.UTF8.GetBytes($"{header}.{payload}");
        using var hmac = new HMACSHA256(key);
        return Base64UrlEncode(hmac.ComputeHash(data));
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
        using var client = CreateAuthenticatedClient();

        await client.PostAsync(
            $"{ClinicalUrl}/fhir/Patient/",
            new StringContent(
                """{"Active": true, "GivenName": "E2ETest", "FamilyName": "TestPatient", "Gender": "other"}""",
                Encoding.UTF8,
                "application/json"
            )
        );

        await client.PostAsync(
            $"{SchedulingUrl}/Practitioner",
            new StringContent(
                """{"Identifier": "DR001", "Active": true, "NameGiven": "E2EPractitioner", "NameFamily": "DrTest", "Qualification": "MD", "Specialty": "General Practice", "TelecomEmail": "drtest@hospital.org", "TelecomPhone": "+1-555-0123"}""",
                Encoding.UTF8,
                "application/json"
            )
        );

        await client.PostAsync(
            $"{SchedulingUrl}/Practitioner",
            new StringContent(
                """{"Identifier": "DR002", "Active": true, "NameGiven": "Sarah", "NameFamily": "Johnson", "Qualification": "DO", "Specialty": "Cardiology", "TelecomEmail": "sjohnson@hospital.org", "TelecomPhone": "+1-555-0124"}""",
                Encoding.UTF8,
                "application/json"
            )
        );

        await client.PostAsync(
            $"{SchedulingUrl}/Practitioner",
            new StringContent(
                """{"Identifier": "DR003", "Active": true, "NameGiven": "Michael", "NameFamily": "Chen", "Qualification": "MD", "Specialty": "Neurology", "TelecomEmail": "mchen@hospital.org", "TelecomPhone": "+1-555-0125"}""",
                Encoding.UTF8,
                "application/json"
            )
        );

        await client.PostAsync(
            $"{SchedulingUrl}/Appointment",
            new StringContent(
                """{"ServiceCategory": "General", "ServiceType": "Checkup", "Start": "2025-12-20T10:00:00Z", "End": "2025-12-20T11:00:00Z", "PatientReference": "Patient/1", "PractitionerReference": "Practitioner/1", "Priority": "routine"}""",
                Encoding.UTF8,
                "application/json"
            )
        );
    }
}

/// <summary>
/// Single collection definition for ALL E2E tests.
/// All tests share ONE E2EFixture instance to prevent port conflicts.
/// Tests within this collection run sequentially by default.
/// </summary>
[CollectionDefinition("E2E Tests")]
public sealed class E2ECollection : ICollectionFixture<E2EFixture>;
