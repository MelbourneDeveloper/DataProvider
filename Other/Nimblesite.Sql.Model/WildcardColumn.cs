namespace Nimblesite.Sql.Model;

/// <summary>
/// Represents a wildcard column (*) that selects all columns
/// </summary>
public sealed record WildcardColumn : ColumnInfo
{
    /// <summary>
    /// Gets the optional table alias that scopes the wildcard.
    /// </summary>
    public string? TableAlias { get; }

    internal WildcardColumn(string? tableAlias = null)
        : base((string?)null)
    {
        TableAlias = tableAlias;
    }
}
