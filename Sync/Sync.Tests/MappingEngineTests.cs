using Microsoft.Extensions.Logging.Abstractions;

namespace Sync.Tests;

/// <summary>
/// Tests for MappingEngine - data transformation during sync.
/// Covers spec Section 7 - Data Mapping.
/// </summary>
public sealed class MappingEngineTests
{
    private readonly NullLogger<MappingEngineTests> _logger = new();

    #region FindMapping Tests

    [Fact]
    public void FindMapping_ExactMatch_ReturnsMapping()
    {
        var mapping = CreateTestMapping(
            "user-to-customer",
            "User",
            "Customer",
            MappingDirection.Push
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Push);

        Assert.NotNull(found);
        Assert.Equal("user-to-customer", found.Id);
    }

    [Fact]
    public void FindMapping_BothDirection_MatchesPush()
    {
        var mapping = CreateTestMapping("user-both", "User", "Customer", MappingDirection.Both);
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Push);

        Assert.NotNull(found);
        Assert.Equal("user-both", found.Id);
    }

    [Fact]
    public void FindMapping_BothDirection_MatchesPull()
    {
        var mapping = CreateTestMapping("user-both", "User", "Customer", MappingDirection.Both);
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Pull);

        Assert.NotNull(found);
    }

    [Fact]
    public void FindMapping_WrongDirection_ReturnsNull()
    {
        var mapping = CreateTestMapping("user-push", "User", "Customer", MappingDirection.Push);
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Pull);

        Assert.Null(found);
    }

    [Fact]
    public void FindMapping_UnmappedTable_ReturnsNull()
    {
        var mapping = CreateTestMapping(
            "order-mapping",
            "Order",
            "OrderSummary",
            MappingDirection.Push
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var found = MappingEngine.FindMapping("User", config, MappingDirection.Push);

        Assert.Null(found);
    }

    #endregion

    #region ApplyMapping Tests - Passthrough

    [Fact]
    public void ApplyMapping_PassthroughMode_NoMapping_ReturnsIdentity()
    {
        var config = SyncMappingConfig.Passthrough;
        var entry = CreateEntry("Person", """{"Id":"p1"}""", """{"Id":"p1","Name":"Alice"}""");

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Single(success.Entries);
        Assert.Equal("Person", success.Entries[0].TargetTable);
        Assert.Equal("""{"Id":"p1"}""", success.Entries[0].TargetPkValue);
    }

    [Fact]
    public void ApplyMapping_StrictMode_NoMapping_ReturnsSkipped()
    {
        var config = SyncMappingConfig.Empty;
        var entry = CreateEntry("Person", """{"Id":"p1"}""", """{"Id":"p1","Name":"Alice"}""");

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var skipped = Assert.IsType<MappingSkipped>(result);
        Assert.Contains("Person", skipped.Reason);
    }

    #endregion

    #region ApplyMapping Tests - Single Target

    [Fact]
    public void ApplyMapping_SingleTarget_RenamesTable()
    {
        var mapping = CreateTestMapping(
            "user-to-customer",
            "User",
            "Customer",
            MappingDirection.Push
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);
        var entry = CreateEntry("User", """{"Id":"u1"}""", """{"Id":"u1","Name":"Alice"}""");

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Single(success.Entries);
        Assert.Equal("Customer", success.Entries[0].TargetTable);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_MapsPrimaryKey()
    {
        var mapping = new TableMapping(
            Id: "user-mapping",
            SourceTable: "User",
            TargetTable: "Customer",
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: new PkMapping("Id", "CustomerId"),
            ColumnMappings: [],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);
        var entry = CreateEntry("User", """{"Id":"u1"}""", """{"Id":"u1","Name":"Alice"}""");

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Contains("CustomerId", success.Entries[0].TargetPkValue);
        Assert.Contains("u1", success.Entries[0].TargetPkValue);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_MapsColumns()
    {
        var columnMappings = new List<ColumnMapping>
        {
            new("FullName", "Name"),
            new("EmailAddress", "Email"),
        };

        var mapping = new TableMapping(
            Id: "user-mapping",
            SourceTable: "User",
            TargetTable: "Customer",
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: columnMappings,
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);
        var entry = CreateEntry(
            "User",
            """{"Id":"u1"}""",
            """{"Id":"u1","FullName":"Alice","EmailAddress":"alice@test.com"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Contains("Name", success.Entries[0].MappedPayload);
        Assert.Contains("Alice", success.Entries[0].MappedPayload);
        Assert.Contains("Email", success.Entries[0].MappedPayload);
        Assert.Contains("alice@test.com", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_ConstantTransform()
    {
        var columnMappings = new List<ColumnMapping>
        {
            new("Name", "Name"),
            new(null, "Source", TransformType.Constant, "mobile-app"),
        };

        var mapping = new TableMapping(
            Id: "user-mapping",
            SourceTable: "User",
            TargetTable: "Customer",
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: columnMappings,
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);
        var entry = CreateEntry("User", """{"Id":"u1"}""", """{"Id":"u1","Name":"Alice"}""");

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Contains("Source", success.Entries[0].MappedPayload);
        Assert.Contains("mobile-app", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void ApplyMapping_SingleTarget_ExcludesColumns()
    {
        var mapping = new TableMapping(
            Id: "user-mapping",
            SourceTable: "User",
            TargetTable: "Customer",
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: [],
            ExcludedColumns: ["PasswordHash", "SecurityStamp"],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);
        var entry = CreateEntry(
            "User",
            """{"Id":"u1"}""",
            """{"Id":"u1","Name":"Alice","PasswordHash":"secret","SecurityStamp":"xyz"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Contains("Name", success.Entries[0].MappedPayload);
        Assert.DoesNotContain("PasswordHash", success.Entries[0].MappedPayload);
        Assert.DoesNotContain("SecurityStamp", success.Entries[0].MappedPayload);
    }

    [Fact]
    public void ApplyMapping_DisabledMapping_ReturnsSkipped()
    {
        var mapping = new TableMapping(
            Id: "disabled-mapping",
            SourceTable: "User",
            TargetTable: "Customer",
            Direction: MappingDirection.Push,
            Enabled: false,
            PkMapping: null,
            ColumnMappings: [],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);
        var entry = CreateEntry("User", """{"Id":"u1"}""", """{"Id":"u1"}""");

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var skipped = Assert.IsType<MappingSkipped>(result);
        Assert.Contains("disabled", skipped.Reason);
    }

    #endregion

    #region ApplyMapping Tests - Multi-Target

    [Fact]
    public void ApplyMapping_MultiTarget_ProducesMultipleEntries()
    {
        var targets = new List<TargetConfig>
        {
            new(
                "OrderHeader",
                [new ColumnMapping("Id", "OrderId"), new ColumnMapping("Total", "Amount")]
            ),
            new(
                "OrderAudit",
                [
                    new ColumnMapping("Id", "OrderId"),
                    new ColumnMapping(null, "EventType", TransformType.Constant, "created"),
                ]
            ),
        };

        var mapping = new TableMapping(
            Id: "order-split",
            SourceTable: "Order",
            TargetTable: null,
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: [],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig(),
            IsMultiTarget: true,
            Targets: targets
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);
        var entry = CreateEntry(
            "Order",
            """{"Id":"o1"}""",
            """{"Id":"o1","Total":99.99,"CustomerId":"c1"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Equal(2, success.Entries.Count);
        Assert.Contains(success.Entries, e => e.TargetTable == "OrderHeader");
        Assert.Contains(success.Entries, e => e.TargetTable == "OrderAudit");
    }

    [Fact]
    public void ApplyMapping_MultiTarget_EachTargetHasCorrectPayload()
    {
        var targets = new List<TargetConfig>
        {
            new("OrderHeader", [new ColumnMapping("Total", "Amount")]),
            new(
                "OrderAudit",
                [new ColumnMapping(null, "EventType", TransformType.Constant, "created")]
            ),
        };

        var mapping = new TableMapping(
            Id: "order-split",
            SourceTable: "Order",
            TargetTable: null,
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: [],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig(),
            IsMultiTarget: true,
            Targets: targets
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);
        var entry = CreateEntry("Order", """{"Id":"o1"}""", """{"Id":"o1","Total":99.99}""");

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        var header = success.Entries.First(e => e.TargetTable == "OrderHeader");
        var audit = success.Entries.First(e => e.TargetTable == "OrderAudit");

        Assert.Contains("Amount", header.MappedPayload);
        Assert.Contains("EventType", audit.MappedPayload);
        Assert.Contains("created", audit.MappedPayload);
    }

    #endregion

    #region MapPrimaryKey Tests

    [Fact]
    public void MapPrimaryKey_NullMapping_ReturnsSamePk()
    {
        var pk = """{"Id":"test-id"}""";

        var result = MappingEngine.MapPrimaryKey(pk, null);

        Assert.Equal(pk, result);
    }

    [Fact]
    public void MapPrimaryKey_WithMapping_RenamesColumn()
    {
        var pk = """{"Id":"test-id"}""";
        var mapping = new PkMapping("Id", "CustomerId");

        var result = MappingEngine.MapPrimaryKey(pk, mapping);

        Assert.Contains("CustomerId", result);
        Assert.Contains("test-id", result);
        Assert.DoesNotContain(":\"Id\"", result);
    }

    [Fact]
    public void MapPrimaryKey_SourceNotFound_ReturnsSamePk()
    {
        var pk = """{"OtherId":"test-id"}""";
        var mapping = new PkMapping("Id", "CustomerId");

        var result = MappingEngine.MapPrimaryKey(pk, mapping);

        Assert.Equal(pk, result);
    }

    #endregion

    #region Delete Operation Tests

    [Fact]
    public void ApplyMapping_DeleteOperation_NullPayload_Succeeds()
    {
        var mapping = CreateTestMapping("user-mapping", "User", "Customer", MappingDirection.Push);
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);
        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "User",
            PkValue: """{"Id":"u1"}""",
            Operation: SyncOperation.Delete,
            Payload: null,
            Origin: "origin-1",
            Timestamp: "2024-01-01T00:00:00Z"
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Null(success.Entries[0].MappedPayload);
    }

    #endregion

    #region Helper Methods

    private static TableMapping CreateTestMapping(
        string id,
        string sourceTable,
        string targetTable,
        MappingDirection direction
    ) =>
        new(
            Id: id,
            SourceTable: sourceTable,
            TargetTable: targetTable,
            Direction: direction,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: [],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );

    private static SyncLogEntry CreateEntry(string table, string pk, string? payload) =>
        new(
            Version: 1,
            TableName: table,
            PkValue: pk,
            Operation: SyncOperation.Insert,
            Payload: payload,
            Origin: "test-origin",
            Timestamp: "2024-01-01T00:00:00Z"
        );

    #endregion
}
