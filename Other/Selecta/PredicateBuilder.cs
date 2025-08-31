using System.Linq.Expressions;

namespace Selecta;

/// <summary>
/// PredicateBuilder enables dynamic construction of Expression trees for LINQ predicates.
/// Allows combining multiple predicates using And/Or operations for complex filtering.
/// Uses parameter rebinding to ensure proper SQL translation.
/// </summary>
public static class PredicateBuilder
{
    /// <summary>
    /// Creates a predicate that always returns true (universal quantifier).
    /// </summary>
    /// <typeparam name="T">The type being filtered</typeparam>
    /// <returns>Expression that always evaluates to true</returns>
    public static Expression<Func<T, bool>> True<T>()
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        return Expression.Lambda<Func<T, bool>>(Expression.Constant(true), parameter);
    }

    /// <summary>
    /// Creates a predicate that always returns false (empty set).
    /// </summary>
    /// <typeparam name="T">The type being filtered</typeparam>
    /// <returns>Expression that always evaluates to false</returns>
    public static Expression<Func<T, bool>> False<T>()
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        return Expression.Lambda<Func<T, bool>>(Expression.Constant(false), parameter);
    }

    /// <summary>
    /// Combines two predicates using logical OR operation.
    /// </summary>
    /// <typeparam name="T">The type being filtered</typeparam>
    /// <param name="expr1">First predicate expression</param>
    /// <param name="expr2">Second predicate expression</param>
    /// <returns>Combined expression using OR logic</returns>
    public static Expression<Func<T, bool>> Or<T>(
        this Expression<Func<T, bool>> expr1,
        Expression<Func<T, bool>> expr2
    )
    {
        var parameter = expr1.Parameters[0];
        var expr2Body = new ParameterReplacer(expr2.Parameters[0], parameter).Visit(expr2.Body);
        return Expression.Lambda<Func<T, bool>>(
            Expression.OrElse(expr1.Body, expr2Body!),
            parameter
        );
    }

    /// <summary>
    /// Combines two predicates using logical AND operation.
    /// </summary>
    /// <typeparam name="T">The type being filtered</typeparam>
    /// <param name="expr1">First predicate expression</param>
    /// <param name="expr2">Second predicate expression</param>
    /// <returns>Combined expression using AND logic</returns>
    public static Expression<Func<T, bool>> And<T>(
        this Expression<Func<T, bool>> expr1,
        Expression<Func<T, bool>> expr2
    )
    {
        var parameter = expr1.Parameters[0];
        var expr2Body = new ParameterReplacer(expr2.Parameters[0], parameter).Visit(expr2.Body);
        return Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(expr1.Body, expr2Body!),
            parameter
        );
    }

    /// <summary>
    /// Negates a predicate using logical NOT operation.
    /// </summary>
    /// <typeparam name="T">The type being filtered</typeparam>
    /// <param name="expr">Predicate expression to negate</param>
    /// <returns>Negated expression</returns>
    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expr)
    {
        var negated = Expression.Not(expr.Body);
        return Expression.Lambda<Func<T, bool>>(negated, expr.Parameters);
    }

    /// <summary>
    /// Helper class to replace parameter references in expression trees.
    /// Enables proper parameter binding when combining expressions.
    /// </summary>
    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        internal ParameterReplacer(
            ParameterExpression oldParameter,
            ParameterExpression newParameter
        )
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _oldParameter ? _newParameter : node;
    }
}
