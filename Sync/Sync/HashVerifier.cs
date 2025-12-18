using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Results;

namespace Sync;

/// <summary>
/// Computes and verifies hashes for sync verification.
/// Implements spec Section 15 (Hash Verification).
/// </summary>
public static class HashVerifier
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Computes SHA-256 hash for a batch of changes.
    /// Format: "{version}:{table_name}:{pk_value}:{operation}:{payload}\n"
    /// </summary>
    /// <param name="changes">Changes to hash, must be in version order.</param>
    /// <returns>Lowercase hex-encoded SHA-256 hash.</returns>
    public static string ComputeBatchHash(IEnumerable<SyncLogEntry> changes)
    {
        var sb = new StringBuilder();

        foreach (var change in changes)
        {
            var opName = change.Operation.ToString().ToLowerInvariant();
            sb.Append(change.Version)
                .Append(':')
                .Append(change.TableName)
                .Append(':')
                .Append(change.PkValue)
                .Append(':')
                .Append(opName)
                .Append(':')
                .Append(change.Payload ?? "null")
                .Append('\n');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Computes full database hash for all tracked tables.
    /// Tables are processed alphabetically, rows ordered by PK.
    /// </summary>
    /// <param name="tables">Alphabetically sorted table names.</param>
    /// <param name="getTableRows">Function to get rows for a table, ordered by PK.</param>
    /// <returns>Lowercase hex-encoded SHA-256 hash.</returns>
    public static string ComputeDatabaseHash(
        IEnumerable<string> tables,
        Func<string, IEnumerable<Dictionary<string, object?>>> getTableRows
    )
    {
        using var stream = new MemoryStream();

        foreach (var tableName in tables.OrderBy(t => t, StringComparer.Ordinal))
        {
            var tableNameBytes = Encoding.UTF8.GetBytes(tableName + "\n");
            stream.Write(tableNameBytes, 0, tableNameBytes.Length);

            foreach (var row in getTableRows(tableName))
            {
                var canonicalJson = ToCanonicalJson(row);
                var rowBytes = Encoding.UTF8.GetBytes(canonicalJson + "\n");
                stream.Write(rowBytes, 0, rowBytes.Length);
            }
        }

        var hash = SHA256.HashData(stream.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Verifies that two hashes match.
    /// </summary>
    /// <param name="expected">Expected hash value.</param>
    /// <param name="actual">Actual computed hash value.</param>
    /// <returns>Success if hashes match, HashMismatch error otherwise.</returns>
    public static Result<bool, SyncError> VerifyHash(string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)
            ? new Result<bool, SyncError>.Success(true)
            : new Result<bool, SyncError>.Failure(new SyncErrorHashMismatch(expected, actual));

    /// <summary>
    /// Converts a dictionary to canonical JSON per spec Section 15.2.
    /// Keys sorted alphabetically, no whitespace, minimal escaping.
    /// </summary>
    /// <param name="data">Data to serialize.</param>
    /// <returns>Canonical JSON string.</returns>
    public static string ToCanonicalJson(Dictionary<string, object?> data)
    {
        var sorted = new SortedDictionary<string, object?>(data, StringComparer.Ordinal);
        return JsonSerializer.Serialize(sorted, CanonicalJsonOptions);
    }

    /// <summary>
    /// Parses a JSON string to a dictionary for hashing.
    /// </summary>
    /// <param name="json">JSON string to parse.</param>
    /// <returns>Dictionary or empty dictionary on parse failure.</returns>
    public static Dictionary<string, object?> ParseJson(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
