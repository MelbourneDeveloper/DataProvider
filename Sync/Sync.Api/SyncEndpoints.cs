#pragma warning disable CA1848 // Use LoggerMessage delegates for performance

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sync.Postgres;
using Sync.SQLite;

namespace Sync.Api;

/// <summary>
/// Extension methods to map sync HTTP endpoints.
/// Implements spec Section 10 (Real-Time Subscriptions) and Section 11 (Bi-Directional Sync).
/// </summary>
public static class SyncEndpoints
{
    /// <summary>
    /// Maps all sync-related HTTP endpoints.
    /// </summary>
    /// <param name="app">Web application builder.</param>
    /// <returns>Endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var sync = app.MapGroup("/api/sync").WithTags("Sync");

        // Get changes endpoint (pull)
        sync.MapGet("/changes", GetChanges);

        // Push changes endpoint
        sync.MapPost("/changes", PushChanges);

        // Get current sync state
        sync.MapGet("/state", GetSyncState);

        // Initialize sync schema
        sync.MapPost("/init", InitializeSchema);

        // Real-time subscriptions via SSE
        sync.MapGet("/subscribe/table/{tableName}", SubscribeToTable);
        sync.MapGet("/subscribe/record/{tableName}/{pkValue}", SubscribeToRecord);

        // Client registration
        sync.MapPost("/clients/register", RegisterClient);
        sync.MapGet("/clients", GetClients);

        return app;
    }

    private static IResult GetChanges(
        HttpContext context,
        long fromVersion = 0,
        int batchSize = 1000,
        string db = "sqlite"
    )
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation(
            "GET /changes: fromVersion={FromVersion}, batchSize={BatchSize}, db={Db}",
            fromVersion,
            batchSize,
            db
        );

        if (db.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            var getConn = context.RequestServices.GetRequiredService<Func<NpgsqlConnection>>();
            using var conn = getConn();
            var result = PostgresSyncLogRepository.FetchChanges(conn, fromVersion, batchSize + 1);

            return result switch
            {
                SyncLogListOk ok => CreateBatchResponse(ok.Value, fromVersion, batchSize),
                SyncLogListError err => Results.Problem(err.Value.Message),
                _ => Results.Problem("Unknown error"),
            };
        }
        else
        {
            var getConn = context.RequestServices.GetRequiredService<Func<SqliteConnection>>();
            using var conn = getConn();
            var result = SyncLogRepository.FetchChanges(conn, fromVersion, batchSize + 1);

            return result switch
            {
                SyncLogListOk ok => CreateBatchResponse(ok.Value, fromVersion, batchSize),
                SyncLogListError err => Results.Problem(err.Value.Message),
                _ => Results.Problem("Unknown error"),
            };
        }
    }

    private static IResult CreateBatchResponse(
        IReadOnlyList<SyncLogEntry> changes,
        long fromVersion,
        int batchSize
    )
    {
        var hasMore = changes.Count > batchSize;
        var batchChanges = hasMore ? changes.Take(batchSize).ToList() : changes.ToList();
        var toVersion = batchChanges.Count > 0 ? batchChanges[^1].Version : fromVersion;
        var hash = HashVerifier.ComputeBatchHash(batchChanges);

        return Results.Ok(
            new SyncBatchResponse(
                Changes: batchChanges,
                FromVersion: fromVersion,
                ToVersion: toVersion,
                HasMore: hasMore,
                Hash: hash
            )
        );
    }

    private static async Task<IResult> PushChanges(
        HttpContext context,
        PushChangesRequest request,
        string db = "sqlite"
    )
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation(
            "POST /changes: {Count} changes, db={Db}",
            request.Changes.Count,
            db
        );

        var hub = context.RequestServices.GetRequiredService<SubscriptionHub>();

        if (db.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            var getConn = context.RequestServices.GetRequiredService<Func<NpgsqlConnection>>();
            using var conn = getConn();

            // Enable suppression
            PostgresSyncSession.EnableSuppression(conn);
            try
            {
                foreach (var entry in request.Changes)
                {
                    var result = PostgresChangeApplier.ApplyChange(conn, entry, logger);
                    if (result is BoolSyncError err)
                    {
                        return Results.Problem(err.Value.Message);
                    }

                    // Notify subscribers
                    await hub.NotifyChange(entry);
                }
            }
            finally
            {
                PostgresSyncSession.DisableSuppression(conn);
            }
        }
        else
        {
            var getConn = context.RequestServices.GetRequiredService<Func<SqliteConnection>>();
            using var conn = getConn();

            // Enable suppression
            SyncSessionManager.EnableSuppression(conn);
            try
            {
                foreach (var entry in request.Changes)
                {
                    var result = ChangeApplierSQLite.ApplyChange(conn, entry, logger);
                    if (result is BoolSyncError err)
                    {
                        return Results.Problem(err.Value.Message);
                    }

                    // Notify subscribers
                    await hub.NotifyChange(entry);
                }
            }
            finally
            {
                SyncSessionManager.DisableSuppression(conn);
            }
        }

        return Results.Ok(new { Applied = request.Changes.Count });
    }

    private static IResult GetSyncState(HttpContext context, string db = "sqlite")
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("GET /state: db={Db}", db);

        if (db.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            var getConn = context.RequestServices.GetRequiredService<Func<NpgsqlConnection>>();
            using var conn = getConn();

            var originResult = PostgresSyncSchema.GetOriginId(conn);
            var versionResult = PostgresSyncLogRepository.GetLastServerVersion(conn);
            var maxResult = PostgresSyncLogRepository.GetMaxVersion(conn);

            return Results.Ok(
                new SyncStateResponse(
                    OriginId: originResult is StringSyncOk o ? o.Value : "",
                    LastServerVersion: versionResult is LongSyncOk v ? v.Value : 0,
                    MaxVersion: maxResult is LongSyncOk m ? m.Value : 0
                )
            );
        }
        else
        {
            var getConn = context.RequestServices.GetRequiredService<Func<SqliteConnection>>();
            using var conn = getConn();

            var originResult = SyncSchema.GetOriginId(conn);
            var versionResult = SyncLogRepository.GetLastServerVersion(conn);
            var maxResult = SyncLogRepository.GetMaxVersion(conn);

            return Results.Ok(
                new SyncStateResponse(
                    OriginId: originResult is StringSyncOk o ? o.Value : "",
                    LastServerVersion: versionResult is LongSyncOk v ? v.Value : 0,
                    MaxVersion: maxResult is LongSyncOk m ? m.Value : 0
                )
            );
        }
    }

    private static IResult InitializeSchema(
        HttpContext context,
        InitSchemaRequest request,
        string db = "sqlite"
    )
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("POST /init: db={Db}, originId={OriginId}", db, request.OriginId);

        if (db.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            var getConn = context.RequestServices.GetRequiredService<Func<NpgsqlConnection>>();
            using var conn = getConn();

            var schemaResult = PostgresSyncSchema.CreateSchema(conn);
            if (schemaResult is BoolSyncError schemaErr)
            {
                return Results.Problem(schemaErr.Value.Message);
            }

            if (!string.IsNullOrEmpty(request.OriginId))
            {
                var originResult = PostgresSyncSchema.SetOriginId(conn, request.OriginId);
                if (originResult is BoolSyncError originErr)
                {
                    return Results.Problem(originErr.Value.Message);
                }
            }
        }
        else
        {
            var getConn = context.RequestServices.GetRequiredService<Func<SqliteConnection>>();
            using var conn = getConn();

            var schemaResult = SyncSchema.CreateSchema(conn);
            if (schemaResult is BoolSyncError schemaErr)
            {
                return Results.Problem(schemaErr.Value.Message);
            }

            if (!string.IsNullOrEmpty(request.OriginId))
            {
                var originResult = SyncSchema.SetOriginId(conn, request.OriginId);
                if (originResult is BoolSyncError originErr)
                {
                    return Results.Problem(originErr.Value.Message);
                }
            }
        }

        return Results.Ok(new { Initialized = true });
    }

    private static async Task SubscribeToTable(
        HttpContext context,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var hub = context.RequestServices.GetRequiredService<SubscriptionHub>();

        logger.LogInformation("SSE: Subscribing to table {Table}", tableName);

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        var subscriptionId = Guid.NewGuid().ToString();
        var channel = hub.Subscribe(subscriptionId, tableName, null);

        try
        {
            await context.Response.WriteAsync($"event: connected\ndata: {{\"subscriptionId\":\"{subscriptionId}\"}}\n\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);

            await foreach (var change in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(change);
                await context.Response.WriteAsync($"event: change\ndata: {json}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            hub.Unsubscribe(subscriptionId);
            logger.LogInformation("SSE: Unsubscribed {SubscriptionId}", subscriptionId);
        }
    }

    private static async Task SubscribeToRecord(
        HttpContext context,
        string tableName,
        string pkValue,
        CancellationToken cancellationToken
    )
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var hub = context.RequestServices.GetRequiredService<SubscriptionHub>();

        logger.LogInformation("SSE: Subscribing to record {Table}/{Pk}", tableName, pkValue);

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        var subscriptionId = Guid.NewGuid().ToString();
        var channel = hub.Subscribe(subscriptionId, tableName, pkValue);

        try
        {
            await context.Response.WriteAsync($"event: connected\ndata: {{\"subscriptionId\":\"{subscriptionId}\"}}\n\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);

            await foreach (var change in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(change);
                await context.Response.WriteAsync($"event: change\ndata: {json}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            hub.Unsubscribe(subscriptionId);
            logger.LogInformation("SSE: Unsubscribed {SubscriptionId}", subscriptionId);
        }
    }

    private static IResult RegisterClient(HttpContext context, RegisterClientRequest request, string db = "sqlite")
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("POST /clients/register: originId={OriginId}, db={Db}", request.OriginId, db);

        var client = new SyncClient(
            OriginId: request.OriginId,
            LastSyncVersion: request.LastSyncVersion,
            LastSyncTimestamp: DateTime.UtcNow.ToString("O"),
            CreatedAt: DateTime.UtcNow.ToString("O")
        );

        if (db.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            var getConn = context.RequestServices.GetRequiredService<Func<NpgsqlConnection>>();
            using var conn = getConn();
            var result = PostgresSyncClientRepository.Upsert(conn, client);
            return result is BoolSyncOk ? Results.Ok(client) : Results.Problem(((BoolSyncError)result).Value.Message);
        }
        else
        {
            var getConn = context.RequestServices.GetRequiredService<Func<SqliteConnection>>();
            using var conn = getConn();
            var result = SyncClientRepository.Upsert(conn, client);
            return result is BoolSyncOk ? Results.Ok(client) : Results.Problem(((BoolSyncError)result).Value.Message);
        }
    }

    private static IResult GetClients(HttpContext context, string db = "sqlite")
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("GET /clients: db={Db}", db);

        if (db.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            var getConn = context.RequestServices.GetRequiredService<Func<NpgsqlConnection>>();
            using var conn = getConn();
            var result = PostgresSyncClientRepository.GetAll(conn);
            return result is SyncClientListOk ok
                ? Results.Ok(ok.Value)
                : Results.Problem(((SyncClientListError)result).Value.Message);
        }
        else
        {
            var getConn = context.RequestServices.GetRequiredService<Func<SqliteConnection>>();
            using var conn = getConn();
            var result = SyncClientRepository.GetAll(conn);
            return result is SyncClientListOk ok
                ? Results.Ok(ok.Value)
                : Results.Problem(((SyncClientListError)result).Value.Message);
        }
    }
}

/// <summary>
/// Response for sync batch retrieval.
/// </summary>
public sealed record SyncBatchResponse(
    IReadOnlyList<SyncLogEntry> Changes,
    long FromVersion,
    long ToVersion,
    bool HasMore,
    string Hash
);

/// <summary>
/// Response for sync state.
/// </summary>
public sealed record SyncStateResponse(string OriginId, long LastServerVersion, long MaxVersion);

/// <summary>
/// Request to push changes.
/// </summary>
public sealed record PushChangesRequest(IReadOnlyList<SyncLogEntry> Changes);

/// <summary>
/// Request to initialize schema.
/// </summary>
public sealed record InitSchemaRequest(string? OriginId);

/// <summary>
/// Request to register a client.
/// </summary>
public sealed record RegisterClientRequest(string OriginId, long LastSyncVersion);
