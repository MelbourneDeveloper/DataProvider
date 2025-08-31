namespace Selecta;

// Algebraic data type. Closed Hierarchy.


/// <summary>
/// Represents a closed set of SQL comparison operators (ADT).
/// </summary>
public abstract record ComparisonOperator
{
    /// <summary>
    /// Prevents external inheritance - this makes the type hierarchy "closed".
    /// </summary>
    private protected ComparisonOperator() { }

    /// <summary>
    /// Gets the SQL symbol or keyword that represents this operator.
    /// TODO: there should be no ToSql methods
    /// </summary>
    public string ToSql() =>
        this switch
        {
            EqualsOp => "=",
            NotEqualsOp => "!=",
            GreaterThanOp => ">",
            LessThanOp => "<",
            GreaterOrEqualOp => ">=",
            LessOrEqualOp => "<=",
            LikeOp => "LIKE",
            InOp => "IN",
            IsNullOp => "IS NULL",
            IsNotNullOp => "IS NOT NULL",
            _ => "/*UNKNOWN_OP*/",
        };

    /// <summary>
    /// Equality comparison (&quot;=&quot;).
    /// </summary>
    public static readonly ComparisonOperator Eq = new EqualsOp();

    /// <summary>
    /// Inequality comparison (&quot;!=&quot;).
    /// </summary>
    public static readonly ComparisonOperator NotEq = new NotEqualsOp();

    /// <summary>
    /// Greater-than comparison (&quot;&gt;&quot;).
    /// </summary>
    public static readonly ComparisonOperator GreaterThan = new GreaterThanOp();

    /// <summary>
    /// Less-than comparison (&quot;&lt;&quot;).
    /// </summary>
    public static readonly ComparisonOperator LessThan = new LessThanOp();

    /// <summary>
    /// Greater-than-or-equal comparison (&quot;&gt;=&quot;).
    /// </summary>
    public static readonly ComparisonOperator GreaterOrEq = new GreaterOrEqualOp();

    /// <summary>
    /// Less-than-or-equal comparison (&quot;&lt;=&quot;).
    /// </summary>
    public static readonly ComparisonOperator LessOrEq = new LessOrEqualOp();

    /// <summary>
    /// LIKE comparison.
    /// </summary>
    public static readonly ComparisonOperator Like = new LikeOp();

    /// <summary>
    /// IN comparison.
    /// </summary>
    public static readonly ComparisonOperator In = new InOp();

    /// <summary>
    /// IS NULL comparison (unary).
    /// </summary>
    public static readonly ComparisonOperator IsNull = new IsNullOp();

    /// <summary>
    /// IS NOT NULL comparison (unary).
    /// </summary>
    public static readonly ComparisonOperator IsNotNull = new IsNotNullOp();
}

/// <summary>
/// Equality comparison (&quot;=&quot;).
/// </summary>
public sealed record EqualsOp : ComparisonOperator
{
    internal EqualsOp() { }
}

/// <summary>
/// Inequality comparison (&quot;!=&quot;).
/// </summary>
public sealed record NotEqualsOp : ComparisonOperator
{
    internal NotEqualsOp() { }
}

/// <summary>
/// Greater-than comparison (&quot;&gt;&quot;).
/// </summary>
public sealed record GreaterThanOp : ComparisonOperator
{
    internal GreaterThanOp() { }
}

/// <summary>
/// Less-than comparison (&quot;&lt;&quot;).
/// </summary>
public sealed record LessThanOp : ComparisonOperator
{
    internal LessThanOp() { }
}

/// <summary>
/// Greater-than-or-equal comparison (&quot;&gt;=&quot;).
/// </summary>
public sealed record GreaterOrEqualOp : ComparisonOperator
{
    internal GreaterOrEqualOp() { }
}

/// <summary>
/// Less-than-or-equal comparison (&quot;&lt;=&quot;).
/// </summary>
public sealed record LessOrEqualOp : ComparisonOperator
{
    internal LessOrEqualOp() { }
}

/// <summary>
/// LIKE comparison.
/// </summary>
public sealed record LikeOp : ComparisonOperator
{
    internal LikeOp() { }
}

/// <summary>
/// IN comparison.
/// </summary>
public sealed record InOp : ComparisonOperator
{
    internal InOp() { }
}

/// <summary>
/// IS NULL comparison (unary).
/// </summary>
public sealed record IsNullOp : ComparisonOperator
{
    internal IsNullOp() { }
}

/// <summary>
/// IS NOT NULL comparison (unary).
/// </summary>
public sealed record IsNotNullOp : ComparisonOperator
{
    internal IsNotNullOp() { }
}
