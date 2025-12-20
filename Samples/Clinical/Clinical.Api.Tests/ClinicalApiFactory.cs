namespace Clinical.Api.Tests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Sync.SQLite;

/// <summary>
/// WebApplicationFactory for Clinical.Api e2e testing with isolated in-memory SQLite database.
/// </summary>
public sealed class ClinicalApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    /// <summary>
    /// Creates a new instance of ClinicalApiFactory with an isolated in-memory database.
    /// </summary>
    public ClinicalApiFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        InitializeDatabase();
    }

    /// <summary>
    /// Gets a new connection to the test database.
    /// </summary>
    public SqliteConnection GetConnection()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        return conn;
    }

    /// <summary>
    /// Gets the shared connection for direct database access in tests.
    /// </summary>
    public SqliteConnection SharedConnection => _connection;

    private void InitializeDatabase()
    {
        _ = SyncSchema.CreateSchema(_connection);
        _ = SyncSchema.SetOriginId(_connection, Guid.NewGuid().ToString());

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema.sql");
        if (File.Exists(schemaPath))
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = File.ReadAllText(schemaPath);
            cmd.ExecuteNonQuery();
        }

        _ = TriggerGenerator.CreateTriggers(_connection, "fhir_Patient", null);
        _ = TriggerGenerator.CreateTriggers(_connection, "fhir_Encounter", null);
        _ = TriggerGenerator.CreateTriggers(_connection, "fhir_Condition", null);
        _ = TriggerGenerator.CreateTriggers(_connection, "fhir_MedicationRequest", null);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(Func<SqliteConnection>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            var sharedConnection = _connection;
            services.AddSingleton<Func<SqliteConnection>>(() =>
            {
                return sharedConnection;
            });
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }

        base.Dispose(disposing);
    }
}
