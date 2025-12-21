namespace Clinical.Sync;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal static class Program
{
    internal static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var clinicalDbPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "Clinical.Api",
            "clinical.db"
        );
        var schedulingApiUrl = "http://localhost:5001";

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
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Used for JSON deserialization"
)]
internal sealed record SyncChange(
    long Version,
    string TableName,
    string RowId,
    string Operation,
    string? Data,
    string Timestamp
);
