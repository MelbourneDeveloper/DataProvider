using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Reporting.Engine;

/// <summary>
/// Grid-based layout for the report.
/// </summary>
public sealed record LayoutDefinition(int Columns, ImmutableArray<LayoutRow> Rows);

/// <summary>
/// A row of cells in the report grid.
/// </summary>
public sealed record LayoutRow(ImmutableArray<LayoutCell> Cells);

/// <summary>
/// A cell in the report grid containing a component.
/// </summary>
public sealed record LayoutCell(
    int ColSpan,
    ComponentDefinition Component,
    string? CssClass = null
);

/// <summary>
/// A visual component that renders data.
/// </summary>
public sealed record ComponentDefinition(
    ComponentType Type,
    string? DataSource,
    string? Title,
    string? Value,
    string? Format,
    ChartType? ChartType,
    AxisDefinition? XAxis,
    AxisDefinition? YAxis,
    ImmutableArray<ColumnDefinition>? Columns,
    int? PageSize,
    string? Content,
    string? Style,
    string? CssClass = null,
    ImmutableDictionary<string, string>? CssStyle = null
);

/// <summary>
/// Supported component types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComponentType
{
    /// <summary>Single KPI metric value.</summary>
    Metric,

    /// <summary>Chart visualization.</summary>
    Chart,

    /// <summary>Data table with rows and columns.</summary>
    Table,

    /// <summary>Static or templated text block.</summary>
    Text,
}

/// <summary>
/// Supported chart types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChartType
{
    /// <summary>Bar chart.</summary>
    Bar,

    /// <summary>Line chart.</summary>
    Line,

    /// <summary>Pie chart.</summary>
    Pie,

    /// <summary>Area chart.</summary>
    Area,
}

/// <summary>
/// Axis definition for charts.
/// </summary>
public sealed record AxisDefinition(string Field, string? Label, string? Format);

/// <summary>
/// Column definition for table components.
/// </summary>
public sealed record ColumnDefinition(string Field, string Header);
