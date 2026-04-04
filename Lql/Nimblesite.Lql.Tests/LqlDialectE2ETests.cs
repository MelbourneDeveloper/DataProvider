using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Sql.Model;
using Outcome;
using Xunit;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// E2E tests: LQL parse -> convert to all 3 SQL dialects -> verify output.
/// Targets function mappings, DISTINCT, complex filters, and edge cases.
/// </summary>
public sealed class LqlDialectE2ETests
{
    private static (string PostgreSql, string SqlServer, string SQLite) ConvertToAllDialects(
        string lqlCode
    )
    {
        var result = LqlStatementConverter.ToStatement(lqlCode);
        if (result is not Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError> parseOk)
        {
            Assert.Fail($"Parse failed: {((Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError>)result).Value.DetailedMessage}");
            return default;
        }

        var stmt = parseOk.Value;

        var pg = stmt.ToPostgreSql();
        var ss = stmt.ToSqlServer();
        var sl = stmt.ToSQLite();

        if (pg is not Result<string, SqlError>.Ok<string, SqlError> pgOk)
        {
            Assert.Fail("PostgreSql conversion failed");
            return default;
        }

        if (ss is not Result<string, SqlError>.Ok<string, SqlError> ssOk)
        {
            Assert.Fail("SqlServer conversion failed");
            return default;
        }

        if (sl is not Result<string, SqlError>.Ok<string, SqlError> slOk)
        {
            Assert.Fail("SQLite conversion failed");
            return default;
        }

        return (pgOk.Value, ssOk.Value, slOk.Value);
    }

    [Fact]
    public void CountFunction_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            orders
            |> group_by(orders.user_id)
            |> select(orders.user_id, count(*) as order_count)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        Assert.Contains("COUNT(*)", pg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(*)", ss, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(*)", sl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", pg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", ss, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", sl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("order_count", pg, StringComparison.Ordinal);
        Assert.Contains("order_count", ss, StringComparison.Ordinal);
        Assert.Contains("order_count", sl, StringComparison.Ordinal);
    }

    [Fact]
    public void SumAndAvgFunctions_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            orders
            |> group_by(orders.status)
            |> select(
                orders.status,
                sum(orders.total) as total_amount,
                avg(orders.total) as avg_amount
            )
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("SUM", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AVG", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("total_amount", sql, StringComparison.Ordinal);
            Assert.Contains("avg_amount", sql, StringComparison.Ordinal);
            Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CountDistinctFunction_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            orders
            |> group_by(orders.user_id)
            |> select(orders.user_id, count(distinct orders.product_id) as unique_products)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("COUNT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DISTINCT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("unique_products", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void UpperLowerFunctions_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            users
            |> select(upper(users.name) as upper_name, lower(users.email) as lower_email)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("UPPER", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("LOWER", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("upper_name", sql, StringComparison.Ordinal);
            Assert.Contains("lower_email", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SelectDistinct_AllDialects_GeneratesDistinctSQL()
    {
        var lql = """
            users |> select_distinct(users.country)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("SELECT DISTINCT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("country", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OffsetAndLimit_AllDialects_GeneratesCorrectPagination()
    {
        var lql = """
            users
            |> order_by(users.name asc)
            |> offset(20)
            |> limit(10)
            |> select(users.id, users.name)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        Assert.Contains("OFFSET", pg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", pg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFFSET", sl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sl, StringComparison.OrdinalIgnoreCase);
        // SQL Server uses OFFSET...FETCH or TOP
        Assert.Contains("OFFSET", ss, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HavingClause_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            orders
            |> group_by(orders.user_id)
            |> having(fn(group) => count(*) > 5)
            |> select(orders.user_id, count(*) as order_count)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("HAVING", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LeftJoin_AllDialects_GeneratesLeftJoinSQL()
    {
        var lql = """
            users
            |> left_join(orders, on = users.id = orders.user_id)
            |> select(users.name, orders.total)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("LEFT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void MultipleJoins_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            users
            |> join(orders, on = users.id = orders.user_id)
            |> join(products, on = orders.product_id = products.id)
            |> select(users.name, orders.total, products.name)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("users", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("orders", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("products", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ComplexFilterWithAndOr_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            users
            |> filter(fn(row) => row.users.age > 18 and row.users.country = 'US' or row.users.status = 'premium')
            |> select(users.id, users.name)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AND", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("OR", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OrderByDescending_AllDialects_GeneratesDescSQL()
    {
        var lql = """
            users
            |> order_by(users.age desc, users.name asc)
            |> select(users.id, users.name, users.age)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DESC", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ASC", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FilterWithStringLiteral_AllDialects_PreservesQuotes()
    {
        var lql = """
            users
            |> filter(fn(row) => row.users.status = 'active')
            |> select(users.id, users.name)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("'active'", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FilterWithNumericComparison_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            products
            |> filter(fn(row) => row.products.price >= 100 and row.products.price <= 500)
            |> select(products.id, products.name, products.price)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains(">=", sql, StringComparison.Ordinal);
            Assert.Contains("<=", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void JoinWithFilterAndOrderBy_AllDialects_FullPipelineWorks()
    {
        var lql = """
            users
            |> join(orders, on = users.id = orders.user_id)
            |> filter(fn(row) => row.orders.total > 100)
            |> order_by(orders.total desc)
            |> limit(50)
            |> select(users.name, orders.total, orders.status)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DESC", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GroupByWithMultipleAggregates_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            orders
            |> group_by(orders.status)
            |> select(
                orders.status,
                count(*) as cnt,
                sum(orders.total) as total,
                avg(orders.total) as avg_val,
                count(distinct orders.user_id) as unique_users
            )
            |> order_by(total desc)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("COUNT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SUM", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AVG", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SimpleTableReference_AllDialects_GeneratesSelectStar()
    {
        var lql = "users |> select(users.id, users.name)";
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FROM", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ArithmeticExpressions_AllDialects_GeneratesCorrectSQL()
    {
        var lql = """
            products
            |> select(
                products.id,
                products.price * products.quantity as total_value,
                products.price + 10 as price_plus_ten
            )
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("*", sql, StringComparison.Ordinal);
            Assert.Contains("+", sql, StringComparison.Ordinal);
            Assert.Contains("total_value", sql, StringComparison.Ordinal);
            Assert.Contains("price_plus_ten", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CaseExpression_AllDialects_GeneratesCaseWhenSQL()
    {
        var lql = """
            orders
            |> select(
                orders.id,
                case
                    when orders.total > 1000 then orders.total * 0.95
                    when orders.total > 500 then orders.total * 0.97
                    else orders.total
                end as discounted
            )
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("CASE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WHEN", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("THEN", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ELSE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("END", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FilterWithNotEquals_AllDialects_GeneratesCorrectOperator()
    {
        var lql = """
            users
            |> filter(fn(row) => row.users.status != 'deleted')
            |> select(users.id, users.name)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                sql.Contains("!=", StringComparison.Ordinal)
                    || sql.Contains("<>", StringComparison.Ordinal),
                "Should contain != or <> operator"
            );
        }
    }

    [Fact]
    public void SelectWithColumnAlias_AllDialects_GeneratesAliasedColumns()
    {
        var lql = """
            users
            |> select(users.id, users.name as full_name, users.email as contact_email)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        foreach (var sql in new[] { pg, ss, sl })
        {
            Assert.Contains("full_name", sql, StringComparison.Ordinal);
            Assert.Contains("contact_email", sql, StringComparison.Ordinal);
            Assert.Contains("AS", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LimitOnly_AllDialects_GeneratesLimitSQL()
    {
        var lql = """
            users |> limit(25) |> select(users.id, users.name)
            """;
        var (pg, ss, sl) = ConvertToAllDialects(lql);

        Assert.Contains("LIMIT", pg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sl, StringComparison.OrdinalIgnoreCase);
        // SQL Server uses TOP
        Assert.True(
            ss.Contains("TOP", StringComparison.OrdinalIgnoreCase)
                || ss.Contains("FETCH", StringComparison.OrdinalIgnoreCase),
            "SQL Server should use TOP or FETCH for limit"
        );
    }
}
