using System.Text;
using Lql.FunctionMapping;
using Selecta;

namespace Lql.Postgres;

/// <summary>
/// Context for building PostgreSQL queries with proper table aliases and structure
/// </summary>
public sealed class PostgreSqlContext : ISqlContext
{
#pragma warning disable IDE0052 // Remove unread private members
    private readonly IFunctionMappingProvider _functionMappingProvider;
#pragma warning restore IDE0052 // Remove unread private members
    private readonly SelectStatementBuilder _builder = new();
    private string? _baseTable;
    private string? _baseAlias;

    /// <summary>
    /// Initializes a new instance of the PostgreSqlContext class
    /// </summary>
    /// <param name="functionMappingProvider">The function mapping provider (defaults to PostgreSQL provider)</param>
    public PostgreSqlContext(IFunctionMappingProvider? functionMappingProvider = null)
    {
        _functionMappingProvider =
            functionMappingProvider ?? PostgreSqlFunctionMappingLocal.Instance;
    }

    /// <summary>
    /// Gets a value indicating whether this query has joins
    /// </summary>
    public bool HasJoins => _builder.Build().HasJoins;

    /// <summary>
    /// Sets the base table for the query
    /// </summary>
    /// <param name="tableName">The base table name</param>
    public void SetBaseTable(string tableName)
    {
        _baseTable = tableName;
        _baseAlias = GenerateTableAlias(tableName);
        _builder.AddTable(tableName, _baseAlias);
    }

    /// <summary>
    /// Adds a JOIN to the query
    /// </summary>
    /// <param name="joinType">The type of join (INNER JOIN, LEFT JOIN, etc.)</param>
    /// <param name="tableName">The table to join</param>
    /// <param name="condition">The join condition</param>
    public void AddJoin(string joinType, string tableName, string? condition)
    {
        string alias;

        // Check if this is a subquery (starts with SELECT or parentheses)
        if (
            tableName.TrimStart().StartsWith("(SELECT", StringComparison.OrdinalIgnoreCase)
            || tableName.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
        )
        {
            // For subqueries, try to extract the base table name to generate alias
            alias = ExtractSubqueryAlias(tableName);
        }
        else
        {
            // For regular tables, use the standard alias generation
            alias = GenerateTableAlias(tableName);
        }

        _builder.AddTable(tableName, alias);
        if (!string.IsNullOrEmpty(condition))
        {
            _builder.AddJoin(_baseTable ?? "", tableName, condition, joinType);
        }
    }

    /// <summary>
    /// Extracts an appropriate alias for a subquery by finding the base table name
    /// </summary>
    /// <param name="subquerySql">The subquery SQL</param>
    /// <returns>The generated alias</returns>
    private static string ExtractSubqueryAlias(string subquerySql)
    {
        // Try to find the FROM clause and extract the table name
        var upperSql = subquerySql.ToUpperInvariant();
        var fromIndex = upperSql.IndexOf("FROM", StringComparison.Ordinal);

        if (fromIndex >= 0)
        {
            // Find the table name after FROM
            var afterFrom = subquerySql[(fromIndex + 4)..].Trim();
            var words = afterFrom.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length > 0)
            {
                var tableName = words[0];
                // Generate alias from the actual table name
                return GenerateTableAlias(tableName);
            }
        }

        // Fallback to generic alias if we can't extract the table name
        return "sq"; // subquery
    }

    /// <summary>
    /// Sets the SELECT columns for the query
    /// </summary>
    /// <param name="columns">The columns to select</param>
    /// <param name="distinct">Whether to use DISTINCT</param>
    public void SetSelectColumns(IEnumerable<ColumnInfo> columns, bool distinct = false) =>
        _builder.WithSelectColumns(columns, distinct);

    /// <summary>
    /// Adds a WHERE condition
    /// </summary>
    /// <param name="condition">The condition to add</param>
    public void AddWhereCondition(WhereCondition condition) =>
        _builder.AddWhereCondition(condition);

    /// <summary>
    /// Adds GROUP BY columns
    /// </summary>
    /// <param name="columns">The columns to group by</param>
    public void AddGroupBy(IEnumerable<ColumnInfo> columns) => _builder.AddGroupBy(columns);

    /// <summary>
    /// Adds ORDER BY items
    /// </summary>
    /// <param name="orderItems">The order items (column, direction)</param>
    public void AddOrderBy(IEnumerable<(string Column, string Direction)> orderItems)
    {
        foreach (var (column, direction) in orderItems)
        {
            _builder.AddOrderBy(column, direction);
        }
    }

    /// <summary>
    /// Adds a HAVING condition
    /// </summary>
    /// <param name="condition">The having condition</param>
    public void AddHaving(string condition) => _builder.WithHaving(condition);

    /// <summary>
    /// Sets the LIMIT
    /// </summary>
    /// <param name="count">The limit count</param>
    public void SetLimit(string count) => _builder.WithLimit(count);

    /// <summary>
    /// Sets the OFFSET
    /// </summary>
    /// <param name="count">The offset count</param>
    public void SetOffset(string count) => _builder.WithOffset(count);

    /// <summary>
    /// Adds a UNION or UNION ALL
    /// </summary>
    /// <param name="query">The query to union with</param>
    /// <param name="isUnionAll">Whether this is UNION ALL</param>
    public void AddUnion(string query, bool isUnionAll) => _builder.AddUnion(query, isUnionAll);

    /// <summary>
    /// Generates the final SQL query
    /// </summary>
    /// <returns>The SQL query string</returns>
    public string GenerateSQL()
    {
        var statement = _builder.Build();

        if (statement.Unions.Count > 0)
        {
            return GenerateUnionSQL(statement);
        }

        return GenerateSelectSQL(statement);
    }

    /// <summary>
    /// Generates a SELECT SQL query
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The SELECT SQL string</returns>
    private string GenerateSelectSQL(SelectStatement statement)
    {
        var sql = new StringBuilder();

        // SELECT clause
        sql.Append(GenerateSelectClause(statement));

        // FROM clause
        sql.Append(GenerateFromClause(statement));

        // WHERE clause
        if (statement.WhereConditions.Count > 0)
        {
            sql.Append(GenerateWhereClause(statement));
        }

        // GROUP BY clause
        if (statement.GroupByColumns.Count > 0)
        {
            sql.Append(GenerateGroupByClause(statement));
        }

        // HAVING clause
        if (!string.IsNullOrEmpty(statement.HavingCondition))
        {
            sql.Append(GenerateHavingClause(statement));
        }

        // ORDER BY clause
        if (statement.OrderByItems.Count > 0)
        {
            sql.Append(GenerateOrderByClause(statement));
        }

        // LIMIT clause
        if (!string.IsNullOrEmpty(statement.Limit))
        {
            sql.Append(GenerateLimitClause(statement));
        }

        // OFFSET clause
        if (!string.IsNullOrEmpty(statement.Offset))
        {
            sql.Append(
                System.Globalization.CultureInfo.InvariantCulture,
                $"\nOFFSET {statement.Offset}"
            );
        }

        return sql.ToString();
    }

    /// <summary>
    /// Generates a UNION SQL query
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The UNION SQL string</returns>
    private string GenerateUnionSQL(SelectStatement statement)
    {
        var sql = new StringBuilder();

        // Add the main query
        sql.Append(GenerateSelectSQL(statement));

        // Add each union
        foreach (var union in statement.Unions)
        {
            sql.Append(union.IsUnionAll ? "\nUNION ALL\n" : "\nUNION\n");
            sql.Append(union.Query);
        }

        return sql.ToString();
    }

    /// <summary>
    /// Generates the SELECT clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The SELECT clause string</returns>
    private static string GenerateSelectClause(SelectStatement statement)
    {
        var selectKeyword = statement.IsDistinct ? "SELECT DISTINCT" : "SELECT";

        if (statement.SelectList.Count == 0)
        {
            return $"{selectKeyword} *";
        }

        var processedColumns = statement.SelectList.Select(GenerateColumnSqlWithAlias);

        // Use single-line format for simple queries (3 or fewer columns, no joins)
        // Use multi-line format for complex queries
        bool useMultiLine = statement.SelectList.Count > 3 || statement.HasJoins;

        if (useMultiLine)
        {
            // Format columns with proper indentation
            var formattedColumns = processedColumns.Select(col => $"    {col}");
            var columns = string.Join(",\n", formattedColumns);
            return $"{selectKeyword}\n{columns}";
        }
        else
        {
            // Single-line format
            var columns = string.Join(", ", processedColumns);
            return $"{selectKeyword} {columns}";
        }
    }

    /// <summary>
    /// Generates the FROM clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The FROM clause string</returns>
    private string GenerateFromClause(SelectStatement statement)
    {
        if (_baseTable == null)
        {
            return "";
        }

        var sql = new StringBuilder();

        var baseTable = statement.Tables.Count > 0 ? statement.Tables.First() : null;
        if (baseTable == null)
        {
            return "";
        }

        if (statement.HasJoins)
        {
            sql.Append(
                System.Globalization.CultureInfo.InvariantCulture,
                $"\nFROM {baseTable.Name} {baseTable.Alias}"
            );
        }
        else
        {
            sql.Append(
                System.Globalization.CultureInfo.InvariantCulture,
                $"\nFROM {baseTable.Name}"
            );
        }

        // Add joins - get from Tables (skip first one which is base table)
        var joinTables = statement.Tables.Count > 1 ? statement.Tables.Skip(1) : [];
        var joinRelationships = statement.JoinGraph.GetRelationships();

        foreach (var table in joinTables)
        {
            var relationship = joinRelationships.FirstOrDefault(j => j.RightTable == table.Name);
            var joinType = relationship?.JoinType ?? "INNER JOIN";

            sql.Append(
                System.Globalization.CultureInfo.InvariantCulture,
                $"\n{joinType} {table.Name} {table.Alias}"
            );

            if (relationship != null && !string.IsNullOrEmpty(relationship.Condition))
            {
                var processedCondition = relationship.Condition;
                sql.Append(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $" ON {processedCondition}"
                );
            }
        }

        return sql.ToString();
    }

    /// <summary>
    /// Generates the WHERE clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The WHERE clause string</returns>
    private static string GenerateWhereClause(SelectStatement statement) =>
        $"\nWHERE {string.Join(" AND ", statement.WhereConditions.Select(GenerateWhereConditionSql))}";

    /// <summary>
    /// Generates SQL for a single WHERE condition
    /// </summary>
    /// <param name="condition">The WHERE condition</param>
    /// <returns>The SQL string for the condition</returns>
    private static string GenerateWhereConditionSql(WhereCondition condition) =>
        condition switch
        {
            ComparisonCondition c => $"{GenerateColumnSql(c.Left)} {c.Operator.ToSql()} {c.Right}",
            LogicalOperator lo => lo.ToSql(),
            Parenthesis p => p.IsOpening ? "(" : ")",
            ExpressionCondition e => e.Expression,
            _ => "/*UNKNOWN_WHERE*/",
        };

    /// <summary>
    /// Generates SQL for a ColumnInfo
    /// </summary>
    /// <param name="columnInfo">The column info</param>
    /// <returns>The SQL string for the column</returns>
    private static string GenerateColumnSql(ColumnInfo columnInfo) =>
        columnInfo switch
        {
            NamedColumn n => string.IsNullOrEmpty(n.TableAlias)
                ? n.Name
                : $"{n.TableAlias}.{n.Name}",
            WildcardColumn w => string.IsNullOrEmpty(w.TableAlias) ? "*" : $"{w.TableAlias}.*",
            ExpressionColumn e => e.Expression,
            SubQueryColumn s => $"({s.SubQuery})",
            _ => "/*UNKNOWN_COLUMN*/",
        };

    /// <summary>
    /// Generates SQL for a ColumnInfo with alias if present
    /// </summary>
    /// <param name="columnInfo">The column info</param>
    /// <returns>The SQL string for the column with alias</returns>
    private static string GenerateColumnSqlWithAlias(ColumnInfo columnInfo)
    {
        var sql = GenerateColumnSql(columnInfo);
        return string.IsNullOrEmpty(columnInfo.Alias) ? sql : $"{sql} AS {columnInfo.Alias}";
    }

    /// <summary>
    /// Generates a table alias from a table name
    /// </summary>
    /// <param name="tableName">The table name</param>
    /// <returns>The generated alias</returns>
    private static string GenerateTableAlias(string tableName)
    {
        ArgumentNullException.ThrowIfNull(tableName);
        // Use first letter of the table name (to match expected test output)
        return tableName.Length > 0 ? tableName[0].ToString().ToLowerInvariant() : "t";
    }

    /// <summary>
    /// Generates the GROUP BY clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The GROUP BY clause string</returns>
    private static string GenerateGroupByClause(SelectStatement statement)
    {
        var processedColumns = statement.GroupByColumns.Select(GenerateColumnSql);

        return $"\nGROUP BY {string.Join(", ", processedColumns)}";
    }

    /// <summary>
    /// Generates the HAVING clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The HAVING clause string</returns>
    private static string GenerateHavingClause(SelectStatement statement)
    {
        if (string.IsNullOrEmpty(statement.HavingCondition))
        {
            return "";
        }

        var processedCondition = statement.HavingCondition;

        return $"\nHAVING {processedCondition}";
    }

    /// <summary>
    /// Generates the ORDER BY clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The ORDER BY clause string</returns>
    private static string GenerateOrderByClause(SelectStatement statement)
    {
        var processedItems = statement.OrderByItems.Select(item =>
        {
            var processedColumn = GenerateColumnSql(ColumnInfo.Named(item.Column));
            return $"{processedColumn} {item.Direction}";
        });

        return $"\nORDER BY {string.Join(", ", processedItems)}";
    }

    /// <summary>
    /// Generates the LIMIT clause
    /// </summary>
    /// <param name="statement">The SQL statement to generate from</param>
    /// <returns>The LIMIT clause string</returns>
    private static string GenerateLimitClause(SelectStatement statement) =>
        $"\nLIMIT {statement.Limit}";
}
