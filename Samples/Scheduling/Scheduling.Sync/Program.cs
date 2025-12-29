using Scheduling.Sync;

var builder = Host.CreateApplicationBuilder(args);

// Support environment variable override for testing
// Default path navigates from bin/Debug/net9.0 up to Scheduling.Api/bin/Debug/net9.0
var schedulingDbPath =
    Environment.GetEnvironmentVariable("SCHEDULING_DB_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Scheduling.Api", "bin", "Debug", "net9.0", "scheduling.db");
var clinicalApiUrl =
    Environment.GetEnvironmentVariable("CLINICAL_API_URL") ?? "http://localhost:5080";

Console.WriteLine($"[Scheduling.Sync] Using database: {schedulingDbPath}");
Console.WriteLine($"[Scheduling.Sync] Clinical API URL: {clinicalApiUrl}");

// Configure sync service with SQLite (same as Scheduling.Api)
builder.Services.AddSingleton<Func<SqliteConnection>>(_ =>
    () =>
    {
        var conn = new SqliteConnection($"Data Source={schedulingDbPath}");
        conn.Open();
        return conn;
    }
);

builder.Services.AddHostedService(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SchedulingSyncWorker>>();
    var getConn = sp.GetRequiredService<Func<SqliteConnection>>();
    return new SchedulingSyncWorker(logger, getConn, clinicalApiUrl);
});

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
