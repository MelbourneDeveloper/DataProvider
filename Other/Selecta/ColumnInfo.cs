namespace Selecta;

/// <summary>
/// Represents a column in the SELECT list - a closed type hierarchy for different column types
/// </summary>
public abstract record ColumnInfo
{
    /// <summary>
    /// The column alias (optional)
    /// </summary>
    public string? Alias { get; }

    /// <summary>
    /// Prevents external inheritance - this makes the type hierarchy "closed"
    /// </summary>
    private protected ColumnInfo(string? alias = null)
    {
        Alias = alias;
    }

    /// <summary>
    /// Creates a named column
    /// </summary>
    public static ColumnInfo Named(string name, string? tableAlias = null, string? alias = null) =>
        new NamedColumn(name, tableAlias, alias);

    /// <summary>
    /// Creates a wildcard column
    /// </summary>
    public static ColumnInfo Wildcard(string? tableAlias = null) => new WildcardColumn(tableAlias);

    /// <summary>
    /// Creates an expression column
    /// </summary>
    public static ColumnInfo FromExpression(string expression, string? alias = null) =>
        new ExpressionColumn(expression, alias);

    /// <summary>
    /// Creates a subquery column
    /// </summary>
    public static ColumnInfo FromSubQuery(SelectStatement subQuery, string? alias = null) =>
        new SubQueryColumn(subQuery, alias);
}
