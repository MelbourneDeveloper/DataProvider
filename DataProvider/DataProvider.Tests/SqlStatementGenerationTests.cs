using Lql.SQLite;
using Outcome;
using Selecta;
using Xunit;

namespace DataProvider.Tests;

public sealed class SqlStatementGenerationTests
{
    [Fact]
    public void ToSQLite_SimpleSelectFromSingleTable_GeneratesExpectedSql()
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Id")
            .AddSelectColumn("Name")
            .AddTable("Users")
            .Build();

        var result = stmt.ToSQLite();

        Assert.IsType<StringOk>(result);
        var sql = ((StringOk)result).Value;
        Assert.Equal("SELECT Id, Name FROM Users", sql);
    }

    [Fact]
    public void ToSQLite_SelectAllWildcard_WhenNoColumnsSelected_GeneratesStar()
    {
        var stmt = new SelectStatementBuilder().AddTable("Users").Build();

        var result = stmt.ToSQLite();

        var success = Assert.IsType<StringOk>(result);
        Assert.Equal("SELECT * FROM Users", success.Value);
    }

    [Fact]
    public void ToSQLite_WithWhereComparison_FormatsCondition()
    {
        var where = WhereCondition.Comparison(
            ColumnInfo.Named("Age"),
            ComparisonOperator.GreaterThan,
            "18"
        );
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Id")
            .AddTable("Users")
            .AddWhereCondition(where)
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal("SELECT Id FROM Users WHERE Age > 18", success.Value);
    }

    [Fact]
    public void ToSQLite_WithWhereLogicalOperators_FormatsSequence()
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Id")
            .AddTable("Users")
            .AddWhereCondition(
                WhereCondition.Comparison(
                    ColumnInfo.Named("Age"),
                    ComparisonOperator.GreaterOrEq,
                    "18"
                )
            )
            .AddWhereCondition(WhereCondition.And())
            .AddWhereCondition(
                WhereCondition.Comparison(
                    ColumnInfo.Named("Country"),
                    ComparisonOperator.Eq,
                    "'AU'"
                )
            )
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal("SELECT Id FROM Users WHERE Age >= 18 AND Country = 'AU'", success.Value);
    }

    [Fact]
    public void ToSQLite_WithParenthesesInWhere_FormatsParens()
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Id")
            .AddTable("Users")
            .AddWhereCondition(WhereCondition.OpenParen())
            .AddWhereCondition(
                WhereCondition.Comparison(
                    ColumnInfo.Named("Age"),
                    ComparisonOperator.GreaterOrEq,
                    "18"
                )
            )
            .AddWhereCondition(WhereCondition.And())
            .AddWhereCondition(
                WhereCondition.Comparison(
                    ColumnInfo.Named("Age"),
                    ComparisonOperator.LessThan,
                    "65"
                )
            )
            .AddWhereCondition(WhereCondition.CloseParen())
            .AddWhereCondition(WhereCondition.And())
            .AddWhereCondition(
                WhereCondition.Comparison(ColumnInfo.Named("Active"), ComparisonOperator.Eq, "1")
            )
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal(
            "SELECT Id FROM Users WHERE ( Age >= 18 AND Age < 65 ) AND Active = 1",
            success.Value
        );
    }

    [Fact]
    public void ToSQLite_WithJoin_OutputsInnerJoin()
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Users.Id")
            .AddSelectColumn("Orders.Total")
            .AddTable("Users")
            .AddTable("Orders")
            // IMPORTANT: pass full join type text as expected by generator
            .AddJoin("Users", "Orders", "Users.Id = Orders.UserId", "INNER JOIN")
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal(
            "SELECT Users.Id, Orders.Total FROM Users INNER JOIN Orders ON Users.Id = Orders.UserId",
            success.Value
        );
    }

    [Fact]
    public void ToSQLite_WithGroupByAndHaving_OutputsClauses()
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Country")
            .AddSelectColumn(ColumnInfo.FromExpression("COUNT(*)", "Total"))
            .AddTable("Users")
            .AddGroupBy([ColumnInfo.Named("Country")])
            .WithHaving("COUNT(*) > 10")
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal(
            "SELECT Country, COUNT(*) AS Total FROM Users GROUP BY Country HAVING COUNT(*) > 10",
            success.Value
        );
    }

    [Fact]
    public void ToSQLite_WithOrderBy_OutputsOrderBy()
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn("Id")
            .AddTable("Users")
            .AddOrderBy("Name", "ASC")
            .AddOrderBy("Id", "DESC")
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal("SELECT Id FROM Users ORDER BY Name ASC, Id DESC", success.Value);
    }

    [Fact]
    public void ToSQLite_WithDistinctAndPaging_OutputsDistinctLimitOffset()
    {
        var stmt = new SelectStatementBuilder()
            .WithDistinct(true)
            .AddSelectColumn("Name")
            .AddTable("Users")
            .WithLimit("5")
            .WithOffset("10")
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal("SELECT DISTINCT Name FROM Users LIMIT 5 OFFSET 10", success.Value);
    }

    [Fact]
    public void ToSQLite_WithWildcardAndAlias_FormatsCorrectly()
    {
        var stmt = new SelectStatementBuilder()
            .AddSelectColumn(ColumnInfo.Wildcard("u"))
            .AddTable("Users")
            .Build();

        var success = Assert.IsType<StringOk>(stmt.ToSQLite());
        Assert.Equal("SELECT u.* FROM Users", success.Value);
    }
}
