using System.Collections.ObjectModel;
using System.Data;

namespace Lql.Browser.Models;

/// <summary>
/// Result of query execution containing all necessary data
/// </summary>
public record QueryExecutionResult
{
    /// <summary>
    /// Collection of query result rows
    /// </summary>
    public required ObservableCollection<QueryResultRow> QueryResults { get; init; }

    /// <summary>
    /// Execution time as formatted string
    /// </summary>
    public required string ExecutionTime { get; init; }

    /// <summary>
    /// Row count as formatted string
    /// </summary>
    public required string RowCount { get; init; }

    /// <summary>
    /// Header for the results display
    /// </summary>
    public required string ResultsHeader { get; init; }

    /// <summary>
    /// Raw DataTable for export operations
    /// </summary>
    public required DataTable DataTable { get; init; }
}
