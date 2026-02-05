using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ICD10.Api.Tests;

/// <summary>
/// WebApplicationFactory for ICD10.Api e2e testing.
/// Uses PostgreSQL container with real ICD-10 codes.
/// </summary>
public sealed class ICD10ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// PostgreSQL connection string for Docker container.
    /// </summary>
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=icd10;Username=icd10;Password=changeme";

    /// <summary>
    /// Checks if the embedding service at localhost:8000 is available.
    /// </summary>
    public bool EmbeddingServiceAvailable
    {
        get
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var response = client.GetAsync("http://localhost:8000/health").Result;
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use environment variable or default to Docker container
        var connectionString =
            Environment.GetEnvironmentVariable("ICD10_TEST_CONNECTION_STRING")
            ?? DefaultConnectionString;

        builder.ConfigureAppConfiguration(
            (context, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] = connectionString,
                        ["EmbeddingService:BaseUrl"] = "http://localhost:8000",
                    }
                );
            }
        );

        var apiAssembly = typeof(Program).Assembly;
        var contentRoot = Path.GetDirectoryName(apiAssembly.Location)!;
        builder.UseContentRoot(contentRoot);
    }
}
