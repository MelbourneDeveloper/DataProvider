namespace Nimblesite.Sql.Model;

// ALGEBRAIC DATA TYPE!!!

/// <summary>
/// Represents a WHERE condition - a closed type hierarchy for different condition types
/// </summary>
public abstract record WhereCondition
{
    /// <summary>
    /// Prevents external inheritance - this makes the type hierarchy "closed"
    /// </summary>
    private protected WhereCondition() { }

    /// <summary>
    /// Creates a comparison condition (e.g., column = value)
    /// </summary>
    public static WhereCondition Comparison(
        ColumnInfo left,
        ComparisonOperator @operator,
        string right
    ) => new ComparisonCondition(left, @operator, right);

    /// <summary>
    /// Creates a logical operator (AND, OR)
    /// </summary>
    public static WhereCondition And() => LogicalOperator.AndOperator;

    /// <summary>
    /// Creates a logical operator (AND, OR)
    /// </summary>
    public static WhereCondition Or() => LogicalOperator.OrOperator;

    /// <summary>
    /// Creates an opening parenthesis
    /// </summary>
    public static WhereCondition OpenParen() => new Parenthesis(true);

    /// <summary>
    /// Creates a closing parenthesis
    /// </summary>
    public static WhereCondition CloseParen() => new Parenthesis(false);

    /// <summary>
    /// Creates a raw expression condition
    /// </summary>
    public static WhereCondition FromExpression(string expression) =>
        new ExpressionCondition(expression);
}

/// <summary>
/// Represents a comparison condition (e.g., column = value, column > value)
/// </summary>
public sealed record ComparisonCondition : WhereCondition
{
    /// <summary>
    /// Gets the left-hand side column of the comparison.
    /// </summary>
    public ColumnInfo Left { get; }

    /// <summary>
    /// Gets the comparison operator.
    /// </summary>
    public ComparisonOperator Operator { get; }

    /// <summary>
    /// Gets the right-hand side value or expression of the comparison.
    /// </summary>
    public string Right { get; }

    internal ComparisonCondition(ColumnInfo left, ComparisonOperator @operator, string right)
    {
        Left = left;
        Operator = @operator;
        Right = right;
    }
}

/// <summary>
/// Represents a parenthesis for grouping conditions
/// </summary>
public sealed record Parenthesis : WhereCondition
{
    /// <summary>
    /// Gets a value indicating whether this is an opening parenthesis; otherwise it is closing.
    /// </summary>
    public bool IsOpening { get; }

    internal Parenthesis(bool isOpening)
    {
        IsOpening = isOpening;
    }
}

/// <summary>
/// Represents a raw expression condition
/// </summary>
public sealed record ExpressionCondition : WhereCondition
{
    /// <summary>
    /// Gets the raw expression text.
    /// </summary>
    public string Expression { get; }

    internal ExpressionCondition(string expression)
    {
        Expression = expression;
    }
}
