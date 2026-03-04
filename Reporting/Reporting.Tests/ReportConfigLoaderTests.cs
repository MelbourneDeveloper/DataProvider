using Microsoft.Extensions.Logging;
using Reporting.Engine;
using Xunit;
using LoadError = Outcome.Result<Reporting.Engine.ReportDefinition, Selecta.SqlError>.Error<
    Reporting.Engine.ReportDefinition,
    Selecta.SqlError
>;
using LoadOk = Outcome.Result<Reporting.Engine.ReportDefinition, Selecta.SqlError>.Ok<
    Reporting.Engine.ReportDefinition,
    Selecta.SqlError
>;

namespace Reporting.Tests;

#pragma warning disable CS1591

public sealed class ReportConfigLoaderTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public ReportConfigLoaderTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<ReportConfigLoaderTests>();
    }

    [Fact]
    public void LoadFromJson_WithValidJson_ReturnsReportDefinition()
    {
        // Arrange
        var json = """
            {
                "id": "test-report",
                "title": "Test Report",
                "parameters": [
                    { "name": "startDate", "type": "Date", "label": "Start", "required": true, "default": null }
                ],
                "dataSources": [
                    {
                        "id": "ds1",
                        "type": "Sql",
                        "connectionRef": "my-db",
                        "query": "SELECT * FROM products",
                        "parameters": ["startDate"]
                    }
                ],
                "layout": {
                    "columns": 12,
                    "rows": [
                        {
                            "cells": [
                                {
                                    "colSpan": 12,
                                    "component": {
                                        "type": "Table",
                                        "dataSource": "ds1",
                                        "title": "Products"
                                    }
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        // Act
        var result = ReportConfigLoader.LoadFromJson(json: json, logger: _logger);

        // Assert
        Assert.True(result is LoadOk, $"Expected success but got {result.GetType()}");
        var report = ((LoadOk)result).Value;
        Assert.Equal("test-report", report.Id);
        Assert.Equal("Test Report", report.Title);
        Assert.Single(report.Parameters);
        Assert.Equal("startDate", report.Parameters[0].Name);
        Assert.Equal(ParameterType.Date, report.Parameters[0].Type);
        Assert.Single(report.DataSources);
        Assert.Equal("ds1", report.DataSources[0].Id);
        Assert.Equal(DataSourceType.Sql, report.DataSources[0].Type);
        Assert.Single(report.Layout.Rows);
    }

    [Fact]
    public void LoadFromJson_WithInvalidJson_ReturnsError()
    {
        // Arrange
        var invalidJson = "{ not valid json }}}";

        // Act
        var result = ReportConfigLoader.LoadFromJson(json: invalidJson, logger: _logger);

        // Assert
        Assert.True(result is LoadError);
    }

    [Fact]
    public void LoadFromJson_WithMultipleDataSources_ParsesAll()
    {
        // Arrange
        var json = """
            {
                "id": "multi-ds",
                "title": "Multi DS",
                "parameters": [],
                "dataSources": [
                    { "id": "sql-ds", "type": "Sql", "connectionRef": "db", "query": "SELECT 1", "parameters": [] },
                    { "id": "lql-ds", "type": "Lql", "connectionRef": "db", "query": "products |> select(Name)", "parameters": [] }
                ],
                "layout": { "columns": 12, "rows": [] }
            }
            """;

        // Act
        var result = ReportConfigLoader.LoadFromJson(json: json, logger: _logger);

        // Assert
        Assert.True(result is LoadOk);
        var report = ((LoadOk)result).Value;
        Assert.Equal(2, report.DataSources.Length);
        Assert.Equal(DataSourceType.Sql, report.DataSources[0].Type);
        Assert.Equal(DataSourceType.Lql, report.DataSources[1].Type);
    }

    [Fact]
    public void LoadFromJson_WithLayoutComponents_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
                "id": "layout-test",
                "title": "Layout Test",
                "parameters": [],
                "dataSources": [],
                "layout": {
                    "columns": 12,
                    "rows": [
                        {
                            "cells": [
                                {
                                    "colSpan": 4,
                                    "component": {
                                        "type": "Metric",
                                        "dataSource": "ds1",
                                        "title": "Total",
                                        "value": "count",
                                        "format": "number"
                                    }
                                },
                                {
                                    "colSpan": 8,
                                    "component": {
                                        "type": "Chart",
                                        "chartType": "Bar",
                                        "dataSource": "ds1",
                                        "title": "Chart",
                                        "xAxis": { "field": "category" },
                                        "yAxis": { "field": "count", "label": "Count" }
                                    }
                                }
                            ]
                        }
                    ]
                }
            }
            """;

        // Act
        var result = ReportConfigLoader.LoadFromJson(json: json, logger: _logger);

        // Assert
        Assert.True(result is LoadOk);
        var report = ((LoadOk)result).Value;
        Assert.Single(report.Layout.Rows);
        Assert.Equal(2, report.Layout.Rows[0].Cells.Length);

        var metric = report.Layout.Rows[0].Cells[0].Component;
        Assert.Equal(ComponentType.Metric, metric.Type);
        Assert.Equal("count", metric.Value);

        var chart = report.Layout.Rows[0].Cells[1].Component;
        Assert.Equal(ComponentType.Chart, chart.Type);
        Assert.Equal(ChartType.Bar, chart.ChartType);
        Assert.Equal("category", chart.XAxis?.Field);
    }

    [Fact]
    public void LoadFromFile_WithNonexistentFile_ReturnsError()
    {
        // Act
        var result = ReportConfigLoader.LoadFromFile(
            filePath: "/tmp/nonexistent-report.json",
            logger: _logger
        );

        // Assert
        Assert.True(result is LoadError);
    }

    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }
}
