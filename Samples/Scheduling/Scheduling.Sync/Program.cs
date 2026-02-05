using Microsoft.Extensions.Configuration;
using Scheduling.Sync;

var builder = Host.CreateApplicationBuilder(args);

var connectionString =
    Environment.GetEnvironmentVariable("SCHEDULING_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("PostgreSQL connection string required");
var clinicalApiUrl =
    Environment.GetEnvironmentVariable("CLINICAL_API_URL") ?? "http://localhost:5080";

Console.WriteLine($"[Scheduling.Sync] Clinical API URL: {clinicalApiUrl}");

builder.Services.AddSingleton<Func<NpgsqlConnection>>(_ =>
    () =>
    {
        var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        return conn;
    }
);

builder.Services.AddHostedService(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SchedulingSyncWorker>>();
    var getConn = sp.GetRequiredService<Func<NpgsqlConnection>>();
    return new SchedulingSyncWorker(logger, getConn, clinicalApiUrl);
});

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
