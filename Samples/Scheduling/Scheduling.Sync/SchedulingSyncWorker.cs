using System.Text.Json;

namespace Scheduling.Sync;

/// <summary>
/// Background worker that syncs patient data from Clinical domain to Scheduling domain.
/// Applies column mappings defined in SyncMappings.json.
/// </summary>
internal sealed class SchedulingSyncWorker : BackgroundService
{
    private readonly ILogger<SchedulingSyncWorker> _logger;
    private readonly Func<NpgsqlConnection> _getConnection;
    private readonly string _clinicalEndpoint;
    private readonly int _pollIntervalSeconds;

    /// <summary>
    /// Creates a new scheduling sync worker.
    /// </summary>
    public SchedulingSyncWorker(
        ILogger<SchedulingSyncWorker> logger,
        Func<NpgsqlConnection> getConnection
    )
    {
        _logger = logger;
        _getConnection = getConnection;
        _clinicalEndpoint =
            Environment.GetEnvironmentVariable("CLINICAL_ENDPOINT") ?? "http://localhost:5001";
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncPatientDataAsync(stoppingToken);
            }
            catch (Exception ex)
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
        _logger.LogInformation("Starting sync cycle");

        using var conn = _getConnection();

        // Get last sync version
        var lastVersion = GetLastSyncVersion(conn);

        // Fetch changes from Clinical domain
        var changesUrl = $"{_clinicalEndpoint}/sync/changes?fromVersion={lastVersion}&limit=100";

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(changesUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Failed to fetch changes from {Url}: {Status}",
                changesUrl,
                response.StatusCode
            );
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
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
    private void ApplyMappedChange(NpgsqlConnection connection, SyncChange change)
    {
        try
        {
            if (change.TableName != "fhir_Patient")
            {
                return; // Only sync patient data
            }

            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                change.RowData ?? "{}"
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
            if (change.Operation == "DELETE")
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
                    VALUES (@id, @name, @phone, @email, CURRENT_TIMESTAMP)
                    ON CONFLICT (PatientId) DO UPDATE SET
                        DisplayName = EXCLUDED.DisplayName,
                        ContactPhone = EXCLUDED.ContactPhone,
                        ContactEmail = EXCLUDED.ContactEmail,
                        SyncedAt = CURRENT_TIMESTAMP
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

    private long GetLastSyncVersion(NpgsqlConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS _sync_state (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            SELECT value FROM _sync_state WHERE key = 'last_clinical_sync_version'
            """;

        var result = cmd.ExecuteScalar();
        return result is string str && long.TryParse(str, out var version) ? version : 0;
    }

    private void UpdateLastSyncVersion(NpgsqlConnection connection, long version)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO _sync_state (key, value) VALUES ('last_clinical_sync_version', @version)
            ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value
            """;
        cmd.Parameters.AddWithValue("@version", version.ToString());
        cmd.ExecuteNonQuery();
    }
}

/// <summary>
/// Represents a sync change from the Clinical domain.
/// </summary>
public sealed record SyncChange(long Version, string TableName, string Operation, string? RowData);
