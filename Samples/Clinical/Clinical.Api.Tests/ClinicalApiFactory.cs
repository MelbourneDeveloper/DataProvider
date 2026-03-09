using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Clinical.Api.Tests;

/// <summary>
/// WebApplicationFactory for Clinical.Api e2e testing.
/// Creates an isolated PostgreSQL test database per factory instance.
/// </summary>
public sealed class ClinicalApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName;
    private readonly string _connectionString;

    private static readonly string BaseConnectionString =
        Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")
        ?? "Host=localhost;Database=postgres;Username=postgres;Password=changeme";

    /// <summary>
    /// Creates a new instance with an isolated PostgreSQL test database.
    /// </summary>
    public ClinicalApiFactory()
    {
        _dbName = $"test_clinical_{Guid.NewGuid():N}";

        using (var adminConn = new NpgsqlConnection(BaseConnectionString))
        {
            adminConn.Open();
            using var createCmd = adminConn.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE {_dbName}";
            createCmd.ExecuteNonQuery();
        }

        _connectionString = BaseConnectionString.Replace(
            "Database=postgres",
            $"Database={_dbName}"
        );
    }

    /// <summary>
    /// Gets the connection string for direct access in tests if needed.
    /// </summary>
    public string ConnectionString => _connectionString;

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", _connectionString);
        builder.UseEnvironment("Development");

        var clinicalApiAssembly = typeof(Program).Assembly;
        var contentRoot = Path.GetDirectoryName(clinicalApiAssembly.Location)!;
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
                using var adminConn = new NpgsqlConnection(BaseConnectionString);
                adminConn.Open();

                using var terminateCmd = adminConn.CreateCommand();
                terminateCmd.CommandText =
                    $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_dbName}'";
                terminateCmd.ExecuteNonQuery();

                using var dropCmd = adminConn.CreateCommand();
                dropCmd.CommandText = $"DROP DATABASE IF EXISTS {_dbName}";
                dropCmd.ExecuteNonQuery();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
