using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Results;

namespace Sync.SQLite;

/// <summary>
/// Applies sync changes to SQLite database.
/// Implements spec Section 11 (Bi-Directional Sync Protocol).
/// </summary>
public static class ChangeApplierSQLite
{
    /// <summary>
    /// Applies a single sync log entry to the database.
    /// Returns true on success, false on FK violation (for defer/retry).
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="entry">The sync log entry to apply.</param>
    /// <returns>True if applied, false if FK violation, or error.</returns>
    public static Result<bool, SyncError> ApplyChange(
        SqliteConnection connection,
        SyncLogEntry entry
    )
    {
        try
        {
            return entry.Operation switch
            {
                SyncOperation.Insert => ApplyInsert(connection, entry),
                SyncOperation.Update => ApplyUpdate(connection, entry),
                SyncOperation.Delete => ApplyDelete(connection, entry),
                _ => new Result<bool, SyncError>.Failure(
                    new SyncErrorDatabase($"Unknown operation: {entry.Operation}")
                ),
            };
        }
        catch (SqliteException ex) when (IsForeignKeyViolation(ex))
        {
            // FK violation - return false to defer for retry
            return new Result<bool, SyncError>.Success(false);
        }
        catch (SqliteException ex)
        {
            return new Result<bool, SyncError>.Failure(
                new SyncErrorDatabase($"Failed to apply change: {ex.Message}")
            );
        }
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Table names come from internal sync log, not user input")]
    private static Result<bool, SyncError> ApplyInsert(
        SqliteConnection connection,
        SyncLogEntry entry
    )
    {
        if (string.IsNullOrEmpty(entry.Payload))
        {
            return new Result<bool, SyncError>.Failure(
                new SyncErrorDatabase("Insert requires payload")
            );
        }

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.Payload);
        if (data == null || data.Count == 0)
        {
            return new Result<bool, SyncError>.Failure(
                new SyncErrorDatabase("Invalid payload JSON")
            );
        }

        var columns = string.Join(", ", data.Keys);
        var parameters = string.Join(", ", data.Keys.Select(k => $"@{k}"));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"INSERT OR REPLACE INTO {entry.TableName} ({columns}) VALUES ({parameters})";

        foreach (var kvp in data)
        {
            cmd.Parameters.AddWithValue($"@{kvp.Key}", JsonElementToValue(kvp.Value));
        }

        cmd.ExecuteNonQuery();
        return new Result<bool, SyncError>.Success(true);
    }

    private static Result<bool, SyncError> ApplyUpdate(
        SqliteConnection connection,
        SyncLogEntry entry
    )
    {
        // For simplicity, UPDATE uses same UPSERT logic as INSERT
        return ApplyInsert(connection, entry);
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Table names come from internal sync log, not user input")]
    private static Result<bool, SyncError> ApplyDelete(
        SqliteConnection connection,
        SyncLogEntry entry
    )
    {
        var pkData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.PkValue);
        if (pkData == null || pkData.Count == 0)
        {
            return new Result<bool, SyncError>.Failure(
                new SyncErrorDatabase("Invalid pk_value JSON")
            );
        }

        var pkColumn = pkData.Keys.First();
        var pkValue = JsonElementToValue(pkData[pkColumn]);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM {entry.TableName} WHERE {pkColumn} = @pk";
        cmd.Parameters.AddWithValue("@pk", pkValue);
        cmd.ExecuteNonQuery();

        return new Result<bool, SyncError>.Success(true);
    }

    private static object JsonElementToValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => 1L,
            JsonValueKind.False => 0L,
            JsonValueKind.Null => DBNull.Value,
            _ => element.ToString(),
        };

    private static bool IsForeignKeyViolation(SqliteException ex) =>
        ex.SqliteErrorCode == 19 && // SQLITE_CONSTRAINT
        ex.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
}
