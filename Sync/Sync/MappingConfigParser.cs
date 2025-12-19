using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Sync;

/// <summary>
/// Parses JSON mapping configuration files.
/// Implements spec Section 7.3 - Mapping Configuration Schema.
/// </summary>
public static class MappingConfigParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <summary>
    /// Parses mapping configuration from JSON string.
    /// </summary>
    /// <param name="json">JSON configuration string.</param>
    /// <param name="logger">Logger for parse errors.</param>
    /// <returns>Parsed config or error.</returns>
    public static MappingConfigParseResult Parse(string json, ILogger logger)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<MappingConfigDto>(json, JsonOptions);

            if (dto is null)
            {
                return new MappingConfigParseError("JSON deserialized to null");
            }

            var mappings =
                dto.Mappings?.Select(m => ConvertMapping(m, logger))
                    .Where(m => m is not null)
                    .Cast<TableMapping>()
                    .ToList() ?? [];

            var unmappedBehavior = dto.UnmappedTableBehavior?.ToLowerInvariant() switch
            {
                "passthrough" => UnmappedTableBehavior.Passthrough,
                _ => UnmappedTableBehavior.Strict,
            };

            var config = new SyncMappingConfig(
                Version: dto.Version ?? "1.0",
                UnmappedTableBehavior: unmappedBehavior,
                Mappings: mappings
            );

            logger.LogInformation(
                "MAPPING: Parsed config v{Version} with {Count} mappings",
                config.Version,
                config.Mappings.Count
            );

            return new MappingConfigParseOk(config);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "MAPPING: Failed to parse JSON config");
            return new MappingConfigParseError($"JSON parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts DTO to domain model.
    /// </summary>
    private static TableMapping? ConvertMapping(TableMappingDto dto, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.SourceTable))
        {
            logger.LogWarning("MAPPING: Skipping invalid mapping - missing id or source_table");
            return null;
        }

        var direction = dto.Direction?.ToLowerInvariant() switch
        {
            "pull" => MappingDirection.Pull,
            "both" => MappingDirection.Both,
            _ => MappingDirection.Push,
        };

        var pkMapping = dto.PkMapping is not null
            ? new PkMapping(dto.PkMapping.SourceColumn, dto.PkMapping.TargetColumn)
            : null;

        var columnMappings =
            dto.ColumnMappings?.Select(ConvertColumnMapping)
                .Where(c => c is not null)
                .Cast<ColumnMapping>()
                .ToList() ?? [];

        var excludedColumns = dto.ExcludedColumns ?? [];

        var filter = !string.IsNullOrWhiteSpace(dto.Filter?.Lql)
            ? new SyncFilter(dto.Filter.Lql)
            : null;

        var trackingStrategy = dto.SyncTracking?.Strategy?.ToLowerInvariant() switch
        {
            "hash" => SyncTrackingStrategy.Hash,
            "timestamp" => SyncTrackingStrategy.Timestamp,
            "external" => SyncTrackingStrategy.External,
            _ => SyncTrackingStrategy.Version,
        };

        var syncTracking = new SyncTrackingConfig(
            Enabled: dto.SyncTracking?.Enabled ?? true,
            Strategy: trackingStrategy,
            TrackingColumn: dto.SyncTracking?.TrackingColumn
        );

        var targets = dto
            .Targets?.Select(ConvertTarget)
            .Where(t => t is not null)
            .Cast<TargetConfig>()
            .ToList();

        return new TableMapping(
            Id: dto.Id,
            SourceTable: dto.SourceTable,
            TargetTable: dto.TargetTable,
            Direction: direction,
            Enabled: dto.Enabled ?? true,
            PkMapping: pkMapping,
            ColumnMappings: columnMappings,
            ExcludedColumns: excludedColumns,
            Filter: filter,
            SyncTracking: syncTracking,
            IsMultiTarget: dto.MultiTarget ?? false,
            Targets: targets
        );
    }

    /// <summary>
    /// Converts column mapping DTO.
    /// </summary>
    private static ColumnMapping? ConvertColumnMapping(ColumnMappingDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Target))
        {
            return null;
        }

        var transform = dto.Transform?.ToLowerInvariant() switch
        {
            "constant" => TransformType.Constant,
            "lql" => TransformType.Lql,
            _ => TransformType.None,
        };

        return new ColumnMapping(
            Source: dto.Source,
            Target: dto.Target,
            Transform: transform,
            Value: dto.Value,
            Lql: dto.Lql
        );
    }

    /// <summary>
    /// Converts target config DTO.
    /// </summary>
    private static TargetConfig? ConvertTarget(TargetDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Table))
        {
            return null;
        }

        var columns =
            dto.ColumnMappings?.Select(ConvertColumnMapping)
                .Where(c => c is not null)
                .Cast<ColumnMapping>()
                .ToList() ?? [];

        return new TargetConfig(dto.Table, columns);
    }

    /// <summary>
    /// Serializes a mapping config to JSON.
    /// </summary>
    /// <param name="config">Config to serialize.</param>
    /// <returns>JSON string.</returns>
    public static string ToJson(SyncMappingConfig config)
    {
        var dto = new MappingConfigDto
        {
            Version = config.Version,
            UnmappedTableBehavior = config.UnmappedTableBehavior.ToString().ToLowerInvariant(),
            Mappings = [.. config.Mappings.Select(ToDto)],
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    /// Converts domain model to DTO for serialization.
    /// </summary>
    private static TableMappingDto ToDto(TableMapping m) =>
        new()
        {
            Id = m.Id,
            SourceTable = m.SourceTable,
            TargetTable = m.TargetTable,
            Direction = m.Direction.ToString().ToLowerInvariant(),
            Enabled = m.Enabled,
            MultiTarget = m.IsMultiTarget,
            PkMapping = m.PkMapping is not null
                ? new PkMappingDto
                {
                    SourceColumn = m.PkMapping.SourceColumn,
                    TargetColumn = m.PkMapping.TargetColumn,
                }
                : null,
            ColumnMappings =
            [
                .. m.ColumnMappings.Select(c => new ColumnMappingDto
                {
                    Source = c.Source,
                    Target = c.Target,
                    Transform =
                        c.Transform == TransformType.None
                            ? null
                            : c.Transform.ToString().ToLowerInvariant(),
                    Value = c.Value,
                    Lql = c.Lql,
                }),
            ],
            ExcludedColumns = [.. m.ExcludedColumns],
            Filter = m.Filter is not null ? new FilterDto { Lql = m.Filter.Lql } : null,
            SyncTracking = new SyncTrackingDto
            {
                Enabled = m.SyncTracking.Enabled,
                Strategy = m.SyncTracking.Strategy.ToString().ToLowerInvariant(),
                TrackingColumn = m.SyncTracking.TrackingColumn,
            },
            Targets = m
                .Targets?.Select(t => new TargetDto
                {
                    Table = t.Table,
                    ColumnMappings =
                    [
                        .. t.ColumnMappings.Select(c => new ColumnMappingDto
                        {
                            Source = c.Source,
                            Target = c.Target,
                            Transform =
                                c.Transform == TransformType.None
                                    ? null
                                    : c.Transform.ToString().ToLowerInvariant(),
                            Value = c.Value,
                            Lql = c.Lql,
                        }),
                    ],
                })
                .ToList(),
        };

    // ===== DTOs for JSON serialization =====

    private sealed class MappingConfigDto
    {
        public string? Version { get; set; }

        [JsonPropertyName("unmapped_table_behavior")]
        public string? UnmappedTableBehavior { get; set; }

        public List<TableMappingDto>? Mappings { get; set; }
    }

    private sealed class TableMappingDto
    {
        public string Id { get; set; } = "";

        [JsonPropertyName("source_table")]
        public string SourceTable { get; set; } = "";

        [JsonPropertyName("target_table")]
        public string? TargetTable { get; set; }

        public string? Direction { get; set; }
        public bool? Enabled { get; set; }

        [JsonPropertyName("multi_target")]
        public bool? MultiTarget { get; set; }

        [JsonPropertyName("pk_mapping")]
        public PkMappingDto? PkMapping { get; set; }

        [JsonPropertyName("column_mappings")]
        public List<ColumnMappingDto>? ColumnMappings { get; set; }

        [JsonPropertyName("excluded_columns")]
        public List<string>? ExcludedColumns { get; set; }

        public FilterDto? Filter { get; set; }

        [JsonPropertyName("sync_tracking")]
        public SyncTrackingDto? SyncTracking { get; set; }

        public List<TargetDto>? Targets { get; set; }
    }

    private sealed class PkMappingDto
    {
        [JsonPropertyName("source_column")]
        public string SourceColumn { get; set; } = "";

        [JsonPropertyName("target_column")]
        public string TargetColumn { get; set; } = "";
    }

    private sealed class ColumnMappingDto
    {
        public string? Source { get; set; }
        public string Target { get; set; } = "";
        public string? Transform { get; set; }
        public string? Value { get; set; }
        public string? Lql { get; set; }
    }

    private sealed class FilterDto
    {
        public string Lql { get; set; } = "";
    }

    private sealed class SyncTrackingDto
    {
        public bool Enabled { get; set; } = true;
        public string? Strategy { get; set; }

        [JsonPropertyName("tracking_column")]
        public string? TrackingColumn { get; set; }
    }

    private sealed class TargetDto
    {
        public string Table { get; set; } = "";

        [JsonPropertyName("column_mappings")]
        public List<ColumnMappingDto>? ColumnMappings { get; set; }
    }
}

/// <summary>
/// Result type for mapping config parsing.
/// </summary>
public abstract record MappingConfigParseResult;

/// <summary>
/// Successful parse result.
/// </summary>
/// <param name="Config">Parsed configuration.</param>
public sealed record MappingConfigParseOk(SyncMappingConfig Config) : MappingConfigParseResult;

/// <summary>
/// Failed parse result.
/// </summary>
/// <param name="Error">Error message.</param>
public sealed record MappingConfigParseError(string Error) : MappingConfigParseResult;
