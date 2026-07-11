namespace Nimblesite.Sql.Model;

// ALGEBRAIC DATA TYPE!! Closed Hierarchy!

/// <summary>
/// Represents a closed set of logical operators used in WHERE clauses (ADT).
/// </summary>
public abstract record LogicalOperator : WhereCondition
{
    /// <summary>
    /// Prevents external inheritance - this makes the type hierarchy "closed".
    /// </summary>
    private protected LogicalOperator() { }

    /// <summary>
    /// Gets the SQL keyword for this logical operator.
    /// </summary>
    public string ToSql() =>
        this switch
        {
            AndOp => "AND",
            OrOp => "OR",
            _ => "/*UNKNOWN_LOGICAL*/",
        };

    /// <summary>
    /// Logical AND operator.
    /// </summary>
    public static readonly LogicalOperator AndOperator = new AndOp();

    /// <summary>
    /// Logical OR operator.
    /// </summary>
    public static readonly LogicalOperator OrOperator = new OrOp();
}

/// <summary>
/// Logical AND operator.
/// </summary>
public sealed record AndOp : LogicalOperator
{
    internal AndOp() { }
}

/// <summary>
/// Logical OR operator.
/// </summary>
public sealed record OrOp : LogicalOperator
{
    internal OrOp() { }
}
