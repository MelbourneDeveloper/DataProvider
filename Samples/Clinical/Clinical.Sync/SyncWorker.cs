using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clinical.Sync;

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
    /// FAULT TOLERANT: This worker NEVER crashes. It handles all errors gracefully and retries indefinitely.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Log(
            LogLevel.Information,
            "[SYNC-START] Clinical.Sync worker starting at {Time}. Target: {Url}",
            DateTimeOffset.Now,
            _schedulingApiUrl
        );

        var consecutiveFailures = 0;
        const int maxConsecutiveFailuresBeforeWarning = 3;

        // Main sync loop - NEVER exits except on cancellation
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformSync(stoppingToken).ConfigureAwait(false);

                // Reset failure counter on success
                if (consecutiveFailures > 0)
                {
                    _logger.Log(
                        LogLevel.Information,
                        "[SYNC-RECOVERED] Sync recovered after {Count} consecutive failures",
                        consecutiveFailures
                    );
                    consecutiveFailures = 0;
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                consecutiveFailures++;
                var retryDelay = Math.Min(5 * consecutiveFailures, 30); // Exponential backoff up to 30s

                if (consecutiveFailures >= maxConsecutiveFailuresBeforeWarning)
                {
                    _logger.Log(
                        LogLevel.Warning,
                        "[SYNC-FAULT] Scheduling.Api unreachable for {Count} consecutive attempts. Error: {Message}. Retrying in {Delay}s...",
                        consecutiveFailures,
                        ex.Message,
                        retryDelay
                    );
                }
                else
                {
                    _logger.Log(
                        LogLevel.Information,
                        "[SYNC-RETRY] Scheduling.Api not reachable ({Message}). Attempt {Count}, retrying in {Delay}s...",
                        ex.Message,
                        consecutiveFailures,
                        retryDelay
                    );
                }

                await Task.Delay(TimeSpan.FromSeconds(retryDelay), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.Log(
                    LogLevel.Information,
                    "[SYNC-SHUTDOWN] Sync worker shutting down gracefully"
                );
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                var retryDelay = Math.Min(10 * consecutiveFailures, 60); // Longer backoff for unknown errors

                _logger.Log(
                    LogLevel.Error,
                    ex,
                    "[SYNC-ERROR] Unexpected error during sync (attempt {Count}). Retrying in {Delay}s. Error type: {Type}",
                    consecutiveFailures,
                    retryDelay,
                    ex.GetType().Name
                );

                await Task.Delay(TimeSpan.FromSeconds(retryDelay), stoppingToken)
                    .ConfigureAwait(false);
            }
        }

        _logger.Log(
            LogLevel.Information,
            "[SYNC-EXIT] Clinical.Sync worker exited at {Time}",
            DateTimeOffset.Now
        );
    }

    private async Task PerformSync(CancellationToken cancellationToken)
    {
        _logger.Log(
            LogLevel.Information,
            "Starting sync from Scheduling.Api at {Time}",
            DateTimeOffset.Now
        );

        using var httpClient = new HttpClient { BaseAddress = new Uri(_schedulingApiUrl) };
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateSyncToken());

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
        // Extract the ID from PkValue which is JSON like {"Id":"uuid-here"}
        var pkData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(change.PkValue);
        var rowId = pkData?.GetValueOrDefault("Id").GetString() ?? change.PkValue;

        if (change.Operation == SyncChange.Delete)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = "DELETE FROM sync_Provider WHERE ProviderId = @id";
            cmd.Parameters.AddWithValue("@id", rowId);
            cmd.ExecuteNonQuery();
            _logger.Log(LogLevel.Debug, "Deleted provider {ProviderId}", rowId);
            return;
        }

        if (change.Payload == null)
        {
            return;
        }

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(change.Payload);
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

    private static readonly string[] SyncRoles = ["sync-client", "clinician", "scheduler", "admin"];

    /// <summary>
    /// Generates a JWT token for sync worker authentication.
    /// Uses the dev mode signing key (32 zeros) for E2E testing.
    /// </summary>
    private static string GenerateSyncToken()
    {
        var signingKey = new byte[32]; // 32 zeros = dev mode key
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));
        var expiration = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var payload = Base64UrlEncode(
            Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(
                    new
                    {
                        sub = "clinical-sync-worker",
                        name = "Clinical Sync Worker",
                        email = "sync@clinical.local",
                        jti = Guid.NewGuid().ToString(),
                        exp = expiration,
                        roles = SyncRoles,
                    }
                )
            )
        );
        var signature = ComputeHmacSignature(header, payload, signingKey);
        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string ComputeHmacSignature(string header, string payload, byte[] key)
    {
        var data = Encoding.UTF8.GetBytes($"{header}.{payload}");
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(data);
        return Base64UrlEncode(hash);
    }
}
