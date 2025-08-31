using System.Data;

namespace Selecta;

/// <summary>
/// Extension methods for IDbConnection to execute SelectStatement queries
/// </summary>
public static class SelectStatementDbExtensions
{
    /// <summary>
    /// Creates a query builder from a table
    /// </summary>
    public static SelectStatementBuilder QueryFrom(
        this IDbConnection _,
        string tableName,
        string? alias = null
    ) => tableName.From(alias);

    /// <summary>
    /// Creates a SELECT query builder
    /// </summary>
    public static SelectStatementBuilder Select(
        this IDbConnection _,
        params (string? tableAlias, string columnName)[] columns
    ) => new SelectStatementBuilder().Select(columns);
}
