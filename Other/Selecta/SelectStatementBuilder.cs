using System.Collections.Frozen;

namespace Selecta;

/// <summary>
/// Builder for constructing SelectStatement instances
/// ONLY FOR SELECT STATEMENTS!!!!
/// </summary>
public sealed class SelectStatementBuilder
{
    private readonly List<ColumnInfo> _selectList = [];
    private readonly List<TableInfo> _tables = [];
    private readonly List<ParameterInfo> _parameters = [];
    private readonly JoinGraph _joinGraph = new();
    private readonly List<WhereCondition> _whereConditions = [];
    private readonly List<ColumnInfo> _groupByColumns = [];
    private readonly List<OrderByItem> _orderByItems = [];
    private readonly List<UnionOperation> _unions = [];
    private string? _havingCondition;
    private string? _limit;
    private string? _offset;
    private bool _isDistinct;

    /// <summary>
    /// Sets the SELECT list columns
    /// </summary>
    /// <param name="columns">The columns to select</param>
    /// <param name="distinct">Whether to use DISTINCT</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder WithSelectColumns(
        IEnumerable<ColumnInfo> columns,
        bool distinct = false
    )
    {
        _selectList.Clear();
        _selectList.AddRange(columns);
        _isDistinct = distinct;
        return this;
    }

    /// <summary>
    /// Adds a SELECT column
    /// </summary>
    /// <param name="column">The column to add</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder AddSelectColumn(ColumnInfo column)
    {
        _selectList.Add(column);
        return this;
    }

    /// <summary>
    /// Adds a SELECT column by name
    /// </summary>
    /// <param name="name">The column name</param>
    /// <param name="alias">The column alias</param>
    /// <param name="tableAlias">The table alias</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder AddSelectColumn(
        string name,
        string? alias = null,
        string? tableAlias = null
    )
    {
        _selectList.Add(ColumnInfo.Named(name, tableAlias, alias));
        return this;
    }

    /// <summary>
    /// Adds a table to the FROM clause
    /// </summary>
    /// <param name="table">The table to add</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder AddTable(TableInfo table)
    {
        _tables.Add(table);
        return this;
    }

    /// <summary>
    /// Adds a table by name
    /// </summary>
    /// <param name="name">The table name</param>
    /// <param name="alias">The table alias</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder AddTable(string name, string? alias = null)
    {
        _tables.Add(new TableInfo(name, alias));
        return this;
    }

    /// <summary>
    /// Adds a join to the query
    /// </summary>
    /// <param name="leftTable">The left table</param>
    /// <param name="rightTable">The right table</param>
    /// <param name="condition">The join condition</param>
    /// <param name="joinType">The join type</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder AddJoin(
        string leftTable,
        string rightTable,
        string condition,
        string joinType = "INNER"
    )
    {
        _joinGraph.Add(leftTable, rightTable, condition, joinType);
        return this;
    }

    /// <summary>
    /// Adds a WHERE condition
    /// </summary>
    /// <param name="condition">The condition to add</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder AddWhereCondition(WhereCondition condition)
    {
        _whereConditions.Add(condition);
        return this;
    }

    /// <summary>
    /// Adds a WHERE condition from a string expression
    /// </summary>
    /// <param name="condition">The condition string to add</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder AddWhereCondition(string condition)
    {
        _whereConditions.Add(WhereCondition.FromExpression(condition));
        return this;
    }

    /// <summary>
    /// Adds GROUP BY columns
    /// </summary>
    /// <param name="columns">The columns to group by</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder AddGroupBy(IEnumerable<ColumnInfo> columns)
    {
        _groupByColumns.AddRange(columns);
        return this;
    }

    /// <summary>
    /// Adds an ORDER BY item
    /// </summary>
    /// <param name="column">The column to order by</param>
    /// <param name="direction">The order direction</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder AddOrderBy(string column, string direction)
    {
        _orderByItems.Add(new OrderByItem(column, direction));
        return this;
    }

    /// <summary>
    /// Sets the HAVING condition
    /// </summary>
    /// <param name="condition">The having condition</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder WithHaving(string condition)
    {
        _havingCondition = condition;
        return this;
    }

    /// <summary>
    /// Sets the LIMIT
    /// </summary>
    /// <param name="limit">The limit value</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder WithLimit(string limit)
    {
        _limit = limit;
        return this;
    }

    /// <summary>
    /// Sets the OFFSET
    /// </summary>
    /// <param name="offset">The offset value</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder WithOffset(string offset)
    {
        _offset = offset;
        return this;
    }

    /// <summary>
    /// Sets whether to use DISTINCT
    /// </summary>
    /// <param name="distinct">Whether to use DISTINCT</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder WithDistinct(bool distinct = true)
    {
        _isDistinct = distinct;
        return this;
    }

    /// <summary>
    /// Adds a UNION operation
    /// </summary>
    /// <param name="query">The query to union with</param>
    /// <param name="isUnionAll">Whether this is UNION ALL</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder AddUnion(string query, bool isUnionAll = false)
    {
        _unions.Add(new UnionOperation(query, isUnionAll));
        return this;
    }

    /// <summary>
    /// Adds a parameter
    /// </summary>
    /// <param name="parameter">The parameter to add</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder AddParameter(ParameterInfo parameter)
    {
        _parameters.Add(parameter);
        return this;
    }

    /// <summary>
    /// Adds a parameter by name and type
    /// </summary>
    /// <param name="name">The parameter name</param>
    /// <param name="sqlType">The SQL type</param>
    /// <returns>This builder instance</returns>
    public SelectStatementBuilder AddParameter(string name, string sqlType = "NVARCHAR")
    {
        _parameters.Add(new ParameterInfo(name, sqlType));
        return this;
    }

    /// <summary>
    /// Builds the SelectStatement
    /// </summary>
    /// <returns>The constructed SelectStatement</returns>
    public SelectStatement Build() =>
        new()
        {
            SelectList = _selectList.AsReadOnly(),
            Tables = _tables.ToFrozenSet(),
            Parameters = _parameters.ToFrozenSet(),
            JoinGraph = _joinGraph,
            WhereConditions = _whereConditions.AsReadOnly(),
            GroupByColumns = _groupByColumns.ToFrozenSet(),
            OrderByItems = _orderByItems.AsReadOnly(),
            HavingCondition = _havingCondition,
            Limit = _limit,
            Offset = _offset,
            IsDistinct = _isDistinct,
            Unions = _unions.ToFrozenSet(),
        };
}
