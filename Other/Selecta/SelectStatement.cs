using System.Collections.Frozen;

namespace Selecta;

/// <summary>
/// Represents a parsed SQL SELECT statement with extracted metadata that is generic for all SQL flavors
/// </summary>
public sealed record SelectStatement
{
    /// <summary>
    /// Gets the SELECT list columns
    /// </summary>
    public IReadOnlyList<ColumnInfo> SelectList { get; init; } = [];

    /// <summary>
    /// Gets the tables in the FROM clause
    /// </summary>
    public FrozenSet<TableInfo> Tables { get; init; } = [];

    /// <summary>
    /// Gets the query parameters
    /// </summary>
    public FrozenSet<ParameterInfo> Parameters { get; init; } = [];

    /// <summary>
    /// Gets the join graph
    /// </summary>
    public JoinGraph JoinGraph { get; init; } = new();

    /// <summary>
    /// Gets the WHERE conditions
    /// </summary>
    public IReadOnlyList<WhereCondition> WhereConditions { get; init; } = [];

    /// <summary>
    /// Gets the GROUP BY columns
    /// </summary>
    public FrozenSet<ColumnInfo> GroupByColumns { get; init; } = [];

    /// <summary>
    /// Gets the ORDER BY items
    /// </summary>
    public IReadOnlyList<OrderByItem> OrderByItems { get; init; } = [];

    /// <summary>
    /// Gets the HAVING condition
    /// TODO: this is wrong. This should be a collection, not a string
    /// </summary>
    public string? HavingCondition { get; init; }

    /// <summary>
    /// Gets the LIMIT value
    /// </summary>
    public string? Limit { get; init; }

    /// <summary>
    /// Gets the OFFSET value
    /// </summary>
    public string? Offset { get; init; }

    /// <summary>
    /// Gets whether the query uses DISTINCT
    /// </summary>
    public bool IsDistinct { get; init; }

    /// <summary>
    /// Gets the UNION operations
    /// </summary>
    public FrozenSet<UnionOperation> Unions { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the query has joins
    /// </summary>
    public bool HasJoins => JoinGraph.Count > 0;

    /// <summary>
    /// Creates a new SelectQueryable from a table name
    /// </summary>
    public static SelectQueryable<T> From<T>(string? tableName = null, string? alias = null) =>
        new(tableName ?? typeof(T).Name, alias);

    /// <summary>
    /// Creates a new dynamic SelectQueryable from a table name
    /// </summary>
    public static SelectQueryable<dynamic> From(string tableName, string? alias = null) =>
        new(tableName, alias);
}
