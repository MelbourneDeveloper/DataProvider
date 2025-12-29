
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clinical.Sync;
internal static class Program
{
    internal static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Support environment variable override for testing
        // Default path navigates from bin/Debug/net9.0 up to Clinical.Api/bin/Debug/net9.0
        var clinicalDbPath =
            Environment.GetEnvironmentVariable("CLINICAL_DB_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Clinical.Api", "bin", "Debug", "net9.0", "clinical.db");
        var schedulingApiUrl =
            Environment.GetEnvironmentVariable("SCHEDULING_API_URL") ?? "http://localhost:5001";

        Console.WriteLine($"[Clinical.Sync] Using database: {clinicalDbPath}");
        Console.WriteLine($"[Clinical.Sync] Scheduling API URL: {schedulingApiUrl}");

        builder.Services.AddSingleton<Func<SqliteConnection>>(_ =>
            () =>
            {
                var conn = new SqliteConnection($"Data Source={clinicalDbPath}");
                conn.Open();
                return conn;
            }
        );

        builder.Services.AddHostedService(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SyncWorker>>();
            var getConn = sp.GetRequiredService<Func<SqliteConnection>>();
            return new SyncWorker(logger, getConn, schedulingApiUrl);
        });

        var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Sync change record from remote API.
/// Matches the SyncLogEntry schema returned by /sync/changes endpoint.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Used for JSON deserialization"
)]
internal sealed record SyncChange(
    long Version,
    string TableName,
    string PkValue,
    int Operation,
    string? Payload,
    string Origin,
    string Timestamp
)
{
    /// <summary>Insert operation (0).</summary>
    public const int Insert = 0;

    /// <summary>Update operation (1).</summary>
    public const int Update = 1;

    /// <summary>Delete operation (2).</summary>
    public const int Delete = 2;
}
