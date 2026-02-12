using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Migration;
using Migration.Postgres;
using Npgsql;

namespace ICD10.Api.Tests;

/// <summary>
/// WebApplicationFactory for ICD10.Api e2e testing.
/// Creates an isolated PostgreSQL test database per factory instance,
/// creates schema from YAML, and seeds reference data.
/// </summary>
public sealed class ICD10ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName;
    private readonly string _connectionString;

    private static readonly string BaseConnectionString =
        Environment.GetEnvironmentVariable("ICD10_TEST_CONNECTION_STRING")
        ?? "Host=localhost;Database=postgres;Username=postgres;Password=changeme";

    /// <summary>
    /// Creates a new instance with an isolated PostgreSQL test database,
    /// schema from YAML migration, and seeded reference data.
    /// </summary>
    public ICD10ApiFactory()
    {
        _dbName = $"test_icd10_{Guid.NewGuid():N}";

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

        // Create schema and seed data BEFORE the app starts.
        // When app starts, DatabaseSetup.Initialize detects tables exist and skips.
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        // Enable pgvector extension
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector";
            cmd.ExecuteNonQuery();
        }

        // Create schema from YAML using Migration library
        var apiDir = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
        var yamlPath = Path.Combine(apiDir, "icd10-schema.yaml");
        var schema = SchemaYamlSerializer.FromYamlFile(yamlPath);
        PostgresDdlGenerator.MigrateSchema(conn, schema);

        // Seed reference data
        TestDataSeeder.Seed(conn);
    }

    /// <summary>
    /// Gets the connection string for direct access in tests if needed.
    /// </summary>
    public string ConnectionString => _connectionString;

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
        builder.UseSetting("ConnectionStrings:Postgres", _connectionString);
        builder.UseSetting("EmbeddingService:BaseUrl", "http://localhost:8000");
        builder.UseEnvironment("Development");

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
