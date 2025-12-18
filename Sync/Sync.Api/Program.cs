using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Npgsql;
using Sync;
using Sync.Api;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<ApiSubscriptionManager>();
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Debug);
});

var app = builder.Build();

// Health check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

// Sync endpoints - Pull changes from server
app.MapGet(
    "/sync/changes",
    (
        long fromVersion,
        int batchSize,
        string connectionString,
        string dbType,
        ILogger<Program> logger
    ) =>
    {
        logger.LogInformation(
            "API: Pull changes from version {Version}, batchSize {Size}, dbType {Type}",
            fromVersion,
            batchSize,
            dbType
        );

        try
        {
            var entries = dbType.ToLowerInvariant() switch
            {
                "sqlite" => FetchChangesFromSqlite(
                    connectionString,
                    fromVersion,
                    batchSize,
                    logger
                ),
                "postgres" => FetchChangesFromPostgres(
                    connectionString,
                    fromVersion,
                    batchSize,
                    logger
                ),
                _ => throw new ArgumentException($"Unknown database type: {dbType}"),
            };

            return Results.Ok(
                new
                {
                    Changes = entries,
                    FromVersion = fromVersion,
                    ToVersion = entries.Count > 0 ? entries.Max(e => e.Version) : fromVersion,
                    HasMore = entries.Count == batchSize,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "API: Failed to fetch changes");
            return Results.Problem(ex.Message);
        }
    }
);

// Push changes to server
app.MapPost(
    "/sync/changes",
    async (
        HttpRequest request,
        string connectionString,
        string dbType,
        ILogger<Program> logger,
        ApiSubscriptionManager subscriptions
    ) =>
    {
        logger.LogInformation("API: Push changes, dbType {Type}", dbType);

        try
        {
            var body = await JsonSerializer.DeserializeAsync<PushChangesRequest>(request.Body);
            if (body?.Changes is null)
            {
                return Results.BadRequest("Changes array required");
            }

            var applied = dbType.ToLowerInvariant() switch
            {
                "sqlite" => ApplyChangesToSqlite(
                    connectionString,
                    body.Changes,
                    body.OriginId,
                    logger
                ),
                "postgres" => ApplyChangesToPostgres(
                    connectionString,
                    body.Changes,
                    body.OriginId,
                    logger
                ),
                _ => throw new ArgumentException($"Unknown database type: {dbType}"),
            };

            // Notify subscriptions
            foreach (var entry in body.Changes)
            {
                subscriptions.NotifyChange(entry);
            }

            return Results.Ok(new { Applied = applied, Timestamp = DateTime.UtcNow.ToString("O") });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "API: Failed to push changes");
            return Results.Problem(ex.Message);
        }
    }
);

// Register client
app.MapPost(
    "/sync/clients",
    async (HttpRequest request, string connectionString, string dbType, ILogger<Program> logger) =>
    {
        logger.LogInformation("API: Register client, dbType {Type}", dbType);

        try
        {
            var body = await JsonSerializer.DeserializeAsync<RegisterClientRequest>(request.Body);
            if (body?.OriginId is null)
            {
                return Results.BadRequest("OriginId required");
            }

            var client = new SyncClient(
                body.OriginId,
                body.LastSyncVersion,
                DateTime.UtcNow.ToString("O"),
                DateTime.UtcNow.ToString("O")
            );

            _ = dbType.ToLowerInvariant() switch
            {
                "sqlite" => UpsertClientSqlite(connectionString, client, logger),
                "postgres" => UpsertClientPostgres(connectionString, client, logger),
                _ => throw new ArgumentException($"Unknown database type: {dbType}"),
            };

            return Results.Ok(new { Registered = true, Client = client });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "API: Failed to register client");
            return Results.Problem(ex.Message);
        }
    }
);

// Get server state (max version)
app.MapGet(
    "/sync/state",
    (string connectionString, string dbType, ILogger<Program> logger) =>
    {
        logger.LogInformation("API: Get sync state, dbType {Type}", dbType);

        try
        {
            var maxVersion = dbType.ToLowerInvariant() switch
            {
                "sqlite" => GetMaxVersionSqlite(connectionString, logger),
                "postgres" => GetMaxVersionPostgres(connectionString, logger),
                _ => throw new ArgumentException($"Unknown database type: {dbType}"),
            };

            return Results.Ok(
                new { MaxVersion = maxVersion, Timestamp = DateTime.UtcNow.ToString("O") }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "API: Failed to get sync state");
            return Results.Problem(ex.Message);
        }
    }
);

// Real-time subscriptions via Server-Sent Events (SSE)
app.MapGet(
    "/sync/subscribe",
    async (
        HttpContext context,
        string tableName,
        string? pkValue,
        ApiSubscriptionManager subscriptions,
        ILogger<Program> logger,
        CancellationToken ct
    ) =>
    {
        logger.LogInformation("API: SSE subscribe to table {Table}, pk {Pk}", tableName, pkValue);

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");

        var subscriptionId = Guid.NewGuid().ToString();
        var channel = subscriptions.Subscribe(subscriptionId, tableName, pkValue);

        try
        {
            // Send initial connection event
            await context.Response.WriteAsync(
                $"event: connected\ndata: {{\"subscriptionId\":\"{subscriptionId}\"}}\n\n",
                ct
            );
            await context.Response.Body.FlushAsync(ct);

            // Stream changes as they arrive
            await foreach (var change in channel.Reader.ReadAllAsync(ct))
            {
                var json = JsonSerializer.Serialize(change);
                await context.Response.WriteAsync($"event: change\ndata: {json}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("API: SSE subscription {Id} disconnected", subscriptionId);
        }
        finally
        {
            subscriptions.Unsubscribe(subscriptionId);
        }
    }
);

// Unsubscribe endpoint
app.MapDelete(
    "/sync/subscribe/{subscriptionId}",
    (
        string subscriptionId,
        ApiSubscriptionManager subscriptions,
        ILogger<Program> logger
    ) =>
    {
        logger.LogInformation("API: Unsubscribe {Id}", subscriptionId);
        subscriptions.Unsubscribe(subscriptionId);
        return Results.Ok(new { Unsubscribed = true });
    }
);

app.Run();

// Helper methods
static List<SyncLogEntry> FetchChangesFromSqlite(
    string connectionString,
    long fromVersion,
    int batchSize,
    ILogger logger
)
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var result = Sync.SQLite.SyncLogRepository.FetchChanges(conn, fromVersion, batchSize);
    return result is SyncLogListOk ok ? [.. ok.Value] : [];
}

static List<SyncLogEntry> FetchChangesFromPostgres(
    string connectionString,
    long fromVersion,
    int batchSize,
    ILogger logger
)
{
    using var conn = new NpgsqlConnection(connectionString);
    conn.Open();
    var result = Sync.Postgres.PostgresSyncLogRepository.FetchChanges(conn, fromVersion, batchSize);
    return result is SyncLogListOk ok ? [.. ok.Value] : [];
}

static int ApplyChangesToSqlite(
    string connectionString,
    List<SyncLogEntryDto> changes,
    string originId,
    ILogger logger
)
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();

    // Enable suppression
    Sync.SQLite.SyncSessionManager.EnableSuppression(conn);

    try
    {
        var applied = 0;
        foreach (var change in changes)
        {
            // Skip changes from self (echo prevention)
            if (change.Origin == originId)
                continue;

            var entry = new SyncLogEntry(
                change.Version,
                change.TableName,
                change.PkValue,
                Enum.Parse<SyncOperation>(change.Operation, true),
                change.Payload,
                change.Origin,
                change.Timestamp
            );

            var result = Sync.SQLite.ChangeApplierSQLite.ApplyChange(conn, entry);
            if (result is BoolSyncOk)
                applied++;
        }
        return applied;
    }
    finally
    {
        Sync.SQLite.SyncSessionManager.DisableSuppression(conn);
    }
}

static int ApplyChangesToPostgres(
    string connectionString,
    List<SyncLogEntryDto> changes,
    string originId,
    ILogger logger
)
{
    using var conn = new NpgsqlConnection(connectionString);
    conn.Open();

    // Enable suppression
    Sync.Postgres.PostgresSyncSession.EnableSuppression(conn);

    try
    {
        var applied = 0;
        foreach (var change in changes)
        {
            // Skip changes from self (echo prevention)
            if (change.Origin == originId)
                continue;

            var entry = new SyncLogEntry(
                change.Version,
                change.TableName,
                change.PkValue,
                Enum.Parse<SyncOperation>(change.Operation, true),
                change.Payload,
                change.Origin,
                change.Timestamp
            );

            var result = Sync.Postgres.PostgresChangeApplier.ApplyChange(conn, entry, logger);
            if (result is BoolSyncOk)
                applied++;
        }
        return applied;
    }
    finally
    {
        Sync.Postgres.PostgresSyncSession.DisableSuppression(conn);
    }
}

static bool UpsertClientSqlite(string connectionString, SyncClient client, ILogger logger)
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var result = Sync.SQLite.SyncClientRepository.Upsert(conn, client);
    return result is BoolSyncOk;
}

static bool UpsertClientPostgres(string connectionString, SyncClient client, ILogger logger)
{
    using var conn = new NpgsqlConnection(connectionString);
    conn.Open();
    var result = Sync.Postgres.PostgresSyncClientRepository.Upsert(conn, client);
    return result is BoolSyncOk;
}

static long GetMaxVersionSqlite(string connectionString, ILogger logger)
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var result = Sync.SQLite.SyncLogRepository.GetMaxVersion(conn);
    return result is LongSyncOk ok ? ok.Value : 0;
}

static long GetMaxVersionPostgres(string connectionString, ILogger logger)
{
    using var conn = new NpgsqlConnection(connectionString);
    conn.Open();
    var result = Sync.Postgres.PostgresSyncLogRepository.GetMaxVersion(conn);
    return result is LongSyncOk ok ? ok.Value : 0;
}

/// <summary>
/// Main program entry point for Sync API.
/// </summary>
public partial class Program { }

namespace Sync.Api
{
    /// <summary>
    /// Request to push changes.
    /// </summary>
    public sealed record PushChangesRequest(string OriginId, List<SyncLogEntryDto> Changes);

    /// <summary>
    /// Request to register a client.
    /// </summary>
    public sealed record RegisterClientRequest(string OriginId, long LastSyncVersion);

    /// <summary>
    /// DTO for sync log entry (JSON serialization).
    /// </summary>
    public sealed record SyncLogEntryDto(
        long Version,
        string TableName,
        string PkValue,
        string Operation,
        string? Payload,
        string Origin,
        string Timestamp
    );

    /// <summary>
    /// Manages real-time subscriptions for SSE.
    /// Implements spec Section 10 (Real-Time Subscriptions).
    /// </summary>
    public sealed class ApiSubscriptionManager
    {
        private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();
        private readonly ILogger<ApiSubscriptionManager> _logger;

        /// <summary>
        /// Creates a new subscription manager.
        /// </summary>
        public ApiSubscriptionManager(ILogger<ApiSubscriptionManager> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Subscribe to changes for a table (and optionally specific record).
        /// </summary>
        public System.Threading.Channels.Channel<SyncLogEntry> Subscribe(
            string subscriptionId,
            string tableName,
            string? pkValue
        )
        {
            _logger.LogInformation(
                "SUBS: Creating subscription {Id} for {Table}/{Pk}",
                subscriptionId,
                tableName,
                pkValue
            );

            var channel = System.Threading.Channels.Channel.CreateUnbounded<SyncLogEntry>();
            var sub = new Subscription(subscriptionId, tableName, pkValue, channel);
            _subscriptions[subscriptionId] = sub;

            return channel;
        }

        /// <summary>
        /// Unsubscribe from changes.
        /// </summary>
        public void Unsubscribe(string subscriptionId)
        {
            if (_subscriptions.TryRemove(subscriptionId, out var sub))
            {
                _logger.LogInformation("SUBS: Removed subscription {Id}", subscriptionId);
                sub.Channel.Writer.Complete();
            }
        }

        /// <summary>
        /// Notify all matching subscriptions of a change.
        /// </summary>
        public void NotifyChange(SyncLogEntryDto entry)
        {
            var syncEntry = new SyncLogEntry(
                entry.Version,
                entry.TableName,
                entry.PkValue,
                Enum.Parse<SyncOperation>(entry.Operation, true),
                entry.Payload,
                entry.Origin,
                entry.Timestamp
            );

            foreach (var sub in _subscriptions.Values)
            {
                if (sub.Matches(syncEntry))
                {
                    _logger.LogDebug(
                        "SUBS: Notifying {Id} of change to {Table}/{Pk}",
                        sub.Id,
                        entry.TableName,
                        entry.PkValue
                    );
                    _ = sub.Channel.Writer.TryWrite(syncEntry);
                }
            }
        }

        private sealed record Subscription(
            string Id,
            string TableName,
            string? PkValue,
            System.Threading.Channels.Channel<SyncLogEntry> Channel
        )
        {
            public bool Matches(SyncLogEntry entry)
            {
                if (!string.Equals(TableName, entry.TableName, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (PkValue is null)
                    return true; // Table-level subscription

                // Record-level subscription - check if PK matches
                return entry.PkValue.Contains(PkValue, StringComparison.Ordinal);
            }
        }
    }
}
