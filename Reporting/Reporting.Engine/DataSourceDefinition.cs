using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Reporting.Engine;

/// <summary>
/// Defines a data source within a report (SQL, LQL, or API).
/// </summary>
public sealed record DataSourceDefinition(
    string Id,
    DataSourceType Type,
    string? ConnectionRef,
    string? Query,
    string? Url,
    string? Method,
    ImmutableDictionary<string, string>? Headers,
    ImmutableArray<string> Parameters
);

/// <summary>
/// The type of data source.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataSourceType
{
    /// <summary>Raw SQL query executed against a database connection.</summary>
    Sql,

    /// <summary>LQL expression transpiled to SQL then executed.</summary>
    Lql,

    /// <summary>REST API call returning JSON.</summary>
    Api
}
