using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ICD10.Api.Tests;

/// <summary>
/// WebApplicationFactory for ICD10.Api e2e testing.
/// Uses production database with real ICD-10 codes.
/// </summary>
public sealed class ICD10ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;

    /// <summary>
    /// Creates a new instance using the production database.
    /// </summary>
    public ICD10ApiFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"icd10_test_{Guid.NewGuid()}.db");

        var sourceDb = Path.Combine(
            Path.GetDirectoryName(typeof(Program).Assembly.Location)!,
            "icd10.db"
        );

        if (!File.Exists(sourceDb))
        {
            throw new FileNotFoundException(
                $"Schema database not found at {sourceDb}. Build the API project first."
            );
        }

        File.Copy(sourceDb, _dbPath);
    }

    /// <summary>
    /// Gets the database path for direct access in tests if needed.
    /// </summary>
    public string DbPath => _dbPath;

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
        builder.UseSetting("DbPath", _dbPath);

        var apiAssembly = typeof(Program).Assembly;
        var contentRoot = Path.GetDirectoryName(apiAssembly.Location)!;
        builder.UseContentRoot(contentRoot);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
