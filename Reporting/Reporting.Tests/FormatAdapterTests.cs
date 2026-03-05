using System.Collections.Immutable;
using System.Text.Json;
using Reporting.Engine;
using Xunit;

namespace Reporting.Tests;

#pragma warning disable CS1591

public sealed class FormatAdapterTests
{
    [Fact]
    public void ToJson_WithValidResult_ReturnsValidJson()
    {
        // Arrange
        var result = new ReportExecutionResult(
            ReportId: "test",
            ExecutedAt: new DateTimeOffset(2025, 3, 3, 10, 0, 0, TimeSpan.Zero),
            DataSources: ImmutableDictionary<string, DataSourceResult>.Empty.Add(
                "ds1",
                new DataSourceResult(
                    ColumnNames: ["Name", "Value"],
                    Rows:
                    [
                        ImmutableArray.Create<object?>("Alpha", (object?)42),
                        ImmutableArray.Create<object?>("Beta", (object?)99),
                    ],
                    TotalRows: 2
                )
            )
        );

        // Act
        var json = FormatAdapter.ToJson(result);

        // Assert
        var doc = JsonDocument.Parse(json);
        Assert.Equal("test", doc.RootElement.GetProperty("reportId").GetString());

        // Verify executedAt is present and formatted
        Assert.True(doc.RootElement.TryGetProperty("executedAt", out var executedAt));
        Assert.Contains("2025", executedAt.GetString() ?? "", StringComparison.Ordinal);

        Assert.True(doc.RootElement.TryGetProperty("dataSources", out var ds));
        Assert.True(ds.TryGetProperty("ds1", out var ds1));
        Assert.Equal(2, ds1.GetProperty("totalRows").GetInt32());

        // Verify column names in JSON
        var cols = ds1.GetProperty("columnNames");
        Assert.Equal(2, cols.GetArrayLength());
        Assert.Equal("Name", cols[0].GetString());
        Assert.Equal("Value", cols[1].GetString());

        // Verify row data
        var rows = ds1.GetProperty("rows");
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal("Alpha", rows[0][0].GetString());
        Assert.Equal(42, rows[0][1].GetInt32());
        Assert.Equal("Beta", rows[1][0].GetString());
        Assert.Equal(99, rows[1][1].GetInt32());
    }

    [Fact]
    public void ToCsv_WithValidResult_ReturnsCorrectCsv()
    {
        // Arrange
        var dsResult = new DataSourceResult(
            ColumnNames: ["Name", "Category", "Price"],
            Rows:
            [
                ImmutableArray.Create<object?>("Widget", (object?)"Tools", (object?)29.99),
                ImmutableArray.Create<object?>("Gadget", (object?)"Tech", (object?)49.99),
            ],
            TotalRows: 2
        );

        // Act
        var csv = FormatAdapter.ToCsv(dsResult);

        // Assert
        var lines = csv.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("Name,Category,Price", lines[0]);
        Assert.Equal("Widget,Tools,29.99", lines[1]);
        Assert.Equal("Gadget,Tech,49.99", lines[2]);
    }

    [Fact]
    public void ToCsv_WithNullValues_HandlesGracefully()
    {
        // Arrange
        var dsResult = new DataSourceResult(
            ColumnNames: ["Name", "Notes"],
            Rows: [ImmutableArray.Create<object?>("Widget", null)],
            TotalRows: 1
        );

        // Act
        var csv = FormatAdapter.ToCsv(dsResult);

        // Assert
        var lines = csv.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Name,Notes", lines[0]);
        Assert.Equal("Widget,", lines[1]);
    }

    [Fact]
    public void ToCsv_WithCommasInValues_EscapesCorrectly()
    {
        // Arrange
        var dsResult = new DataSourceResult(
            ColumnNames: ["Name", "Description"],
            Rows: [ImmutableArray.Create<object?>("Widget", (object?)"Small, portable device")],
            TotalRows: 1
        );

        // Act
        var csv = FormatAdapter.ToCsv(dsResult);

        // Assert
        var lines = csv.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Name,Description", lines[0]);
        Assert.Contains("Widget", lines[1], StringComparison.Ordinal);
        Assert.Contains("\"Small, portable device\"", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void ToCsv_WithEmptyResult_ReturnsHeaderOnly()
    {
        // Arrange
        var dsResult = new DataSourceResult(ColumnNames: ["Name", "Price"], Rows: [], TotalRows: 0);

        // Act
        var csv = FormatAdapter.ToCsv(dsResult);

        // Assert
        Assert.Equal("Name,Price", csv);
    }
}
