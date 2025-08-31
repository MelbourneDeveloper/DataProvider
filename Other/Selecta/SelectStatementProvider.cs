using System.Linq.Expressions;

namespace Selecta;

/// <summary>
/// Query provider that builds SelectStatement from LINQ expressions
/// </summary>
public sealed class SelectStatementProvider : IQueryProvider
{
    private readonly SelectStatementBuilder _builder;

    /// <summary>
    /// Initializes a new instance of the SelectStatementProvider
    /// </summary>
    public SelectStatementProvider(string tableName, string? tableAlias = null)
    {
        _builder = new SelectStatementBuilder().AddTable(tableName, tableAlias);
    }

    /// <summary>
    /// Creates a query
    /// </summary>
    public IQueryable CreateQuery(Expression expression) => CreateQuery<object>(expression);

    /// <summary>
    /// Creates a typed query
    /// </summary>
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new SelectQueryable<TElement>(this, expression);

    /// <summary>
    /// Not supported - this is for query building only
    /// </summary>
    public object? Execute(Expression expression) =>
        throw new NotSupportedException("This provider is for SQL generation only");

    /// <summary>
    /// Not supported - this is for query building only
    /// </summary>
    public TResult Execute<TResult>(Expression expression) =>
        throw new NotSupportedException("This provider is for SQL generation only");

    /// <summary>
    /// Builds the SelectStatement from the expression tree
    /// </summary>
    internal SelectStatement BuildSqlStatement(Expression expression)
    {
        var visitor = new SelectStatementVisitor(_builder);
        visitor.Visit(expression);
        return _builder.Build();
    }
}
