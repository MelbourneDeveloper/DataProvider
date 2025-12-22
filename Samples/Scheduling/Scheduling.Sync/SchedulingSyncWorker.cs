using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Scheduling.Sync;

/// <summary>
/// Background worker that syncs patient data from Clinical domain to Scheduling domain.
/// Applies column mappings defined in SyncMappings.json.
/// </summary>
internal sealed class SchedulingSyncWorker : BackgroundService
{
    private readonly ILogger<SchedulingSyncWorker> _logger;
    private readonly Func<SqliteConnection> _getConnection;
    private readonly string _clinicalEndpoint;
    private readonly int _pollIntervalSeconds;

    /// <summary>
    /// Creates a new scheduling sync worker.
    /// </summary>
    public SchedulingSyncWorker(
        ILogger<SchedulingSyncWorker> logger,
        Func<SqliteConnection> getConnection,
        string clinicalEndpoint
    )
    {
        _logger = logger;
        _getConnection = getConnection;
        _clinicalEndpoint = clinicalEndpoint;
        _pollIntervalSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("POLL_INTERVAL_SECONDS"),
            out var interval
        )
            ? interval
            : 30;
    }

    /// <summary>
    /// Main sync loop - polls Clinical domain for patient changes and applies mappings.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Scheduling sync worker starting. Polling {Endpoint} every {Interval}s",
            _clinicalEndpoint,
            _pollIntervalSeconds
        );

        // Initial delay to let APIs start
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncPatientDataAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during sync cycle");
            }

            await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken);
        }
    }

    /// <summary>
    /// Fetches changes from Clinical domain and applies column mappings to sync_ScheduledPatient.
    /// </summary>
    private async Task SyncPatientDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting sync cycle from Clinical.Api");

        using var conn = _getConnection();

        // Get last sync version
        var lastVersion = GetLastSyncVersion(conn);

        // Fetch changes from Clinical domain
        var changesUrl = $"{_clinicalEndpoint}/sync/changes?fromVersion={lastVersion}&limit=100";

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateSyncToken());

        var response = await httpClient.GetAsync(changesUrl, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Failed to fetch changes from {Url}: {Status}",
                changesUrl,
                response.StatusCode
            );
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var changes = JsonSerializer.Deserialize<SyncChange[]>(content);

        if (changes is null || changes.Length == 0)
        {
            _logger.LogDebug("No changes to sync");
            return;
        }

        _logger.LogInformation("Processing {Count} changes", changes.Length);

        // Apply changes with column mapping
        foreach (var change in changes)
        {
            ApplyMappedChange(conn, change);
        }

        // Update last sync version
        UpdateLastSyncVersion(conn, changes.Max(c => c.Version));

        _logger.LogInformation("Sync cycle complete. Processed {Count} changes", changes.Length);
    }

    /// <summary>
    /// Applies a change from Clinical domain to sync_ScheduledPatient with column mapping.
    /// Maps: fhir_Patient -> sync_ScheduledPatient
    /// Transforms: DisplayName = concat(GivenName, ' ', FamilyName)
    /// </summary>
    private void ApplyMappedChange(SqliteConnection connection, SyncChange change)
    {
        try
        {
            if (change.TableName != "fhir_Patient")
            {
                return; // Only sync patient data
            }

            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                change.Payload ?? "{}"
            );

            if (data is null)
            {
                _logger.LogWarning("Failed to parse row data for change {Version}", change.Version);
                return;
            }

            // Apply column mapping
            var patientId = data.TryGetValue("Id", out var id) ? id.GetString() : null;
            var givenName = data.TryGetValue("GivenName", out var gn) ? gn.GetString() : "";
            var familyName = data.TryGetValue("FamilyName", out var fn) ? fn.GetString() : "";
            var phone = data.TryGetValue("Phone", out var p) ? p.GetString() : null;
            var email = data.TryGetValue("Email", out var e) ? e.GetString() : null;

            if (patientId is null)
            {
                _logger.LogWarning("Patient change missing Id field");
                return;
            }

            // Transform: DisplayName = concat(GivenName, ' ', FamilyName)
            var displayName = $"{givenName} {familyName}".Trim();

            // Upsert to sync_ScheduledPatient
            if (change.Operation == SyncChange.Delete)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM sync_ScheduledPatient WHERE PatientId = @id";
                cmd.Parameters.AddWithValue("@id", patientId);
                cmd.ExecuteNonQuery();

                _logger.LogDebug("Deleted patient {PatientId} from sync table", patientId);
            }
            else
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO sync_ScheduledPatient (PatientId, DisplayName, ContactPhone, ContactEmail, SyncedAt)
                    VALUES (@id, @name, @phone, @email, datetime('now'))
                    ON CONFLICT (PatientId) DO UPDATE SET
                        DisplayName = excluded.DisplayName,
                        ContactPhone = excluded.ContactPhone,
                        ContactEmail = excluded.ContactEmail,
                        SyncedAt = datetime('now')
                    """;

                cmd.Parameters.AddWithValue("@id", patientId);
                cmd.Parameters.AddWithValue("@name", displayName);
                cmd.Parameters.AddWithValue("@phone", (object?)phone ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@email", (object?)email ?? DBNull.Value);
                cmd.ExecuteNonQuery();

                _logger.LogDebug(
                    "Synced patient {PatientId}: {DisplayName}",
                    patientId,
                    displayName
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply change {Version}", change.Version);
        }
    }

    private long GetLastSyncVersion(SqliteConnection connection)
    {
        // Ensure _sync_state table exists
        using var createCmd = connection.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS _sync_state (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            )
            """;
        createCmd.ExecuteNonQuery();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM _sync_state WHERE key = 'last_clinical_sync_version'";

        var result = cmd.ExecuteScalar();
        return result is string str && long.TryParse(str, out var version) ? version : 0;
    }

    private void UpdateLastSyncVersion(SqliteConnection connection, long version)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO _sync_state (key, value) VALUES ('last_clinical_sync_version', @version)
            ON CONFLICT (key) DO UPDATE SET value = excluded.value
            """;
        cmd.Parameters.AddWithValue("@version", version.ToString());
        cmd.ExecuteNonQuery();
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
                        sub = "scheduling-sync-worker",
                        name = "Scheduling Sync Worker",
                        email = "sync@scheduling.local",
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

/// <summary>
/// Represents a sync change from the Clinical domain.
/// Matches the SyncLogEntry schema returned by /sync/changes endpoint.
/// </summary>
public sealed record SyncChange(
    long Version,
    string TableName,
    string PkValue,
    int Operation,
    string? Payload,
    string Origin,
    string Timestamp
)
{
    /// <summary>Insert operation (0).</summary>
    public const int Insert = 0;

    /// <summary>Update operation (1).</summary>
    public const int Update = 1;

    /// <summary>Delete operation (2).</summary>
    public const int Delete = 2;
}
