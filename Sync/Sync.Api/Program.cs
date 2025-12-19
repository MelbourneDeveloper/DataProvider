#pragma warning disable CA1848 // Use the LoggerMessage delegates for performance
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Data.Sqlite;
using Npgsql;
using Sync;
using Sync.Api;

var builder = WebApplication.CreateBuilder(args);

// Load configuration from appsettings.json or environment variables
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<ApiSubscriptionManager>();

// Production logging - structured, log level from config
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.SetMinimumLevel(
        builder.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information
    );
});

// Rate limiting for production - prevents DOS attacks
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global rate limit: 1000 requests/minute per client
    options.AddPolicy(
        "sync",
        context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: context.Request.Headers["X-Client-Id"].FirstOrDefault()
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 1000,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10,
                }
            )
    );

    // SSE connections - limited to 100 concurrent per client
    options.AddPolicy(
        "sse",
        context =>
            RateLimitPartition.GetConcurrencyLimiter(
                partitionKey: context.Request.Headers["X-Client-Id"].FirstOrDefault()
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous",
                factory: _ => new ConcurrencyLimiterOptions { PermitLimit = 100, QueueLimit = 10 }
            )
    );
});

// Configure Npgsql connection pooling for PostgreSQL
NpgsqlDataSource? pgDataSource = null;
var pgConnString = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrEmpty(pgConnString))
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(pgConnString);
    pgDataSource = dataSourceBuilder.Build();
    builder.Services.AddSingleton(pgDataSource);
}

var app = builder.Build();

// Use rate limiting middleware
app.UseRateLimiter();

// Request timeout middleware for production
app.Use(
    async (context, next) =>
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        context.RequestAborted = cts.Token;
        await next();
    }
);

// Health check - no rate limiting
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
    .AllowAnonymous();

// Sync endpoints - Pull changes from server
app.MapGet(
        "/sync/changes",
        (
            long fromVersion,
            int batchSize,
            string? connectionString,
            string dbType,
            ILogger<Program> logger,
            IConfiguration config
        ) =>
        {
            // Use configured connection string, fall back to query param for backwards compat
            var connStr =
                connectionString ?? config.GetConnectionString(dbType.ToUpperInvariant()) ?? "";

            if (string.IsNullOrEmpty(connStr))
            {
                logger.LogWarning("API: No connection string for {DbType}", dbType);
                return Results.BadRequest($"No connection string configured for {dbType}");
            }

            // Cap batch size for production
            batchSize = Math.Min(batchSize, 5000);

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
                    "sqlite" => FetchChangesFromSqlite(connStr, fromVersion, batchSize, logger),
                    "postgres" => FetchChangesFromPostgres(connStr, fromVersion, batchSize, logger),
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
    )
    .RequireRateLimiting("sync");

// Push changes to server
app.MapPost(
        "/sync/changes",
        async (
            HttpRequest request,
            string? connectionString,
            string dbType,
            ILogger<Program> logger,
            IConfiguration config,
            ApiSubscriptionManager subscriptions
        ) =>
        {
            var connStr =
                connectionString ?? config.GetConnectionString(dbType.ToUpperInvariant()) ?? "";

            if (string.IsNullOrEmpty(connStr))
            {
                return Results.BadRequest($"No connection string configured for {dbType}");
            }

            logger.LogInformation("API: Push changes, dbType {Type}", dbType);

            try
            {
                var body = await JsonSerializer.DeserializeAsync<PushChangesRequest>(request.Body);
                if (body?.Changes is null)
                {
                    return Results.BadRequest("Changes array required");
                }

                // Cap changes per request for production
                if (body.Changes.Count > 10000)
                {
                    return Results.BadRequest("Max 10000 changes per request");
                }

                var applied = dbType.ToLowerInvariant() switch
                {
                    "sqlite" => ApplyChangesToSqlite(connStr, body.Changes, body.OriginId, logger),
                    "postgres" => ApplyChangesToPostgres(
                        connStr,
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

                return Results.Ok(
                    new { Applied = applied, Timestamp = DateTime.UtcNow.ToString("O") }
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "API: Failed to push changes");
                return Results.Problem(ex.Message);
            }
        }
    )
    .RequireRateLimiting("sync");

// Register client
app.MapPost(
        "/sync/clients",
        async (
            HttpRequest request,
            string? connectionString,
            string dbType,
            ILogger<Program> logger,
            IConfiguration config
        ) =>
        {
            var connStr =
                connectionString ?? config.GetConnectionString(dbType.ToUpperInvariant()) ?? "";

            if (string.IsNullOrEmpty(connStr))
            {
                return Results.BadRequest($"No connection string configured for {dbType}");
            }

            logger.LogInformation("API: Register client, dbType {Type}", dbType);

            try
            {
                var body = await JsonSerializer.DeserializeAsync<RegisterClientRequest>(
                    request.Body
                );
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
                    "sqlite" => UpsertClientSqlite(connStr, client, logger),
                    "postgres" => UpsertClientPostgres(connStr, client, logger),
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
    )
    .RequireRateLimiting("sync");

// Get server state (max version)
app.MapGet(
        "/sync/state",
        (string? connectionString, string dbType, ILogger<Program> logger, IConfiguration config) =>
        {
            var connStr =
                connectionString ?? config.GetConnectionString(dbType.ToUpperInvariant()) ?? "";

            if (string.IsNullOrEmpty(connStr))
            {
                return Results.BadRequest($"No connection string configured for {dbType}");
            }

            logger.LogInformation("API: Get sync state, dbType {Type}", dbType);

            try
            {
                var maxVersion = dbType.ToLowerInvariant() switch
                {
                    "sqlite" => GetMaxVersionSqlite(connStr, logger),
                    "postgres" => GetMaxVersionPostgres(connStr, logger),
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
    )
    .RequireRateLimiting("sync");

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
            logger.LogInformation(
                "API: SSE subscribe to table {Table}, pk {Pk}",
                tableName,
                pkValue
            );

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
    )
    .RequireRateLimiting("sse");

// Unsubscribe endpoint
app.MapDelete(
        "/sync/subscribe/{subscriptionId}",
        (string subscriptionId, ApiSubscriptionManager subscriptions, ILogger<Program> logger) =>
        {
            logger.LogInformation("API: Unsubscribe {Id}", subscriptionId);
            subscriptions.Unsubscribe(subscriptionId);
            return Results.Ok(new { Unsubscribed = true });
        }
    )
    .RequireRateLimiting("sync");

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
    /// Production hardened with bounded channels and cleanup.
    /// </summary>
    public sealed class ApiSubscriptionManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();
        private readonly ILogger<ApiSubscriptionManager> _logger;
#pragma warning disable IDE0052 // Timer keeps cleanup alive
        private readonly Timer _cleanupTimer;
#pragma warning restore IDE0052

        /// <summary>
        /// Creates a new subscription manager with auto-cleanup.
        /// </summary>
        public ApiSubscriptionManager(ILogger<ApiSubscriptionManager> logger)
        {
            _logger = logger;
            // Cleanup stale subscriptions every 5 minutes
            _cleanupTimer = new Timer(
                CleanupStaleSubscriptions,
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5)
            );
        }

        /// <summary>
        /// Disposes resources including the cleanup timer.
        /// </summary>
        public void Dispose() => _cleanupTimer.Dispose();

        /// <summary>
        /// Subscribe to changes for a table (and optionally specific record).
        /// Uses BOUNDED channel to prevent memory exhaustion.
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

            // PRODUCTION: Bounded channel prevents memory exhaustion
            var channel = System.Threading.Channels.Channel.CreateBounded<SyncLogEntry>(
                new System.Threading.Channels.BoundedChannelOptions(1000)
                {
                    FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest,
                }
            );
            var sub = new Subscription(
                subscriptionId,
                tableName,
                pkValue,
                channel,
                DateTime.UtcNow
            );
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
                    // TryWrite returns false if channel is full - that's ok, we drop oldest
                    _ = sub.Channel.Writer.TryWrite(syncEntry);
                }
            }
        }

        private void CleanupStaleSubscriptions(object? state)
        {
            var staleThreshold = DateTime.UtcNow.AddHours(-1);
            var staleIds = _subscriptions
                .Where(kvp => kvp.Value.CreatedAt < staleThreshold)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var id in staleIds)
            {
                _logger.LogInformation("SUBS: Cleaning up stale subscription {Id}", id);
                Unsubscribe(id);
            }
        }

        private sealed record Subscription(
            string Id,
            string TableName,
            string? PkValue,
            System.Threading.Channels.Channel<SyncLogEntry> Channel,
            DateTime CreatedAt
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
