using Scheduling.Sync;

var builder = Host.CreateApplicationBuilder(args);

// Configure sync service
builder.Services.AddHostedService<SchedulingSyncWorker>();

builder.Services.AddSingleton<Func<NpgsqlConnection>>(() =>
{
    var connectionString =
        Environment.GetEnvironmentVariable("SCHEDULING_DB")
        ?? "Host=localhost;Database=scheduling;Username=clinic;Password=clinic123";
    var conn = new NpgsqlConnection(connectionString);
    conn.Open();
    return conn;
});

var host = builder.Build();
await host.RunAsync();
