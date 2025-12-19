using Microsoft.Extensions.Logging.Abstractions;

namespace Sync.Tests;

/// <summary>
/// Tests for MappingConfigParser - JSON configuration parsing.
/// Covers spec Section 7.3 - Mapping Configuration Schema.
/// </summary>
public sealed class MappingConfigParserTests
{
    private readonly NullLogger<MappingConfigParserTests> _logger = new();

    #region Parse Tests

    [Fact]
    public void Parse_EmptyConfig_ReturnsEmptyMappings()
    {
        var json = """{"version": "1.0", "mappings": []}""";

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        Assert.Empty(success.Config.Mappings);
        Assert.Equal("1.0", success.Config.Version);
    }

    [Fact]
    public void Parse_SimpleMapping_ParsesCorrectly()
    {
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {
                        "id": "user-mapping",
                        "source_table": "User",
                        "target_table": "Customer",
                        "direction": "push",
                        "enabled": true
                    }
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        Assert.Single(success.Config.Mappings);
        var mapping = success.Config.Mappings[0];
        Assert.Equal("user-mapping", mapping.Id);
        Assert.Equal("User", mapping.SourceTable);
        Assert.Equal("Customer", mapping.TargetTable);
        Assert.Equal(MappingDirection.Push, mapping.Direction);
        Assert.True(mapping.Enabled);
    }

    [Fact]
    public void Parse_PkMapping_ParsesCorrectly()
    {
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {
                        "id": "user-mapping",
                        "source_table": "User",
                        "target_table": "Customer",
                        "direction": "push",
                        "pk_mapping": {
                            "source_column": "Id",
                            "target_column": "CustomerId"
                        }
                    }
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        var mapping = success.Config.Mappings[0];
        Assert.NotNull(mapping.PkMapping);
        Assert.Equal("Id", mapping.PkMapping.SourceColumn);
        Assert.Equal("CustomerId", mapping.PkMapping.TargetColumn);
    }

    [Fact]
    public void Parse_ColumnMappings_ParsesCorrectly()
    {
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {
                        "id": "user-mapping",
                        "source_table": "User",
                        "target_table": "Customer",
                        "direction": "push",
                        "column_mappings": [
                            {"source": "FullName", "target": "Name"},
                            {"source": "EmailAddress", "target": "Email"}
                        ]
                    }
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        var mapping = success.Config.Mappings[0];
        Assert.Equal(2, mapping.ColumnMappings.Count);
        Assert.Equal("FullName", mapping.ColumnMappings[0].Source);
        Assert.Equal("Name", mapping.ColumnMappings[0].Target);
    }

    [Fact]
    public void Parse_ConstantTransform_ParsesCorrectly()
    {
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {
                        "id": "user-mapping",
                        "source_table": "User",
                        "target_table": "Customer",
                        "direction": "push",
                        "column_mappings": [
                            {"source": null, "target": "Source", "transform": "constant", "value": "mobile-app"}
                        ]
                    }
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        var colMapping = success.Config.Mappings[0].ColumnMappings[0];
        Assert.Null(colMapping.Source);
        Assert.Equal("Source", colMapping.Target);
        Assert.Equal(TransformType.Constant, colMapping.Transform);
        Assert.Equal("mobile-app", colMapping.Value);
    }

    [Fact]
    public void Parse_LqlTransform_ParsesCorrectly()
    {
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {
                        "id": "user-mapping",
                        "source_table": "User",
                        "target_table": "Customer",
                        "direction": "push",
                        "column_mappings": [
                            {"source": "CreatedAt", "target": "RegisteredDate", "transform": "lql", "lql": "CreatedAt |> dateFormat('yyyy-MM-dd')"}
                        ]
                    }
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        var colMapping = success.Config.Mappings[0].ColumnMappings[0];
        Assert.Equal(TransformType.Lql, colMapping.Transform);
        Assert.Contains("dateFormat", colMapping.Lql);
    }

    [Fact]
    public void Parse_ExcludedColumns_ParsesCorrectly()
    {
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {
                        "id": "user-mapping",
                        "source_table": "User",
                        "target_table": "Customer",
                        "direction": "push",
                        "excluded_columns": ["PasswordHash", "SecurityStamp"]
                    }
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        var excluded = success.Config.Mappings[0].ExcludedColumns;
        Assert.Equal(2, excluded.Count);
        Assert.Contains("PasswordHash", excluded);
        Assert.Contains("SecurityStamp", excluded);
    }

    [Fact]
    public void Parse_Filter_ParsesCorrectly()
    {
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {
                        "id": "user-mapping",
                        "source_table": "User",
                        "target_table": "Customer",
                        "direction": "push",
                        "filter": {"lql": "IsActive = true AND DeletedAt IS NULL"}
                    }
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        var filter = success.Config.Mappings[0].Filter;
        Assert.NotNull(filter);
        Assert.Contains("IsActive", filter.Lql);
    }

    [Fact]
    public void Parse_SyncTracking_ParsesCorrectly()
    {
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {
                        "id": "user-mapping",
                        "source_table": "User",
                        "target_table": "Customer",
                        "direction": "push",
                        "sync_tracking": {
                            "enabled": true,
                            "strategy": "hash",
                            "tracking_column": "_synced_version"
                        }
                    }
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        var tracking = success.Config.Mappings[0].SyncTracking;
        Assert.True(tracking.Enabled);
        Assert.Equal(SyncTrackingStrategy.Hash, tracking.Strategy);
        Assert.Equal("_synced_version", tracking.TrackingColumn);
    }

    [Fact]
    public void Parse_MultiTarget_ParsesCorrectly()
    {
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {
                        "id": "order-split",
                        "source_table": "Order",
                        "direction": "push",
                        "multi_target": true,
                        "targets": [
                            {
                                "table": "OrderHeader",
                                "column_mappings": [
                                    {"source": "Id", "target": "OrderId"},
                                    {"source": "Total", "target": "Amount"}
                                ]
                            },
                            {
                                "table": "OrderAudit",
                                "column_mappings": [
                                    {"source": "Id", "target": "OrderId"},
                                    {"source": null, "target": "EventType", "transform": "constant", "value": "created"}
                                ]
                            }
                        ]
                    }
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        var mapping = success.Config.Mappings[0];
        Assert.True(mapping.IsMultiTarget);
        Assert.NotNull(mapping.Targets);
        Assert.Equal(2, mapping.Targets.Count);
        Assert.Equal("OrderHeader", mapping.Targets[0].Table);
        Assert.Equal("OrderAudit", mapping.Targets[1].Table);
    }

    [Fact]
    public void Parse_DirectionPull_ParsesCorrectly()
    {
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {
                        "id": "pull-mapping",
                        "source_table": "Customer",
                        "target_table": "User",
                        "direction": "pull"
                    }
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        Assert.Equal(MappingDirection.Pull, success.Config.Mappings[0].Direction);
    }

    [Fact]
    public void Parse_DirectionBoth_ParsesCorrectly()
    {
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {
                        "id": "bi-dir-mapping",
                        "source_table": "User",
                        "target_table": "Customer",
                        "direction": "both"
                    }
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        Assert.Equal(MappingDirection.Both, success.Config.Mappings[0].Direction);
    }

    [Fact]
    public void Parse_UnmappedTableBehaviorPassthrough_ParsesCorrectly()
    {
        var json = """
            {
                "version": "1.0",
                "unmapped_table_behavior": "passthrough",
                "mappings": []
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        Assert.Equal(UnmappedTableBehavior.Passthrough, success.Config.UnmappedTableBehavior);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsError()
    {
        var json = """{"this is not valid json""";

        var result = MappingConfigParser.Parse(json, _logger);

        var error = Assert.IsType<MappingConfigParseError>(result);
        Assert.Contains("JSON", error.Error);
    }

    [Fact]
    public void Parse_MissingRequiredFields_SkipsInvalidMapping()
    {
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {"id": "", "source_table": ""},
                    {"id": "valid", "source_table": "User", "target_table": "Customer", "direction": "push"}
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        Assert.Single(success.Config.Mappings);
        Assert.Equal("valid", success.Config.Mappings[0].Id);
    }

    #endregion

    #region ToJson Tests

    [Fact]
    public void ToJson_EmptyConfig_ProducesValidJson()
    {
        var config = SyncMappingConfig.Empty;

        var json = MappingConfigParser.ToJson(config);

        // JSON keys may be PascalCase or camelCase depending on serializer settings
        Assert.True(
            json.Contains("\"version\"", StringComparison.OrdinalIgnoreCase)
                || json.Contains("\"Version\"", StringComparison.OrdinalIgnoreCase)
        );
        Assert.True(
            json.Contains("\"mappings\"", StringComparison.OrdinalIgnoreCase)
                || json.Contains("\"Mappings\"", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void ToJson_RoundTrip_PreservesData()
    {
        var originalJson = """
            {
                "version": "1.0",
                "unmapped_table_behavior": "strict",
                "mappings": [
                    {
                        "id": "test-mapping",
                        "source_table": "User",
                        "target_table": "Customer",
                        "direction": "push",
                        "enabled": true,
                        "column_mappings": [
                            {"source": "Name", "target": "FullName"}
                        ]
                    }
                ]
            }
            """;

        var parseResult = MappingConfigParser.Parse(originalJson, _logger);
        var config = ((MappingConfigParseOk)parseResult).Config;
        var serialized = MappingConfigParser.ToJson(config);
        var reparsed = MappingConfigParser.Parse(serialized, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(reparsed);
        Assert.Equal("1.0", success.Config.Version);
        Assert.Single(success.Config.Mappings);
        Assert.Equal("test-mapping", success.Config.Mappings[0].Id);
        Assert.Equal("User", success.Config.Mappings[0].SourceTable);
    }

    #endregion

    #region Full Spec Example

    [Fact]
    public void Parse_FullSpecExample_ParsesCorrectly()
    {
        // This is the example from spec Section 7.3
        var json = """
            {
                "version": "1.0",
                "mappings": [
                    {
                        "id": "user-to-customer",
                        "source_table": "User",
                        "target_table": "Customer",
                        "direction": "push",
                        "enabled": true,
                        "pk_mapping": {
                            "source_column": "Id",
                            "target_column": "CustomerId"
                        },
                        "column_mappings": [
                            {
                                "source": "FullName",
                                "target": "Name",
                                "transform": null
                            },
                            {
                                "source": "EmailAddress",
                                "target": "Email",
                                "transform": null
                            },
                            {
                                "source": null,
                                "target": "Source",
                                "transform": "constant",
                                "value": "mobile-app"
                            },
                            {
                                "source": "CreatedAt",
                                "target": "RegisteredDate",
                                "transform": "lql",
                                "lql": "CreatedAt |> dateFormat('yyyy-MM-dd')"
                            }
                        ],
                        "excluded_columns": ["PasswordHash", "SecurityStamp"],
                        "filter": {
                            "lql": "IsActive = true AND DeletedAt IS NULL"
                        },
                        "sync_tracking": {
                            "enabled": true,
                            "tracking_column": "_synced_version",
                            "strategy": "version"
                        }
                    }
                ]
            }
            """;

        var result = MappingConfigParser.Parse(json, _logger);

        var success = Assert.IsType<MappingConfigParseOk>(result);
        var mapping = success.Config.Mappings[0];

        Assert.Equal("user-to-customer", mapping.Id);
        Assert.Equal("User", mapping.SourceTable);
        Assert.Equal("Customer", mapping.TargetTable);
        Assert.Equal(MappingDirection.Push, mapping.Direction);
        Assert.True(mapping.Enabled);
        Assert.NotNull(mapping.PkMapping);
        Assert.Equal("Id", mapping.PkMapping.SourceColumn);
        Assert.Equal("CustomerId", mapping.PkMapping.TargetColumn);
        Assert.Equal(4, mapping.ColumnMappings.Count);
        Assert.Equal(2, mapping.ExcludedColumns.Count);
        Assert.NotNull(mapping.Filter);
        Assert.True(mapping.SyncTracking.Enabled);
        Assert.Equal(SyncTrackingStrategy.Version, mapping.SyncTracking.Strategy);
    }

    #endregion
}
