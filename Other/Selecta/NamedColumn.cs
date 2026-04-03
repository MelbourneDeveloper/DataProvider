namespace Nimblesite.Sql.Model;

/// <summary>
/// Represents a named column with optional table qualifier
/// </summary>
public sealed record NamedColumn : ColumnInfo
{
    /// <summary>
    /// Gets the column name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the optional table alias that qualifies the column.
    /// </summary>
    public string? TableAlias { get; }

    internal NamedColumn(string name, string? tableAlias = null, string? alias = null)
        : base(alias)
    {
        Name = name;
        TableAlias = tableAlias;
    }
}
