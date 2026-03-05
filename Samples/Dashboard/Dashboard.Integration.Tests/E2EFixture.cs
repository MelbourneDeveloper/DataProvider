using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Dashboard.Integration.Tests;

/// <summary>
/// Shared fixture that starts all services ONCE for all E2E tests.
/// Set E2E_USE_LOCAL=true to skip Testcontainers/process startup and run against
/// an already-running local dev stack (started via scripts/start-local.sh).
/// </summary>
public sealed class E2EFixture : IAsyncLifetime
{
    /// <summary>
    /// When true, tests run against an already-running local dev stack
    /// instead of spinning up Testcontainers and API processes.
    /// </summary>
    private static readonly bool UseLocalStack =
        Environment.GetEnvironmentVariable("E2E_USE_LOCAL") is "true" or "1";

    private PostgreSqlContainer? _postgresContainer;
    private Process? _clinicalProcess;
    private Process? _schedulingProcess;
    private Process? _gatekeeperProcess;
    private Process? _icd10Process;
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
    /// Clinical API URL. Override with E2E_CLINICAL_URL env var.
    /// </summary>
    public static string ClinicalUrl { get; } =
        Environment.GetEnvironmentVariable("E2E_CLINICAL_URL") ?? "http://localhost:5080";

    /// <summary>
    /// Scheduling API URL. Override with E2E_SCHEDULING_URL env var.
    /// </summary>
    public static string SchedulingUrl { get; } =
        Environment.GetEnvironmentVariable("E2E_SCHEDULING_URL") ?? "http://localhost:5001";

    /// <summary>
    /// Gatekeeper Auth API URL. Override with E2E_GATEKEEPER_URL env var.
    /// </summary>
    public static string GatekeeperUrl { get; } =
        Environment.GetEnvironmentVariable("E2E_GATEKEEPER_URL") ?? "http://localhost:5002";

    /// <summary>
    /// ICD-10 API URL. Override with E2E_ICD10_URL env var.
    /// </summary>
    public static string Icd10Url { get; } =
        Environment.GetEnvironmentVariable("E2E_ICD10_URL") ?? "http://localhost:5090";

    /// <summary>
    /// Dashboard URL - dynamically assigned in container mode, defaults to local in local mode.
    /// </summary>
    public static string DashboardUrl { get; private set; } = "http://localhost:5173";

    /// <summary>
    /// Start all services ONCE for all tests.
    /// When E2E_USE_LOCAL=true, skips all infrastructure and connects to already-running services.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (UseLocalStack)
        {
            Console.WriteLine("[E2E] LOCAL MODE: connecting to already-running services");
            Console.WriteLine($"[E2E]   Clinical:   {ClinicalUrl}");
            Console.WriteLine($"[E2E]   Scheduling: {SchedulingUrl}");
            Console.WriteLine($"[E2E]   Gatekeeper: {GatekeeperUrl}");
            Console.WriteLine($"[E2E]   ICD-10:     {Icd10Url}");
            Console.WriteLine($"[E2E]   Dashboard:  {DashboardUrl}");

            await WaitForServiceReachableAsync(ClinicalUrl, "/fhir/Patient/");
            await WaitForServiceReachableAsync(SchedulingUrl, "/Practitioner");
            await WaitForServiceReachableAsync(GatekeeperUrl, "/auth/login/begin");

            await SeedTestDataAsync();

            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            Browser = await Playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = true }
            );
            return;
        }

        await KillProcessOnPortAsync(5080);
        await KillProcessOnPortAsync(5001);
        await KillProcessOnPortAsync(5002);
        await Task.Delay(2000);

        // Start PostgreSQL container for all APIs (use pgvector for ICD-10 support)
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .WithDatabase("e2e_shared")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _postgresContainer.StartAsync();
        var baseConnStr = _postgresContainer.GetConnectionString();

        // Create separate databases for each API
        var clinicalConnStr = await CreateDatabaseAsync(baseConnStr, "clinical_e2e");
        var schedulingConnStr = await CreateDatabaseAsync(baseConnStr, "scheduling_e2e");
        var gatekeeperConnStr = await CreateDatabaseAsync(baseConnStr, "gatekeeper_e2e");
        var icd10ConnStr = await CreateDatabaseAsync(baseConnStr, "icd10_e2e");

        Console.WriteLine("[E2E] PostgreSQL container started");

        var testAssemblyDir = Path.GetDirectoryName(typeof(E2EFixture).Assembly.Location)!;
        var samplesDir = Path.GetFullPath(
            Path.Combine(testAssemblyDir, "..", "..", "..", "..", "..")
        );
        var rootDir = Path.GetFullPath(Path.Combine(samplesDir, ".."));
        var clinicalProjectDir = Path.Combine(samplesDir, "Clinical", "Clinical.Api");
        var schedulingProjectDir = Path.Combine(samplesDir, "Scheduling", "Scheduling.Api");
        var gatekeeperProjectDir = Path.Combine(rootDir, "Gatekeeper", "Gatekeeper.Api");
        var icd10ProjectDir = Path.Combine(samplesDir, "ICD10", "ICD10.Api");
        var configuration = ResolveBuildConfiguration(testAssemblyDir);

        Console.WriteLine($"[E2E] Test assembly dir: {testAssemblyDir}");
        Console.WriteLine($"[E2E] Build configuration: {configuration}");
        Console.WriteLine($"[E2E] Samples dir: {samplesDir}");
        Console.WriteLine($"[E2E] Clinical dir: {clinicalProjectDir}");
        Console.WriteLine($"[E2E] Gatekeeper dir: {gatekeeperProjectDir}");
        Console.WriteLine($"[E2E] ICD-10 dir: {icd10ProjectDir}");

        var clinicalDll = Path.Combine(
            clinicalProjectDir,
            "bin",
            configuration,
            "net10.0",
            "Clinical.Api.dll"
        );
        _clinicalProcess = StartApiFromDll(
            clinicalDll,
            clinicalProjectDir,
            ClinicalUrl,
            new Dictionary<string, string> { ["ConnectionStrings__Postgres"] = clinicalConnStr }
        );

        var schedulingDll = Path.Combine(
            schedulingProjectDir,
            "bin",
            configuration,
            "net10.0",
            "Scheduling.Api.dll"
        );
        _schedulingProcess = StartApiFromDll(
            schedulingDll,
            schedulingProjectDir,
            SchedulingUrl,
            new Dictionary<string, string> { ["ConnectionStrings__Postgres"] = schedulingConnStr }
        );

        var gatekeeperDll = Path.Combine(
            gatekeeperProjectDir,
            "bin",
            configuration,
            "net10.0",
            "Gatekeeper.Api.dll"
        );
        _gatekeeperProcess = StartApiFromDll(
            gatekeeperDll,
            gatekeeperProjectDir,
            GatekeeperUrl,
            new Dictionary<string, string> { ["ConnectionStrings__Postgres"] = gatekeeperConnStr }
        );

        // Start ICD-10 API (requires PostgreSQL with pgvector)
        var icd10Dll = Path.Combine(
            icd10ProjectDir,
            "bin",
            configuration,
            "net10.0",
            "ICD10.Api.dll"
        );
        if (File.Exists(icd10Dll))
        {
            _icd10Process = StartApiFromDll(
                icd10Dll,
                icd10ProjectDir,
                Icd10Url,
                new Dictionary<string, string> { ["ConnectionStrings__Postgres"] = icd10ConnStr }
            );
            Console.WriteLine($"[E2E] ICD-10 API starting on {Icd10Url}");
        }
        else
        {
            Console.WriteLine($"[E2E] ICD-10 API DLL missing: {icd10Dll}");
        }

        await Task.Delay(2000);

        await WaitForApiAsync(ClinicalUrl, "/fhir/Patient/");
        await WaitForApiAsync(SchedulingUrl, "/Practitioner");
        await WaitForGatekeeperApiAsync();
        if (_icd10Process is not null)
        {
            await WaitForApiAsync(Icd10Url, "/api/icd10/chapters");
        }

        var clinicalSyncDir = Path.Combine(samplesDir, "Clinical", "Clinical.Sync");
        var clinicalSyncDll = Path.Combine(
            clinicalSyncDir,
            "bin",
            configuration,
            "net10.0",
            "Clinical.Sync.dll"
        );
        if (File.Exists(clinicalSyncDll))
        {
            var clinicalSyncEnv = new Dictionary<string, string>
            {
                ["ConnectionStrings__Postgres"] = clinicalConnStr,
                ["SCHEDULING_API_URL"] = SchedulingUrl,
                ["POLL_INTERVAL_SECONDS"] = "5",
            };
            _clinicalSyncProcess = StartSyncWorker(
                clinicalSyncDll,
                clinicalSyncDir,
                clinicalSyncEnv
            );
        }
        else
        {
            Console.WriteLine($"[E2E] Clinical sync worker missing: {clinicalSyncDll}");
        }

        var schedulingSyncDir = Path.Combine(samplesDir, "Scheduling", "Scheduling.Sync");
        var schedulingSyncDll = Path.Combine(
            schedulingSyncDir,
            "bin",
            configuration,
            "net10.0",
            "Scheduling.Sync.dll"
        );
        if (File.Exists(schedulingSyncDll))
        {
            var schedulingSyncEnv = new Dictionary<string, string>
            {
                ["ConnectionStrings__Postgres"] = schedulingConnStr,
                ["CLINICAL_API_URL"] = ClinicalUrl,
                ["POLL_INTERVAL_SECONDS"] = "5",
            };
            _schedulingSyncProcess = StartSyncWorker(
                schedulingSyncDll,
                schedulingSyncDir,
                schedulingSyncEnv
            );
        }
        else
        {
            Console.WriteLine($"[E2E] Scheduling sync worker missing: {schedulingSyncDll}");
        }

        await Task.Delay(2000);

        _dashboardHost = CreateDashboardHost();
        await _dashboardHost.StartAsync();

        var server = _dashboardHost.Services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        DashboardUrl = addressFeature!.Addresses.First();
        Console.WriteLine($"[E2E] Dashboard started on {DashboardUrl}");

        await SeedTestDataAsync();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true }
        );
    }

    /// <summary>
    /// Stop all services ONCE after all tests.
    /// Order matters: stop sync workers FIRST to prevent connection errors.
    /// In local mode, only Playwright is cleaned up.
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

        if (UseLocalStack)
            return;

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

        StopProcess(_icd10Process);

        await KillProcessOnPortAsync(5080);
        await KillProcessOnPortAsync(5001);
        await KillProcessOnPortAsync(5002);
        await KillProcessOnPortAsync(5090);

        if (_postgresContainer is not null)
            await _postgresContainer.DisposeAsync();
    }

    private static Process StartApiFromDll(
        string dllPath,
        string contentRoot,
        string url,
        Dictionary<string, string>? envVars = null
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\" --urls \"{url}\" --contentRoot \"{contentRoot}\"",
            WorkingDirectory = contentRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // Clear ASPNETCORE_URLS inherited from test process
        // (Microsoft.AspNetCore.Mvc.Testing sets it to http://127.0.0.1:0)
        startInfo.EnvironmentVariables.Remove("ASPNETCORE_URLS");

        if (envVars is not null)
        {
            foreach (var kvp in envVars)
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
        }

        var process = new Process { StartInfo = startInfo };

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

        startInfo.EnvironmentVariables.Remove("ASPNETCORE_URLS");

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

    private static async Task<string> CreateDatabaseAsync(
        string baseConnectionString,
        string dbName
    )
    {
        await using var conn = new NpgsqlConnection(baseConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
        await cmd.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = dbName };
        return builder.ConnectionString;
    }

    private static string ResolveBuildConfiguration(string testAssemblyDir)
    {
        var net9Dir = new DirectoryInfo(testAssemblyDir);
        var configuration = net9Dir.Parent?.Name;
        return string.IsNullOrWhiteSpace(configuration) ? "Debug" : configuration;
    }

    private static async Task WaitForApiAsync(string baseUrl, string healthEndpoint)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var maxRetries = 30; // Reduced from 120 to 30 (15 seconds max instead of 60)
        var lastException = (Exception?)null;
        
        for (var i = 0; i < maxRetries; i++)
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
                {
                    Console.WriteLine($"[E2E] API at {baseUrl} started successfully after {i} attempts");
                    return;
                }
                
                // If we get a non-success status code, log it but continue retrying
                Console.WriteLine($"[E2E] API at {baseUrl} returned {response.StatusCode} on attempt {i + 1}");
            }
            catch (Exception ex)
            {
                lastException = ex;
                Console.WriteLine($"[E2E] API at {baseUrl} connection failed on attempt {i + 1}: {ex.Message}");
                
                // If it's a connection refused error early on, fail faster
                if (ex.Message.Contains("Connection refused") && i >= 5)
                {
                    throw new TimeoutException($"API at {baseUrl} failed to start after {i + 1} attempts: {ex.Message}", ex);
                }
            }
            
            if (i < maxRetries - 1)
            {
                await Task.Delay(500);
            }
        }
        
        throw new TimeoutException($"API at {baseUrl} did not start after {maxRetries} attempts. Last error: {lastException?.Message}");
    }

    private static async Task WaitForGatekeeperApiAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var maxRetries = 30; // Reduced from 120 to 30 (15 seconds max instead of 60)
        var lastException = (Exception?)null;
        
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await client.PostAsync(
                    $"{GatekeeperUrl}/auth/login/begin",
                    new StringContent("{}", Encoding.UTF8, "application/json")
                );
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[E2E] Gatekeeper API started successfully after {i} attempts");
                    return;
                }
                
                Console.WriteLine($"[E2E] Gatekeeper API returned {response.StatusCode} on attempt {i + 1}");
            }
            catch (Exception ex)
            {
                lastException = ex;
                Console.WriteLine($"[E2E] Gatekeeper API connection failed on attempt {i + 1}: {ex.Message}");
                
                // If it's a connection refused error early on, fail faster
                if (ex.Message.Contains("Connection refused") && i >= 5)
                {
                    throw new TimeoutException($"Gatekeeper API failed to start after {i + 1} attempts: {ex.Message}", ex);
                }
            }
            
            if (i < maxRetries - 1)
            {
                await Task.Delay(500);
            }
        }
        
        throw new TimeoutException($"Gatekeeper API did not start after {maxRetries} attempts. Last error: {lastException?.Message}");
    }

    /// <summary>
    /// Waits for a service to be reachable (any HTTP response).
    /// Used in local mode where services may be running but have DB issues.
    /// </summary>
    private static async Task WaitForServiceReachableAsync(string baseUrl, string endpoint)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var maxRetries = 30; // Reduced from 60 to 30 (15 seconds max instead of 30)
        var lastException = (Exception?)null;
        
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                _ = await client.GetAsync($"{baseUrl}{endpoint}");
                Console.WriteLine($"[E2E] Service reachable: {baseUrl} after {i} attempts");
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Console.WriteLine($"[E2E] Service at {baseUrl} connection failed on attempt {i + 1}: {ex.Message}");
                
                // If it's a connection refused error early on, fail faster
                if (ex.Message.Contains("Connection refused") && i >= 5)
                {
                    throw new TimeoutException($"Service at {baseUrl} failed to respond after {i + 1} attempts: {ex.Message}", ex);
                }
            }
            
            if (i < maxRetries - 1)
            {
                await Task.Delay(500);
            }
        }
        
        throw new TimeoutException($"Service at {baseUrl} is not reachable after {maxRetries} attempts. Last error: {lastException?.Message}");
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
    /// Creates a new browser page with authentication already set up via localStorage.
    /// This is the proper E2E approach - no testMode backdoor in the frontend.
    /// </summary>
    /// <param name="navigateTo">Optional URL to navigate to after auth setup. Defaults to DashboardUrl.</param>
    /// <param name="userId">User ID for the test token.</param>
    /// <param name="displayName">Display name for the test token.</param>
    /// <param name="email">Email for the test token.</param>
    public async Task<IPage> CreateAuthenticatedPageAsync(
        string? navigateTo = null,
        string userId = "e2e-test-user",
        string displayName = "E2E Test User",
        string email = "e2etest@example.com"
    )
    {
        var page = await Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER {msg.Type}] {msg.Text}");
        page.PageError += (_, err) => Console.WriteLine($"[PAGE ERROR] {err}");

        var token = GenerateTestToken(userId, displayName, email);
        var userJson = JsonSerializer.Serialize(
            new
            {
                userId,
                displayName,
                email,
            }
        );

        // Inject API URL config BEFORE any page script runs
        await page.AddInitScriptAsync(
            $@"window.dashboardConfig = window.dashboardConfig || {{}};
               window.dashboardConfig.ICD10_API_URL = '{Icd10Url}';"
        );

        // Navigate first to establish the origin for localStorage
        await page.GotoAsync(DashboardUrl);

        // Set auth state in localStorage
        var escapedUserJson = userJson.Replace("'", "\\'");
        await page.EvaluateAsync(
            $@"() => {{
                localStorage.setItem('gatekeeper_token', '{token}');
                localStorage.setItem('gatekeeper_user', '{escapedUserJson}');
            }}"
        );

        // Navigate to target URL (or reload if staying on same page)
        var targetUrl = navigateTo ?? DashboardUrl;

        // Always reload first to ensure static files are fully loaded and auth state is picked up
        await page.ReloadAsync();

        // If target URL has a hash fragment, navigate to it after reload
        if (targetUrl != DashboardUrl && targetUrl.Contains('#'))
        {
            var hash = targetUrl.Substring(targetUrl.IndexOf('#'));
            await page.EvaluateAsync($"() => window.location.hash = '{hash}'");
            // Give React time to process hash change
            await Task.Delay(500);
        }

        return page;
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
        // Microsoft.AspNetCore.Mvc.Testing sets ASPNETCORE_URLS globally to
        // http://127.0.0.1:0 which overrides UseUrls(). Clear it so the
        // Dashboard host binds to the expected port.
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", null);

        var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var fileProvider = new PhysicalFileProvider(wwwrootPath);
        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://127.0.0.1:0");
                webBuilder.Configure(app =>
                {
                    // Both middleware must share the same FileProvider so
                    // UseDefaultFiles can find index.html and rewrite / → /index.html
                    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
                    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
                });
            })
            .Build();
    }

    private static async Task SeedTestDataAsync()
    {
        using var client = CreateAuthenticatedClient();

        await SeedAsync(
            client,
            $"{ClinicalUrl}/fhir/Patient/",
            """{"Active": true, "GivenName": "E2ETest", "FamilyName": "TestPatient", "Gender": "other"}"""
        );

        await SeedAsync(
            client,
            $"{SchedulingUrl}/Practitioner",
            """{"Identifier": "DR001", "Active": true, "NameGiven": "E2EPractitioner", "NameFamily": "DrTest", "Qualification": "MD", "Specialty": "General Practice", "TelecomEmail": "drtest@hospital.org", "TelecomPhone": "+1-555-0123"}"""
        );

        await SeedAsync(
            client,
            $"{SchedulingUrl}/Practitioner",
            """{"Identifier": "DR002", "Active": true, "NameGiven": "Sarah", "NameFamily": "Johnson", "Qualification": "DO", "Specialty": "Cardiology", "TelecomEmail": "sjohnson@hospital.org", "TelecomPhone": "+1-555-0124"}"""
        );

        await SeedAsync(
            client,
            $"{SchedulingUrl}/Practitioner",
            """{"Identifier": "DR003", "Active": true, "NameGiven": "Michael", "NameFamily": "Chen", "Qualification": "MD", "Specialty": "Neurology", "TelecomEmail": "mchen@hospital.org", "TelecomPhone": "+1-555-0125"}"""
        );

        await SeedAsync(
            client,
            $"{SchedulingUrl}/Appointment",
            """{"ServiceCategory": "General", "ServiceType": "Checkup", "Start": "2025-12-20T10:00:00Z", "End": "2025-12-20T11:00:00Z", "PatientReference": "Patient/1", "PractitionerReference": "Practitioner/1", "Priority": "routine"}"""
        );
    }

    private static async Task SeedAsync(HttpClient client, string url, string json)
    {
        try
        {
            var response = await client.PostAsync(
                url,
                new StringContent(json, Encoding.UTF8, "application/json")
            );
            Console.WriteLine(
                $"[E2E] Seed {url}: {(int)response.StatusCode} {response.ReasonPhrase}"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[E2E] Seed {url} failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Single collection definition for ALL E2E tests.
/// All tests share ONE E2EFixture instance to prevent port conflicts.
/// Tests within this collection run sequentially by default.
/// </summary>
[CollectionDefinition("E2E Tests")]
public sealed class E2ECollection : ICollectionFixture<E2EFixture>;
