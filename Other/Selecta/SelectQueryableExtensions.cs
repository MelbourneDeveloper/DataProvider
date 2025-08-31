namespace Selecta;

/// <summary>
/// Extension methods to avoid casting when using ToSqlStatement
/// </summary>
public static class SelectQueryableExtensions
{
    /// <summary>
    /// Converts an IQueryable to SelectStatement (only works if it's actually a SelectQueryable)
    /// </summary>
    public static SelectStatement ToSqlStatement<T>(this IQueryable<T> queryable) =>
        queryable is SelectQueryable<T> selectQueryable
            ? selectQueryable.ToSqlStatement()
            : throw new InvalidOperationException(
                "ToSqlStatement() can only be called on SelectQueryable instances"
            );
}
