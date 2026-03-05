using Reporting.Engine;
using Xunit;

namespace Reporting.Tests;

#pragma warning disable CS1591

public sealed class ReportMetadataTests
{
    [Fact]
    public void ToMetadata_StripsConnectionDetails()
    {
        // Arrange
        var report = new ReportDefinition(
            Id: "secure-report",
            Title: "Secure Report",
            Parameters:
            [
                new ReportParameter(
                    Name: "date",
                    Type: ParameterType.Date,
                    Label: "Date",
                    Required: true,
                    Default: null
                ),
            ],
            DataSources:
            [
                new DataSourceDefinition(
                    Id: "ds1",
                    Type: DataSourceType.Sql,
                    ConnectionRef: "secret-connection",
                    Query: "SELECT sensitive_data FROM secret_table",
                    Url: null,
                    Method: null,
                    Headers: null,
                    Parameters: ["date"]
                ),
            ],
            Layout: new LayoutDefinition(Columns: 12, Rows: [])
        );

        // Act
        var metadata = ReportMetadataMapper.ToMetadata(report);

        // Assert
        Assert.Equal("secure-report", metadata.Id);
        Assert.Equal("Secure Report", metadata.Title);
        Assert.Single(metadata.Parameters);
        Assert.Single(metadata.DataSourceIds);
        Assert.Equal("ds1", metadata.DataSourceIds[0]);

        // Metadata should NOT contain connection strings or queries
        var json = System.Text.Json.JsonSerializer.Serialize(metadata);
        Assert.DoesNotContain("secret-connection", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive_data", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret_table", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ToMetadata_PreservesLayoutDefinition()
    {
        // Arrange
        var report = new ReportDefinition(
            Id: "layout-report",
            Title: "Layout",
            Parameters: [],
            DataSources: [],
            Layout: new LayoutDefinition(
                Columns: 12,
                Rows:
                [
                    new LayoutRow(
                        Cells:
                        [
                            new LayoutCell(
                                ColSpan: 6,
                                Component: new ComponentDefinition(
                                    Type: ComponentType.Metric,
                                    DataSource: "ds1",
                                    Title: "KPI",
                                    Value: "total",
                                    Format: "number",
                                    ChartType: null,
                                    XAxis: null,
                                    YAxis: null,
                                    Columns: null,
                                    PageSize: null,
                                    Content: null,
                                    Style: null
                                )
                            ),
                        ]
                    ),
                ]
            )
        );

        // Act
        var metadata = ReportMetadataMapper.ToMetadata(report);

        // Assert
        Assert.Equal(12, metadata.Layout.Columns);
        Assert.Single(metadata.Layout.Rows);
        Assert.Single(metadata.Layout.Rows[0].Cells);
        Assert.Equal(ComponentType.Metric, metadata.Layout.Rows[0].Cells[0].Component.Type);
    }

    [Fact]
    public void ToMetadata_PreservesCustomCss()
    {
        // Arrange
        var report = new ReportDefinition(
            Id: "css-report",
            Title: "Styled",
            Parameters: [],
            DataSources: [],
            Layout: new LayoutDefinition(Columns: 12, Rows: []),
            CustomCss: ".highlight { background: red; } .dark { color: white; }"
        );

        // Act
        var metadata = ReportMetadataMapper.ToMetadata(report);

        // Assert
        Assert.Equal(".highlight { background: red; } .dark { color: white; }", metadata.CustomCss);
    }

    [Fact]
    public void ToMetadata_PreservesCssClassAndCssStyle()
    {
        // Arrange
        var cssStyle = System
            .Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add(
                "fontWeight",
                "bold"
            )
            .Add("color", "#ff0000");

        var report = new ReportDefinition(
            Id: "css-component-report",
            Title: "Component CSS",
            Parameters: [],
            DataSources: [],
            Layout: new LayoutDefinition(
                Columns: 12,
                Rows:
                [
                    new LayoutRow(
                        Cells:
                        [
                            new LayoutCell(
                                ColSpan: 6,
                                Component: new ComponentDefinition(
                                    Type: ComponentType.Metric,
                                    DataSource: "ds1",
                                    Title: "Styled Metric",
                                    Value: "total",
                                    Format: "number",
                                    ChartType: null,
                                    XAxis: null,
                                    YAxis: null,
                                    Columns: null,
                                    PageSize: null,
                                    Content: null,
                                    Style: null,
                                    CssClass: "my-highlight",
                                    CssStyle: cssStyle
                                ),
                                CssClass: "custom-cell"
                            ),
                        ]
                    ),
                ]
            )
        );

        // Act
        var metadata = ReportMetadataMapper.ToMetadata(report);

        // Assert
        var cell = metadata.Layout.Rows[0].Cells[0];
        Assert.Equal("custom-cell", cell.CssClass);
        Assert.Equal("my-highlight", cell.Component.CssClass);
        Assert.NotNull(cell.Component.CssStyle);
        Assert.Equal("bold", cell.Component.CssStyle!["fontWeight"]);
        Assert.Equal("#ff0000", cell.Component.CssStyle!["color"]);
    }
}
