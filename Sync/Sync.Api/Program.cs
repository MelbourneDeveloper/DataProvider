using System.Text.Json;
using Microsoft.Data.Sqlite;
using Npgsql;
using Sync;
using Sync.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add real-time subscription service
builder.Services.AddSingleton<SubscriptionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var subscriptionService = app.Services.GetRequiredService<SubscriptionService>();

// GET /sync/changes - Fetch changes from a version
app.MapGet("/sync/changes", (long fromVersion, int? batchSize, string? connectionString, string? dbType) =>
{
    var size = batchSize ?? 100;
    var connStr = connectionString ?? "Data Source=:memory:";
    var type = dbType?.ToLowerInvariant() ?? "sqlite";

    if (type == "postgres")
    {
        using var conn = new NpgsqlConnection(connStr);
        conn.Open();
        var result = Sync.Postgres.SyncLogRepository.FetchChanges(conn, fromVersion, size);
        return result switch
        {
            SyncLogListOk ok => Results.Ok(new { changes = ok.Value, fromVersion, hasMore = ok.Value.Count == size }),
            SyncLogListError err => Results.BadRequest(new { error = err.Value.ToString() }),
            _ => Results.BadRequest(new { error = "Unknown error" })
        };
    }
    else
    {
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        var result = Sync.SQLite.SyncLogRepository.FetchChanges(conn, fromVersion, size);
        return result switch
        {
            SyncLogListOk ok => Results.Ok(new { changes = ok.Value, fromVersion, hasMore = ok.Value.Count == size }),
            SyncLogListError err => Results.BadRequest(new { error = err.Value.ToString() }),
            _ => Results.BadRequest(new { error = "Unknown error" })
        };
    }
});

// POST /sync/apply - Apply a batch of changes
app.MapPost("/sync/apply", async (HttpContext context, string? connectionString, string? dbType) =>
{
    var connStr = connectionString ?? "Data Source=:memory:";
    var type = dbType?.ToLowerInvariant() ?? "sqlite";
    
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    var entries = JsonSerializer.Deserialize<List<SyncLogEntry>>(body, new JsonSerializerOptions 
    { 
        PropertyNameCaseInsensitive = true 
    });

    if (entries == null)
    {
        return Results.BadRequest(new { error = "Invalid request body" });
    }

    var applied = 0;
    var failed = new List<string>();

    if (type == "postgres")
    {
        using var conn = new NpgsqlConnection(connStr);
        conn.Open();
        
        // Enable trigger suppression
        Sync.Postgres.SyncSessionManager.EnableSuppression(conn);
        
        try
        {
            foreach (var entry in entries)
            {
                var result = Sync.Postgres.ChangeApplierPostgres.ApplyChange(conn, entry);
                if (result is BoolSyncOk { Value: true })
                {
                    applied++;
                }
                else
                {
                    failed.Add($"Version {entry.Version}: {entry.TableName}");
                }
            }
        }
        finally
        {
            Sync.Postgres.SyncSessionManager.DisableSuppression(conn);
        }
    }
    else
    {
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        
        // Enable trigger suppression
        Sync.SQLite.SyncSessionManager.EnableSuppression(conn);
        
        try
        {
            foreach (var entry in entries)
            {
                var result = Sync.SQLite.ChangeApplierSQLite.ApplyChange(conn, entry);
                if (result is BoolSyncOk { Value: true })
                {
                    applied++;
                }
                else
                {
                    failed.Add($"Version {entry.Version}: {entry.TableName}");
                }
            }
        }
        finally
        {
            Sync.SQLite.SyncSessionManager.DisableSuppression(conn);
        }
    }

    // Notify subscribers of changes
    foreach (var entry in entries)
    {
        await subscriptionService.NotifyChange(entry);
    }

    return Results.Ok(new { applied, failed });
});

// GET /sync/version - Get current max version
app.MapGet("/sync/version", (string? connectionString, string? dbType) =>
{
    var connStr = connectionString ?? "Data Source=:memory:";
    var type = dbType?.ToLowerInvariant() ?? "sqlite";

    if (type == "postgres")
    {
        using var conn = new NpgsqlConnection(connStr);
        conn.Open();
        var result = Sync.Postgres.SyncLogRepository.GetMaxVersion(conn);
        return result switch
        {
            LongSyncOk ok => Results.Ok(new { version = ok.Value }),
            LongSyncError err => Results.BadRequest(new { error = err.Value.ToString() }),
            _ => Results.BadRequest(new { error = "Unknown error" })
        };
    }
    else
    {
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        var result = Sync.SQLite.SyncLogRepository.GetMaxVersion(conn);
        return result switch
        {
            LongSyncOk ok => Results.Ok(new { version = ok.Value }),
            LongSyncError err => Results.BadRequest(new { error = err.Value.ToString() }),
            _ => Results.BadRequest(new { error = "Unknown error" })
        };
    }
});

// POST /sync/subscribe - Subscribe to real-time changes (SSE)
app.MapGet("/sync/subscribe", async (HttpContext context, string? tableName, string? recordId) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    var subscriptionId = Guid.NewGuid().ToString();
    var channel = subscriptionService.Subscribe(subscriptionId, tableName, recordId);

    try
    {
        await foreach (var change in channel.ReadAllAsync(context.RequestAborted))
        {
            var json = JsonSerializer.Serialize(change);
            await context.Response.WriteAsync($"data: {json}\n\n");
            await context.Response.Body.FlushAsync();
        }
    }
    catch (OperationCanceledException)
    {
        // Client disconnected
    }
    finally
    {
        subscriptionService.Unsubscribe(subscriptionId);
    }
});

// GET /sync/clients - Get all sync clients
app.MapGet("/sync/clients", (string? connectionString, string? dbType) =>
{
    var connStr = connectionString ?? "Data Source=:memory:";
    var type = dbType?.ToLowerInvariant() ?? "sqlite";

    if (type == "postgres")
    {
        using var conn = new NpgsqlConnection(connStr);
        conn.Open();
        var result = Sync.Postgres.SyncClientRepository.GetAllClients(conn);
        return result switch
        {
            SyncClientListOk ok => Results.Ok(ok.Value),
            SyncClientListError err => Results.BadRequest(new { error = err.Value.ToString() }),
            _ => Results.BadRequest(new { error = "Unknown error" })
        };
    }
    else
    {
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        var result = Sync.SQLite.SyncClientRepository.GetAllClients(conn);
        return result switch
        {
            SyncClientListOk ok => Results.Ok(ok.Value),
            SyncClientListError err => Results.BadRequest(new { error = err.Value.ToString() }),
            _ => Results.BadRequest(new { error = "Unknown error" })
        };
    }
});

// POST /sync/clients - Register/update a sync client
app.MapPost("/sync/clients", async (HttpContext context, string? connectionString, string? dbType) =>
{
    var connStr = connectionString ?? "Data Source=:memory:";
    var type = dbType?.ToLowerInvariant() ?? "sqlite";
    
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    var client = JsonSerializer.Deserialize<SyncClient>(body, new JsonSerializerOptions 
    { 
        PropertyNameCaseInsensitive = true 
    });

    if (client == null)
    {
        return Results.BadRequest(new { error = "Invalid client data" });
    }

    if (type == "postgres")
    {
        using var conn = new NpgsqlConnection(connStr);
        conn.Open();
        var result = Sync.Postgres.SyncClientRepository.UpsertClient(conn, client);
        return result switch
        {
            BoolSyncOk => Results.Ok(new { success = true }),
            BoolSyncError err => Results.BadRequest(new { error = err.Value.ToString() }),
            _ => Results.BadRequest(new { error = "Unknown error" })
        };
    }
    else
    {
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        var result = Sync.SQLite.SyncClientRepository.UpsertClient(conn, client);
        return result switch
        {
            BoolSyncOk => Results.Ok(new { success = true }),
            BoolSyncError err => Results.BadRequest(new { error = err.Value.ToString() }),
            _ => Results.BadRequest(new { error = "Unknown error" })
        };
    }
});

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Make Program accessible for testing
public partial class Program { }
