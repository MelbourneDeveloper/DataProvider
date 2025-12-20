namespace Clinical.Api.Tests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// WebApplicationFactory for Clinical.Api e2e testing.
/// Just configures a temp database path - Program.cs does ALL initialization.
/// </summary>
public sealed class ClinicalApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;

    /// <summary>
    /// Creates a new instance with an isolated temp database.
    /// </summary>
    public ClinicalApiFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"clinical_test_{Guid.NewGuid()}.db");
    }

    /// <summary>
    /// Gets the database path for direct access in tests if needed.
    /// </summary>
    public string DbPath => _dbPath;

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        // Just set configuration - Program.cs handles everything else
        builder.UseSetting("DbPath", _dbPath);

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
