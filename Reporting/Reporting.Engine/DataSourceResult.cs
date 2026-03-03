using System.Collections.Immutable;

namespace Reporting.Engine;

/// <summary>
/// Result of executing a data source query.
/// </summary>
public sealed record DataSourceResult(
    ImmutableArray<string> ColumnNames,
    ImmutableArray<ImmutableArray<object?>> Rows,
    int TotalRows
);

/// <summary>
/// Result of executing an entire report.
/// </summary>
public sealed record ReportExecutionResult(
    string ReportId,
    DateTimeOffset ExecutedAt,
    ImmutableDictionary<string, DataSourceResult> DataSources
);

/// <summary>
/// Request to execute a report with parameter values.
/// </summary>
public sealed record ReportExecuteRequest(
    ImmutableDictionary<string, string> Parameters,
    string Format
);
