using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sync;

/// <summary>
/// Result of applying a mapping to a sync log entry.
/// </summary>
/// <param name="TargetTable">Mapped target table name.</param>
/// <param name="TargetPkValue">Mapped primary key JSON.</param>
/// <param name="MappedPayload">Transformed payload JSON (null for deletes).</param>
/// <param name="MappingId">ID of the mapping that was applied.</param>
public sealed record MappedEntry(
    string TargetTable,
    string TargetPkValue,
    string? MappedPayload,
    string MappingId
);

/// <summary>
/// Mapping result - either mapped entries or skip reason.
/// </summary>
public abstract record MappingResult;

/// <summary>
/// Entry was mapped successfully to one or more targets.
/// </summary>
/// <param name="Entries">Mapped entries (may be multiple for multi-target).</param>
public sealed record MappingSuccess(IReadOnlyList<MappedEntry> Entries) : MappingResult;

/// <summary>
/// Entry was skipped (filtered, excluded, or no mapping).
/// </summary>
/// <param name="Reason">Reason for skipping.</param>
public sealed record MappingSkipped(string Reason) : MappingResult;

/// <summary>
/// Mapping failed with an error.
/// </summary>
/// <param name="Error">Error that occurred.</param>
public sealed record MappingFailed(SyncError Error) : MappingResult;

/// <summary>
/// Engine for applying data mappings to sync log entries.
/// Implements spec Section 7 - Data Mapping.
/// </summary>
public static class MappingEngine
{
    /// <summary>
    /// Applies mapping configuration to a sync log entry.
    /// Per spec Section 7.5.3 - Sync Decision Logic.
    /// </summary>
    /// <param name="entry">Sync log entry to map.</param>
    /// <param name="config">Mapping configuration.</param>
    /// <param name="direction">Current sync direction.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Mapping result.</returns>
    public static MappingResult ApplyMapping(
        SyncLogEntry entry,
        SyncMappingConfig config,
        MappingDirection direction,
        ILogger logger
    )
    {
        var mapping = FindMapping(entry.TableName, config, direction);

        if (mapping is null)
        {
            return config.UnmappedTableBehavior switch
            {
                UnmappedTableBehavior.Passthrough => new MappingSuccess(
                    [CreateIdentityMappedEntry(entry)]
                ),
                _ => new MappingSkipped($"No mapping for table {entry.TableName}"),
            };
        }

        if (!mapping.Enabled)
        {
            return new MappingSkipped($"Mapping {mapping.Id} is disabled");
        }

        return mapping.IsMultiTarget
            ? ApplyMultiTargetMapping(entry, mapping, logger)
            : ApplySingleTargetMapping(entry, mapping, logger);
    }

    /// <summary>
    /// Finds the mapping for a table and direction.
    /// </summary>
    /// <param name="tableName">Source table name.</param>
    /// <param name="config">Mapping configuration.</param>
    /// <param name="direction">Sync direction.</param>
    /// <returns>Matching mapping or null.</returns>
    public static TableMapping? FindMapping(
        string tableName,
        SyncMappingConfig config,
        MappingDirection direction
    ) =>
        config.Mappings.FirstOrDefault(m =>
            m.SourceTable == tableName
            && (m.Direction == direction || m.Direction == MappingDirection.Both)
        );

    /// <summary>
    /// Applies a single-target mapping to an entry.
    /// </summary>
    private static MappingResult ApplySingleTargetMapping(
        SyncLogEntry entry,
        TableMapping mapping,
        ILogger logger
    )
    {
        try
        {
            var targetTable = mapping.TargetTable ?? mapping.SourceTable;
            var targetPk = MapPrimaryKey(entry.PkValue, mapping.PkMapping);
            var mappedPayload = MapPayload(entry.Payload, mapping, logger);

            return new MappingSuccess(
                [new MappedEntry(targetTable, targetPk, mappedPayload, mapping.Id)]
            );
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex,
                "JSON error mapping entry {Version} for table {Table}",
                entry.Version,
                entry.TableName
            );
            return new MappingFailed(new SyncErrorDatabase($"JSON mapping error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Applies a multi-target mapping to an entry.
    /// Per spec Section 7.3 order-split example.
    /// </summary>
    private static MappingResult ApplyMultiTargetMapping(
        SyncLogEntry entry,
        TableMapping mapping,
        ILogger logger
    )
    {
        if (mapping.Targets is null || mapping.Targets.Count == 0)
        {
            return new MappingFailed(
                new SyncErrorDatabase($"Multi-target mapping {mapping.Id} has no targets")
            );
        }

        try
        {
            var entries = new List<MappedEntry>();

            foreach (var target in mapping.Targets)
            {
                var targetPk = MapPrimaryKey(entry.PkValue, mapping.PkMapping);
                var mappedPayload = MapPayloadForTarget(entry.Payload, target, logger);

                entries.Add(new MappedEntry(target.Table, targetPk, mappedPayload, mapping.Id));
            }

            return new MappingSuccess(entries);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON error in multi-target mapping {Id}", mapping.Id);
            return new MappingFailed(new SyncErrorDatabase($"JSON mapping error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Maps a primary key value using PK mapping configuration.
    /// </summary>
    /// <param name="sourcePk">Source PK JSON, e.g. {"Id": "uuid"}.</param>
    /// <param name="pkMapping">PK mapping configuration.</param>
    /// <returns>Mapped PK JSON.</returns>
    public static string MapPrimaryKey(string sourcePk, PkMapping? pkMapping)
    {
        if (pkMapping is null)
        {
            return sourcePk;
        }

        using var doc = JsonDocument.Parse(sourcePk);
        var root = doc.RootElement;

        if (root.TryGetProperty(pkMapping.SourceColumn, out var value))
        {
            return JsonSerializer.Serialize(
                new Dictionary<string, JsonElement> { [pkMapping.TargetColumn] = value.Clone() }
            );
        }

        return sourcePk;
    }

    /// <summary>
    /// Maps payload using column mappings.
    /// </summary>
    private static string? MapPayload(string? payload, TableMapping mapping, ILogger logger)
    {
        if (payload is null)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(payload);
        var source = doc.RootElement;
        var result = new Dictionary<string, object?>();

        if (mapping.ColumnMappings.Count == 0)
        {
            return ApplyExclusionsAndPassthrough(source, mapping.ExcludedColumns);
        }

        foreach (var colMap in mapping.ColumnMappings)
        {
            var value = GetMappedValue(source, colMap, logger);
            if (value is not null || colMap.Source is null)
            {
                result[colMap.Target] = value;
            }
        }

        return JsonSerializer.Serialize(result);
    }

    /// <summary>
    /// Maps payload for a specific target in multi-target mapping.
    /// </summary>
    private static string? MapPayloadForTarget(string? payload, TargetConfig target, ILogger logger)
    {
        if (payload is null)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(payload);
        var source = doc.RootElement;
        var result = new Dictionary<string, object?>();

        foreach (var colMap in target.ColumnMappings)
        {
            var value = GetMappedValue(source, colMap, logger);
            if (value is not null || colMap.Source is null)
            {
                result[colMap.Target] = value;
            }
        }

        return JsonSerializer.Serialize(result);
    }

    /// <summary>
    /// Gets the mapped value for a column mapping.
    /// </summary>
    private static object? GetMappedValue(
        JsonElement source,
        ColumnMapping colMap,
        ILogger logger
    ) =>
        colMap.Transform switch
        {
            TransformType.Constant => colMap.Value,
            TransformType.Lql => ApplyLqlTransform(source, colMap, logger),
            _ when colMap.Source is not null => source.TryGetProperty(colMap.Source, out var prop)
                ? JsonElementToObject(prop)
                : null,
            _ => null,
        };

    /// <summary>
    /// Applies LQL transformation to a value.
    /// Uses LqlExpressionEvaluator for simple transforms.
    /// </summary>
    private static object? ApplyLqlTransform(
        JsonElement source,
        ColumnMapping colMap,
        ILogger logger
    )
    {
        if (string.IsNullOrWhiteSpace(colMap.Lql))
        {
            logger.LogWarning(
                "LQL transform has empty expression for column {Target}",
                colMap.Target
            );
            if (colMap.Source is not null && source.TryGetProperty(colMap.Source, out var prop))
            {
                return JsonElementToObject(prop);
            }
            return null;
        }

        try
        {
            var result = LqlExpressionEvaluator.Evaluate(colMap.Lql, source);
            logger.LogDebug("LQL transform '{Lql}' evaluated to '{Result}'", colMap.Lql, result);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "LQL transform failed for expression '{Lql}', falling back to source column",
                colMap.Lql
            );

            if (colMap.Source is not null && source.TryGetProperty(colMap.Source, out var prop))
            {
                return JsonElementToObject(prop);
            }

            return null;
        }
    }

    /// <summary>
    /// Applies exclusions and returns passthrough payload.
    /// </summary>
    private static string ApplyExclusionsAndPassthrough(
        JsonElement source,
        IReadOnlyList<string> excludedColumns
    )
    {
        var result = new Dictionary<string, object?>();
        var exclusionSet = excludedColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in source.EnumerateObject())
        {
            if (!exclusionSet.Contains(prop.Name))
            {
                result[prop.Name] = JsonElementToObject(prop.Value);
            }
        }

        return JsonSerializer.Serialize(result);
    }

    /// <summary>
    /// Creates an identity-mapped entry (passthrough).
    /// </summary>
    private static MappedEntry CreateIdentityMappedEntry(SyncLogEntry entry) =>
        new(
            TargetTable: entry.TableName,
            TargetPkValue: entry.PkValue,
            MappedPayload: entry.Payload,
            MappingId: $"identity-{entry.TableName}"
        );

    /// <summary>
    /// Converts a JsonElement to a .NET object.
    /// </summary>
    private static object? JsonElementToObject(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.Object => element
                .EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
            _ => element.GetRawText(),
        };
}
