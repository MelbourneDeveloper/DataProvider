using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Reporting.Engine;

/// <summary>
/// Server-side connection configuration. Never exposed to clients.
/// </summary>
public sealed record ConnectionRegistry(
    ImmutableDictionary<string, ConnectionConfig> Connections,
    ImmutableDictionary<string, string> ApiEndpoints
);

/// <summary>
/// Configuration for a database connection.
/// </summary>
public sealed record ConnectionConfig(
    DatabaseProvider Provider,
    string ConnectionString
);

/// <summary>
/// Supported database providers.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DatabaseProvider
{
    /// <summary>SQLite database.</summary>
    Sqlite,

    /// <summary>PostgreSQL database.</summary>
    Postgres,

    /// <summary>SQL Server database.</summary>
    SqlServer
}
