namespace Clinical.Sync;

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sync;

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
/// Background service that pulls Practitioner data from Scheduling.Api and maps to sync_Provider.
/// </summary>
internal sealed class SyncWorker : BackgroundService
{
    private readonly ILogger<SyncWorker> _logger;
    private readonly Func<SqliteConnection> _getConnection;
    private readonly string _schedulingApiUrl;

    /// <summary>
    /// Initializes a new instance of the SyncWorker class.
    /// </summary>
    public SyncWorker(
        ILogger<SyncWorker> logger,
        Func<SqliteConnection> getConnection,
        string schedulingApiUrl
    )
    {
        _logger = logger;
        _getConnection = getConnection;
        _schedulingApiUrl = schedulingApiUrl;
    }

    /// <summary>
    /// Executes the sync worker background service.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Log(
            LogLevel.Information,
            "Clinical.Sync worker starting at {Time}",
            DateTimeOffset.Now
        );

        await Task.Delay(2000, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformSync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Log(LogLevel.Error, ex, "Error during sync operation");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task PerformSync(CancellationToken cancellationToken)
    {
        _logger.Log(
            LogLevel.Information,
            "Starting sync from Scheduling.Api at {Time}",
            DateTimeOffset.Now
        );

        using var httpClient = new HttpClient { BaseAddress = new Uri(_schedulingApiUrl) };

        var changesResponse = await httpClient
            .GetAsync("/sync/changes?limit=100", cancellationToken)
            .ConfigureAwait(false);
        if (!changesResponse.IsSuccessStatusCode)
        {
            _logger.Log(
                LogLevel.Warning,
                "Failed to fetch changes from Scheduling.Api: {StatusCode}",
                changesResponse.StatusCode
            );
            return;
        }

        var changesJson = await changesResponse
            .Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        var changes = JsonSerializer.Deserialize<List<SyncChange>>(changesJson);

        if (changes == null || changes.Count == 0)
        {
            _logger.Log(LogLevel.Information, "No changes to sync");
            return;
        }

        _logger.Log(LogLevel.Information, "Processing {Count} changes", changes.Count);

        using var conn = _getConnection();
        await using var transaction = await conn.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var practitionerChanges = changes
                .Where(c => c.TableName == "fhir_Practitioner")
                .ToList();

            foreach (var change in practitionerChanges)
            {
                ApplyMappedChange(conn, transaction, change);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger.Log(
                LogLevel.Information,
                "Successfully synced {Count} provider changes",
                practitionerChanges.Count
            );
        }
        catch (Exception ex)
        {
            _logger.Log(
                LogLevel.Error,
                ex,
                "Error applying sync changes, rolling back transaction"
            );
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void ApplyMappedChange(
        SqliteConnection conn,
        System.Data.Common.DbTransaction transaction,
        SyncChange change
    )
    {
        if (change.Operation == "DELETE")
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = "DELETE FROM sync_Provider WHERE ProviderId = @id";
            cmd.Parameters.AddWithValue("@id", change.RowId);
            cmd.ExecuteNonQuery();
            _logger.Log(LogLevel.Debug, "Deleted provider {ProviderId}", change.RowId);
            return;
        }

        if (change.Data == null)
        {
            return;
        }

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(change.Data);
        if (data == null)
        {
            return;
        }

        using var upsertCmd = conn.CreateCommand();
        upsertCmd.Transaction = (SqliteTransaction)transaction;
        upsertCmd.CommandText = """
            INSERT INTO sync_Provider (ProviderId, FirstName, LastName, Specialty, SyncedAt)
            VALUES (@providerId, @firstName, @lastName, @specialty, @syncedAt)
            ON CONFLICT(ProviderId) DO UPDATE SET
                FirstName = @firstName,
                LastName = @lastName,
                Specialty = @specialty,
                SyncedAt = @syncedAt
            """;

        upsertCmd.Parameters.AddWithValue(
            "@providerId",
            data.GetValueOrDefault("Id").GetString() ?? string.Empty
        );
        upsertCmd.Parameters.AddWithValue(
            "@firstName",
            data.GetValueOrDefault("NameGiven").GetString() ?? string.Empty
        );
        upsertCmd.Parameters.AddWithValue(
            "@lastName",
            data.GetValueOrDefault("NameFamily").GetString() ?? string.Empty
        );
        upsertCmd.Parameters.AddWithValue(
            "@specialty",
            data.GetValueOrDefault("Specialty").GetString() ?? string.Empty
        );
        upsertCmd.Parameters.AddWithValue("@syncedAt", DateTime.UtcNow.ToString("o"));

        upsertCmd.ExecuteNonQuery();
        _logger.Log(
            LogLevel.Debug,
            "Upserted provider {ProviderId}",
            data.GetValueOrDefault("Id").GetString()
        );
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
