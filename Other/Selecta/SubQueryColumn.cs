namespace Selecta;

/// <summary>
/// Represents a subquery column (subselect)
/// </summary>
public sealed record SubQueryColumn : ColumnInfo
{
    /// <summary>
    /// Gets the subquery that produces the column value.
    /// </summary>
    public SelectStatement SubQuery { get; }

    internal SubQueryColumn(SelectStatement subQuery, string? alias = null)
        : base(alias)
    {
        SubQuery = subQuery;
    }
}
