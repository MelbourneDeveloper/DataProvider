namespace Nimblesite.Sql.Model;

/// <summary>
/// Represents an expression column (calculations, functions, literals)
/// </summary>
public sealed record ExpressionColumn : ColumnInfo
{
    /// <summary>
    /// Gets the SQL expression text for this column.
    /// </summary>
    public string Expression { get; }

    internal ExpressionColumn(string expression, string? alias = null)
        : base(alias)
    {
        Expression = expression;
    }
}
