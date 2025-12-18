using Selecta;

namespace Lql;

/// <summary>
/// Interface for SQL context implementations that generate dialect-specific SQL
/// </summary>
public interface ISqlContext
{
    /// <summary>
    /// Sets the base table for the query
    /// </summary>
    /// <param name="tableName">The base table name</param>
    void SetBaseTable(string tableName);

    /// <summary>
    /// Adds a JOIN to the query
    /// </summary>
    /// <param name="joinType">The type of join (INNER JOIN, LEFT JOIN, etc.)</param>
    /// <param name="tableName">The table to join</param>
    /// <param name="condition">The join condition</param>
    void AddJoin(string joinType, string tableName, string? condition);

    /// <summary>
    /// Sets the SELECT columns for the query
    /// </summary>
    /// <param name="columns">The columns to select</param>
    /// <param name="distinct">Whether to use DISTINCT</param>
    void SetSelectColumns(IEnumerable<ColumnInfo> columns, bool distinct = false);

    /// <summary>
    /// Adds a WHERE condition
    /// </summary>
    /// <param name="condition">The condition to add</param>
    /// TODO: add a where clause list type
    void AddWhereCondition(WhereCondition condition);

    /// <summary>
    /// Adds GROUP BY columns
    /// </summary>
    /// <param name="columns">The columns to group by</param>
    void AddGroupBy(IEnumerable<ColumnInfo> columns);

    /// <summary>
    /// Adds ORDER BY items
    /// </summary>
    /// <param name="orderItems">The order items (column, direction)</param>
    void AddOrderBy(IEnumerable<(string Column, string Direction)> orderItems);

    /// <summary>
    /// Adds a HAVING condition
    /// </summary>
    /// <param name="condition">The having condition</param>
    void AddHaving(string condition);

    /// <summary>
    /// Sets the LIMIT (or TOP for SQL Server)
    /// </summary>
    /// <param name="count">The limit count</param>
    void SetLimit(string count);

    /// <summary>
    /// Sets the OFFSET
    /// </summary>
    /// <param name="count">The offset count</param>
    void SetOffset(string count);

    /// <summary>
    /// Adds a UNION or UNION ALL
    /// </summary>
    /// <param name="query">The query to union with</param>
    /// <param name="isUnionAll">Whether this is UNION ALL</param>
    void AddUnion(string query, bool isUnionAll);

    /// <summary>
    /// Generates the final SQL query
    /// </summary>
    /// <returns>The SQL query string</returns>
    string GenerateSQL();
}
