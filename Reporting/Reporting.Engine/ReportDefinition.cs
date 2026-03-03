using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Reporting.Engine;

/// <summary>
/// A complete report definition including data sources and layout.
/// </summary>
public sealed record ReportDefinition(
    string Id,
    string Title,
    ImmutableArray<ReportParameter> Parameters,
    ImmutableArray<DataSourceDefinition> DataSources,
    LayoutDefinition Layout
);

/// <summary>
/// A parameter that can be passed to data source queries.
/// </summary>
public sealed record ReportParameter(
    string Name,
    ParameterType Type,
    string Label,
    bool Required,
    string? Default
);

/// <summary>
/// Supported parameter types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ParameterType
{
    /// <summary>String parameter.</summary>
    String,

    /// <summary>Date parameter.</summary>
    Date,

    /// <summary>Integer parameter.</summary>
    Integer,

    /// <summary>Decimal parameter.</summary>
    Decimal,

    /// <summary>Boolean parameter.</summary>
    Boolean
}
