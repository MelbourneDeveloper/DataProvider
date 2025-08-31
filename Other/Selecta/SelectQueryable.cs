using System.Collections;
using System.Linq.Expressions;

namespace Selecta;

/// <summary>
/// Represents a SQL query that can be built using LINQ query expressions
/// </summary>
public sealed record SelectQueryable<T> : IOrderedQueryable<T>
{
    private readonly SelectStatementProvider _provider;
    private readonly Expression _expression;

    /// <summary>
    /// Initializes a new instance of the SelectQueryable
    /// </summary>
    public SelectQueryable(string tableName, string? alias = null)
    {
        _provider = new SelectStatementProvider(tableName, alias);
        _expression = Expression.Constant(this);
    }

    internal SelectQueryable(SelectStatementProvider provider, Expression expression)
    {
        _provider = provider;
        _expression = expression;
    }

    /// <summary>
    /// Gets the element type
    /// </summary>
    public Type ElementType => typeof(T);

    /// <summary>
    /// Gets the expression
    /// </summary>
    public Expression Expression => _expression;

    /// <summary>
    /// Gets the query provider
    /// </summary>
    public IQueryProvider Provider => _provider;

    /// <summary>
    /// Builds the final SelectStatement
    /// </summary>
    public SelectStatement ToSqlStatement() => _provider.BuildSqlStatement(_expression);

    /// <summary>
    /// Not supported - this is for query building only
    /// </summary>
    public IEnumerator<T> GetEnumerator() =>
        throw new NotSupportedException("This queryable is for SQL generation only");

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
