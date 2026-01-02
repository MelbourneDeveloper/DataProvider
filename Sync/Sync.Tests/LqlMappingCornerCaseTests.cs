using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sync.Tests;

/// <summary>
/// Corner case and edge case tests for LQL mapping.
/// Tests null handling, empty strings, special characters, Unicode, nested data,
/// and unusual transformation scenarios.
/// </summary>
public sealed class LqlMappingCornerCaseTests
{
    private readonly NullLogger<LqlMappingCornerCaseTests> _logger = new();

    #region Null and Empty Value Handling

    [Fact]
    public void LqlTransform_NullStringColumn_ReturnsEmptyString()
    {
        var source = JsonDocument.Parse("""{"Name":null}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("upper(Name)", source);

        Assert.True(string.IsNullOrEmpty(result?.ToString()));
    }

    [Fact]
    public void LqlTransform_EmptyString_ReturnsEmpty()
    {
        var source = JsonDocument.Parse("""{"Name":""}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("upper(Name)", source);

        Assert.Equal("", result);
    }

    [Fact]
    public void Concat_WithNullColumn_ContinuesWithOtherValues()
    {
        var source = JsonDocument
            .Parse("""{"First":"John","Middle":null,"Last":"Doe"}""")
            .RootElement;

        var result = LqlExpressionEvaluator.Evaluate(
            "concat(First, ' ', Middle, ' ', Last)",
            source
        );

        // Should produce "John  Doe" (double space where null was)
        Assert.Contains("John", result?.ToString());
        Assert.Contains("Doe", result?.ToString());
    }

    [Fact]
    public void Coalesce_AllNull_ReturnsEmpty()
    {
        var source = JsonDocument.Parse("""{"A":null,"B":null,"C":""}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("coalesce(A, B, C)", source);

        // Should return empty or first non-null
        Assert.True(result == null || string.IsNullOrEmpty(result.ToString()));
    }

    [Fact]
    public void Coalesce_FirstNonNull_Returned()
    {
        var source = JsonDocument.Parse("""{"A":"","B":"","C":"found"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("coalesce(A, B, C)", source);

        Assert.Equal("found", result);
    }

    #endregion

    #region Special Characters and Unicode

    [Fact]
    public void LqlTransform_UnicodeCharacters_Preserved()
    {
        var source = JsonDocument.Parse("""{"Name":"日本語テスト"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Name", source);

        Assert.Equal("日本語テスト", result);
    }

    [Fact]
    public void Upper_UnicodeString_HandledCorrectly()
    {
        var source = JsonDocument.Parse("""{"Name":"café"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("upper(Name)", source);

        Assert.Equal("CAFÉ", result);
    }

    [Fact]
    public void Lower_UnicodeString_HandledCorrectly()
    {
        var source = JsonDocument.Parse("""{"Name":"MÜNCHEN"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("lower(Name)", source);

        Assert.Equal("münchen", result);
    }

    [Fact]
    public void Concat_WithEmoji_Preserved()
    {
        var source = JsonDocument.Parse("""{"Prefix":"Hello","Suffix":"🎉"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("concat(Prefix, ' ', Suffix)", source);

        Assert.Equal("Hello 🎉", result);
    }

    [Fact]
    public void LqlTransform_StringWithQuotes_Handled()
    {
        var source = JsonDocument.Parse("""{"Text":"Say \"Hello\" to me"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Text", source);

        Assert.Equal("Say \"Hello\" to me", result);
    }

    [Fact]
    public void LqlTransform_StringWithBackslash_Preserved()
    {
        var source = JsonDocument.Parse("""{"Path":"C:\\Users\\Test"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Path", source);

        Assert.Equal("C:\\Users\\Test", result);
    }

    [Fact]
    public void LqlTransform_StringWithNewlines_Preserved()
    {
        var source = JsonDocument.Parse("""{"Text":"Line1\nLine2\nLine3"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Text", source);

        Assert.Contains("\n", result?.ToString());
    }

    #endregion

    #region Numeric Edge Cases

    [Fact]
    public void LqlTransform_IntegerZero_ReturnsZero()
    {
        var source = JsonDocument.Parse("""{"Value":0}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Value", source);

        Assert.Equal(0L, result);
    }

    [Fact]
    public void LqlTransform_NegativeNumber_Preserved()
    {
        var source = JsonDocument.Parse("""{"Value":-123}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Value", source);

        Assert.Equal(-123L, result);
    }

    [Fact]
    public void LqlTransform_LargeInteger_Preserved()
    {
        var source = JsonDocument.Parse("""{"Value":9223372036854775807}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Value", source);

        Assert.Equal(9223372036854775807L, result);
    }

    [Fact]
    public void LqlTransform_FloatingPoint_Preserved()
    {
        var source = JsonDocument.Parse("""{"Value":3.14159265359}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Value", source);

        Assert.Equal(3.14159265359, result);
    }

    [Fact]
    public void LqlTransform_ScientificNotation_Parsed()
    {
        var source = JsonDocument.Parse("""{"Value":1.5e10}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Value", source);

        Assert.Equal(1.5e10, result);
    }

    #endregion

    #region Boolean Edge Cases

    [Fact]
    public void LqlTransform_BooleanTrue_Preserved()
    {
        var source = JsonDocument.Parse("""{"Active":true}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Active", source);

        Assert.Equal(true, result);
    }

    [Fact]
    public void LqlTransform_BooleanFalse_Preserved()
    {
        var source = JsonDocument.Parse("""{"Active":false}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Active", source);

        Assert.Equal(false, result);
    }

    #endregion

    #region String Function Edge Cases

    [Fact]
    public void Substring_StartBeyondLength_ReturnsEmpty()
    {
        var source = JsonDocument.Parse("""{"Text":"Hello"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("substring(Text, 100, 5)", source);

        Assert.Equal("", result);
    }

    [Fact]
    public void Substring_LengthBeyondEnd_ReturnsTruncated()
    {
        var source = JsonDocument.Parse("""{"Text":"Hello"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("substring(Text, 3, 100)", source);

        // Starting at position 3 (1-based), get remaining
        Assert.Equal("llo", result);
    }

    [Fact]
    public void Left_LengthExceedsString_ReturnsFullString()
    {
        var source = JsonDocument.Parse("""{"Text":"Hi"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("left(Text, 100)", source);

        Assert.Equal("Hi", result);
    }

    [Fact]
    public void Right_LengthExceedsString_ReturnsFullString()
    {
        var source = JsonDocument.Parse("""{"Text":"Hi"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("right(Text, 100)", source);

        Assert.Equal("Hi", result);
    }

    [Fact]
    public void Left_ZeroLength_ReturnsEmpty()
    {
        var source = JsonDocument.Parse("""{"Text":"Hello"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("left(Text, 0)", source);

        Assert.Equal("", result);
    }

    [Fact]
    public void Replace_PatternNotFound_ReturnsOriginal()
    {
        var source = JsonDocument.Parse("""{"Text":"Hello World"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("replace(Text, 'NOTFOUND', 'X')", source);

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Replace_MultipleOccurrences_ReplacesAll()
    {
        var source = JsonDocument.Parse("""{"Text":"a-b-c-d"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("replace(Text, '-', '_')", source);

        Assert.Equal("a_b_c_d", result);
    }

    [Fact]
    public void Trim_NoWhitespace_ReturnsOriginal()
    {
        var source = JsonDocument.Parse("""{"Text":"NoSpaces"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("trim(Text)", source);

        Assert.Equal("NoSpaces", result);
    }

    [Fact]
    public void Trim_OnlyWhitespace_ReturnsEmpty()
    {
        var source = JsonDocument.Parse("""{"Text":"   "}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("trim(Text)", source);

        Assert.Equal("", result);
    }

    [Fact]
    public void Length_EmptyString_ReturnsZero()
    {
        var source = JsonDocument.Parse("""{"Text":""}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("length(Text)", source);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Length_UnicodeString_CountsCodePoints()
    {
        var source = JsonDocument.Parse("""{"Text":"日本語"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("length(Text)", source);

        Assert.Equal(3, result);
    }

    #endregion

    #region Date Transform Edge Cases

    [Fact]
    public void DateFormat_InvalidDate_ReturnsOriginal()
    {
        var source = JsonDocument.Parse("""{"Date":"not-a-date"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("dateFormat(Date, 'yyyy-MM-dd')", source);

        Assert.Equal("not-a-date", result);
    }

    [Fact]
    public void DateFormat_EmptyDate_ReturnsEmpty()
    {
        var source = JsonDocument.Parse("""{"Date":""}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("dateFormat(Date, 'yyyy-MM-dd')", source);

        Assert.Equal("", result);
    }

    [Fact]
    public void DateFormat_IsoFormat_Parsed()
    {
        var source = JsonDocument.Parse("""{"Date":"2024-12-25T15:30:00Z"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("dateFormat(Date, 'yyyy-MM-dd')", source);

        Assert.Equal("2024-12-25", result);
    }

    [Fact]
    public void DateFormat_TimeOnly_Extracted()
    {
        var source = JsonDocument.Parse("""{"Date":"2024-06-15T14:30:45Z"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("dateFormat(Date, 'HH:mm:ss')", source);

        Assert.Equal("14:30:45", result);
    }

    #endregion

    #region Pipe Syntax Edge Cases

    [Fact]
    public void Pipe_EmptyInput_HandledGracefully()
    {
        var source = JsonDocument.Parse("""{"Name":""}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Name |> trim() |> upper()", source);

        Assert.Equal("", result);
    }

    [Fact]
    public void Pipe_MultipleFunctions_AllApplied()
    {
        var source = JsonDocument.Parse("""{"Text":"  HeLLo WoRLd  "}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate("Text |> trim() |> lower()", source);

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void Pipe_WithReplaceFunction_Works()
    {
        var source = JsonDocument.Parse("""{"Path":"users/admin/home"}""").RootElement;

        var result = LqlExpressionEvaluator.Evaluate(
            "Path |> replace('/', '_') |> upper()",
            source
        );

        Assert.Equal("USERS_ADMIN_HOME", result);
    }

    #endregion

    #region MappingEngine Corner Cases

    [Fact]
    public void MappingEngine_NullPayload_ReturnsNullMappedPayload()
    {
        var mapping = CreateMappingWithColumns(
            "test",
            "Source",
            "Target",
            [new ColumnMapping("Name", "DisplayName", TransformType.Lql, null, "upper(Name)")]
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var entry = new SyncLogEntry(
            Version: 1,
            TableName: "Source",
            PkValue: """{"Id":"1"}""",
            Operation: SyncOperation.Delete,
            Payload: null, // DELETE operations have null payload
            Origin: "test",
            Timestamp: "2024-01-01T00:00:00Z"
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Null(success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_EmptyColumnMappings_PassthroughPayload()
    {
        var mapping = new TableMapping(
            Id: "passthrough",
            SourceTable: "Source",
            TargetTable: "Target",
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: [], // Empty - should passthrough
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var entry = CreateEntry(
            "Source",
            """{"Id":"1"}""",
            """{"Id":"1","Name":"Test","Value":123}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        var payload = success.Entries[0].MappedPayload!;
        Assert.Contains("Name", payload);
        Assert.Contains("Test", payload);
        Assert.Contains("Value", payload);
    }

    [Fact]
    public void MappingEngine_LqlWithMissingSourceColumn_FallsBack()
    {
        var columnMappings = new List<ColumnMapping>
        {
            new("NonExistent", "DisplayName", TransformType.Lql, null, "upper(NonExistent)"),
        };

        var mapping = CreateMappingWithColumns("test", "Source", "Target", columnMappings);
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var entry = CreateEntry("Source", """{"Id":"1"}""", """{"Id":"1","Name":"Alice"}""");

        // Should not throw, should handle gracefully
        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.NotNull(success.Entries[0].MappedPayload);
    }

    [Fact]
    public void MappingEngine_MixedTransformTypes_AllApplied()
    {
        var columnMappings = new List<ColumnMapping>
        {
            new("Name", "Name"), // Direct mapping
            new("Email", "NormalizedEmail", TransformType.Lql, null, "lower(Email)"), // LQL transform
            new(null, "Source", TransformType.Constant, "mobile-app"), // Constant
            new("Status", "StatusCode"), // Direct mapping
        };

        var mapping = CreateMappingWithColumns("test", "Source", "Target", columnMappings);
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var entry = CreateEntry(
            "Source",
            """{"Id":"1"}""",
            """{"Id":"1","Name":"Alice","Email":"ALICE@TEST.COM","Status":"active"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        var payload = success.Entries[0].MappedPayload!;
        Assert.Contains("Alice", payload); // Direct
        Assert.Contains("alice@test.com", payload); // LQL lowercase
        Assert.Contains("mobile-app", payload); // Constant
        Assert.Contains("active", payload); // Direct
    }

    [Fact]
    public void MappingEngine_ExcludedColumns_RemovedFromPayload()
    {
        var mapping = new TableMapping(
            Id: "with-exclusions",
            SourceTable: "User",
            TargetTable: "PublicUser",
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: [],
            ExcludedColumns: ["Password", "Salt", "SecurityToken", "InternalNotes"],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var entry = CreateEntry(
            "User",
            """{"Id":"1"}""",
            """{"Id":"1","Name":"Alice","Email":"a@test.com","Password":"secret123","Salt":"xyz","SecurityToken":"abc","InternalNotes":"admin notes","PublicBio":"Hello!"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        var payload = success.Entries[0].MappedPayload!;

        Assert.Contains("Name", payload);
        Assert.Contains("Email", payload);
        Assert.Contains("PublicBio", payload);
        Assert.DoesNotContain("Password", payload);
        Assert.DoesNotContain("Salt", payload);
        Assert.DoesNotContain("SecurityToken", payload);
        Assert.DoesNotContain("InternalNotes", payload);
    }

    [Fact]
    public void MappingEngine_PkMapping_RenamesKey()
    {
        var mapping = new TableMapping(
            Id: "pk-rename",
            SourceTable: "Source",
            TargetTable: "Target",
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: new PkMapping("UserId", "CustomerId"),
            ColumnMappings: [],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var entry = CreateEntry("Source", """{"UserId":"user-123"}""", """{"Name":"Test"}""");

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Contains("CustomerId", success.Entries[0].TargetPkValue);
        Assert.Contains("user-123", success.Entries[0].TargetPkValue);
        Assert.DoesNotContain("UserId", success.Entries[0].TargetPkValue);
    }

    [Fact]
    public void MappingEngine_MultiTarget_AllTargetsReceiveTransformedData()
    {
        var targets = new List<TargetConfig>
        {
            new(
                "UserProfile",
                [
                    new ColumnMapping(
                        "Name",
                        "DisplayName",
                        TransformType.Lql,
                        null,
                        "upper(Name)"
                    ),
                    new ColumnMapping("Email", "Email"),
                ]
            ),
            new(
                "UserAudit",
                [
                    new ColumnMapping("Name", "UserName"),
                    new ColumnMapping(null, "EventType", TransformType.Constant, "user_created"),
                ]
            ),
        };

        var mapping = new TableMapping(
            Id: "multi-target",
            SourceTable: "User",
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
            "User",
            """{"Id":"1"}""",
            """{"Id":"1","Name":"alice","Email":"alice@test.com"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        Assert.Equal(2, success.Entries.Count);

        var profile = success.Entries.First(e => e.TargetTable == "UserProfile");
        Assert.Contains("ALICE", profile.MappedPayload); // Uppercased

        var audit = success.Entries.First(e => e.TargetTable == "UserAudit");
        Assert.Contains("alice", audit.MappedPayload); // Original case
        Assert.Contains("user_created", audit.MappedPayload); // Constant
    }

    #endregion

    #region Complex Real-World Scenarios

    [Fact]
    public void E2E_FullAddressNormalization()
    {
        // Simulate normalizing messy address data
        var columnMappings = new List<ColumnMapping>
        {
            new("street", "Street", TransformType.Lql, null, "trim(street)"),
            new("city", "City", TransformType.Lql, null, "city |> trim() |> upper()"),
            new("state", "State", TransformType.Lql, null, "upper(state)"),
            new("zip", "PostalCode", TransformType.Lql, null, "left(zip, 5)"),
        };

        var mapping = CreateMappingWithColumns(
            "address-norm",
            "RawAddress",
            "Address",
            columnMappings
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var entry = CreateEntry(
            "RawAddress",
            """{"Id":"1"}""",
            """{"Id":"1","street":"  123 Main St  ","city":"  springfield  ","state":"il","zip":"62701-1234"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        var payload = success.Entries[0].MappedPayload!;

        Assert.Contains("123 Main St", payload); // Trimmed street
        Assert.Contains("SPRINGFIELD", payload); // Trimmed + uppercased city
        Assert.Contains("IL", payload); // Uppercased state
        Assert.Contains("62701", payload); // First 5 chars of zip
        Assert.DoesNotContain("-1234", payload); // ZIP+4 removed
    }

    [Fact]
    public void E2E_UserDataSanitization()
    {
        // Remove sensitive fields, normalize remaining
        var columnMappings = new List<ColumnMapping>
        {
            new("username", "Username", TransformType.Lql, null, "lower(username)"),
            new("email", "Email", TransformType.Lql, null, "lower(email)"),
            new("display_name", "DisplayName", TransformType.Lql, null, "trim(display_name)"),
            // Constant for sync source
            new(null, "DataSource", TransformType.Constant, "legacy_crm"),
        };

        var mapping = new TableMapping(
            Id: "user-sanitize",
            SourceTable: "LegacyUser",
            TargetTable: "CleanUser",
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: new PkMapping("legacy_id", "Id"),
            ColumnMappings: columnMappings,
            ExcludedColumns: ["password_hash", "ssn", "credit_card"],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );
        var config = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        var entry = CreateEntry(
            "LegacyUser",
            """{"legacy_id":"USR-001"}""",
            """{"legacy_id":"USR-001","username":"JohnDoe123","email":"John.Doe@Example.COM","display_name":"  John Doe  ","password_hash":"abc123","ssn":"123-45-6789","credit_card":"4111111111111111"}"""
        );

        var result = MappingEngine.ApplyMapping(entry, config, MappingDirection.Push, _logger);

        var success = Assert.IsType<MappingSuccess>(result);
        var payload = success.Entries[0].MappedPayload!;

        // PK renamed
        Assert.Contains("Id", success.Entries[0].TargetPkValue);
        Assert.Contains("USR-001", success.Entries[0].TargetPkValue);

        // Normalized fields
        Assert.Contains("johndoe123", payload); // Lowercase username
        Assert.Contains("john.doe@example.com", payload); // Lowercase email
        Assert.Contains("John Doe", payload); // Trimmed display name

        // Sensitive fields excluded
        Assert.DoesNotContain("password", payload);
        Assert.DoesNotContain("ssn", payload);
        Assert.DoesNotContain("credit_card", payload);
        Assert.DoesNotContain("4111", payload);
    }

    #endregion

    #region Helper Methods

    private static TableMapping CreateMappingWithColumns(
        string id,
        string sourceTable,
        string targetTable,
        IReadOnlyList<ColumnMapping> columnMappings
    ) =>
        new(
            Id: id,
            SourceTable: sourceTable,
            TargetTable: targetTable,
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: columnMappings,
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
