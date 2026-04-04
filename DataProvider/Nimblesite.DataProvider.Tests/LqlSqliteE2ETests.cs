using System.Data;
using Microsoft.Data.Sqlite;
using Nimblesite.Lql.Core;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: LQL parse -> SQLite SQL conversion -> execute against real DB -> verify results.
/// Each test covers a full LQL workflow from string input to verified query results.
/// </summary>
public sealed class LqlSqliteE2ETests : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"lql_e2e_{Guid.NewGuid()}.db"
    );

    private readonly SqliteConnection _connection;

    public LqlSqliteE2ETests()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();
        CreateSchemaAndSeed();
    }

    public void Dispose()
    {
        _connection.Dispose();
        try
        {
            File.Delete(_dbPath);
        }
        catch
        { /* cleanup best-effort */
        }
    }

    private void CreateSchemaAndSeed()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE users (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT NOT NULL,
                age INTEGER NOT NULL,
                country TEXT NOT NULL,
                status TEXT NOT NULL DEFAULT 'active'
            );
            CREATE TABLE orders (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL,
                total REAL NOT NULL,
                status TEXT NOT NULL DEFAULT 'pending',
                FOREIGN KEY (user_id) REFERENCES users(id)
            );
            """;
        cmd.ExecuteNonQuery();

        // Seed users
        using var userCmd = _connection.CreateCommand();
        userCmd.CommandText = """
            INSERT INTO users VALUES ('u1', 'Alice', 'alice@test.com', 30, 'US', 'active');
            INSERT INTO users VALUES ('u2', 'Bob', 'bob@test.com', 25, 'UK', 'active');
            INSERT INTO users VALUES ('u3', 'Charlie', 'charlie@test.com', 45, 'US', 'inactive');
            INSERT INTO users VALUES ('u4', 'Diana', 'diana@test.com', 35, 'AU', 'active');
            INSERT INTO users VALUES ('u5', 'Eve', 'eve@test.com', 22, 'US', 'active');
            """;
        userCmd.ExecuteNonQuery();

        // Seed orders
        using var orderCmd = _connection.CreateCommand();
        orderCmd.CommandText = """
            INSERT INTO orders VALUES ('o1', 'u1', 150.00, 'completed');
            INSERT INTO orders VALUES ('o2', 'u1', 75.50, 'completed');
            INSERT INTO orders VALUES ('o3', 'u2', 200.00, 'pending');
            INSERT INTO orders VALUES ('o4', 'u3', 50.00, 'completed');
            INSERT INTO orders VALUES ('o5', 'u4', 300.00, 'shipped');
            INSERT INTO orders VALUES ('o6', 'u4', 125.00, 'completed');
            INSERT INTO orders VALUES ('o7', 'u5', 45.00, 'pending');
            """;
        orderCmd.ExecuteNonQuery();
    }

    [Fact]
    public void LqlSelectAllFromTable_ParseConvertExecute_ReturnsAllRows()
    {
        // Parse LQL
        var lqlCode = "users |> select(users.id, users.name, users.email)";
        var parseResult = LqlStatementConverter.ToStatement(lqlCode);
        Assert.IsType<Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>>(parseResult);
        var statement = (
            (Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>)parseResult
        ).Value;
        Assert.NotNull(statement);
        Assert.NotNull(statement.AstNode);
        Assert.Null(statement.ParseError);

        // Convert to SQLite
        var sqlResult = statement.ToSQLite();
        Assert.IsType<StringOk>(sqlResult);
        var sql = ((StringOk)sqlResult).Value;
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("users", sql, StringComparison.OrdinalIgnoreCase);

        // Execute against real DB
        var queryResult = _connection.Query<(string Id, string Name, string Email)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetString(1), r.GetString(2))
        );
        Assert.IsType<Result<IReadOnlyList<(string, string, string)>, SqlError>.Ok<
            IReadOnlyList<(string, string, string)>,
            SqlError
        >>(queryResult);
        var rows = (
            (Result<IReadOnlyList<(string, string, string)>, SqlError>.Ok<
                IReadOnlyList<(string, string, string)>,
                SqlError
            >)queryResult
        ).Value;
        Assert.Equal(5, rows.Count);
        Assert.Contains(rows, r => r.Name == "Alice");
        Assert.Contains(rows, r => r.Name == "Bob");
        Assert.Contains(rows, r => r.Email == "diana@test.com");
    }

    [Fact]
    public void LqlWithFilter_ParseConvertExecute_ReturnsFilteredRows()
    {
        var lqlCode =
            "users |> filter(fn(row) => row.users.age > 25) |> select(users.name, users.age)";
        var parseResult = LqlStatementConverter.ToStatement(lqlCode);
        Assert.IsType<Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>>(parseResult);

        var statement = (
            (Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>)parseResult
        ).Value;
        var sqlResult = statement.ToSQLite();
        Assert.IsType<StringOk>(sqlResult);
        var sql = ((StringOk)sqlResult).Value;
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);

        var queryResult = _connection.Query<(string Name, long Age)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetInt64(1))
        );
        var rows = (
            (Result<IReadOnlyList<(string, long)>, SqlError>.Ok<
                IReadOnlyList<(string, long)>,
                SqlError
            >)queryResult
        ).Value;

        // Alice (30), Charlie (45), Diana (35) are > 25
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.True(r.Age > 25));
        Assert.Contains(rows, r => r.Name == "Alice");
        Assert.Contains(rows, r => r.Name == "Charlie");
        Assert.Contains(rows, r => r.Name == "Diana");
    }

    [Fact]
    public void LqlWithOrderByAndLimit_ParseConvertExecute_ReturnsOrderedSubset()
    {
        var lqlCode =
            "users |> order_by(users.age asc) |> limit(3) |> select(users.name, users.age)";
        var parseResult = LqlStatementConverter.ToStatement(lqlCode);
        Assert.IsType<Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>>(parseResult);

        var statement = (
            (Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>)parseResult
        ).Value;
        var sqlResult = statement.ToSQLite();
        Assert.IsType<StringOk>(sqlResult);
        var sql = ((StringOk)sqlResult).Value;
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);

        var queryResult = _connection.Query<(string Name, long Age)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetInt64(1))
        );
        var rows = (
            (Result<IReadOnlyList<(string, long)>, SqlError>.Ok<
                IReadOnlyList<(string, long)>,
                SqlError
            >)queryResult
        ).Value;

        Assert.Equal(3, rows.Count);
        // Should be youngest 3: Eve (22), Bob (25), Alice (30)
        Assert.Equal("Eve", rows[0].Name);
        Assert.Equal(22L, rows[0].Age);
        Assert.Equal("Bob", rows[1].Name);
        Assert.Equal(25L, rows[1].Age);
        Assert.Equal("Alice", rows[2].Name);
        Assert.Equal(30L, rows[2].Age);
    }

    [Fact]
    public void LqlWithJoin_ParseConvertExecute_ReturnsJoinedData()
    {
        var lqlCode =
            "users |> join(orders, on = users.id = orders.user_id) |> select(users.name, orders.total)";
        var parseResult = LqlStatementConverter.ToStatement(lqlCode);
        Assert.IsType<Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>>(parseResult);

        var statement = (
            (Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>)parseResult
        ).Value;
        var sqlResult = statement.ToSQLite();
        Assert.IsType<StringOk>(sqlResult);
        var sql = ((StringOk)sqlResult).Value;
        Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("users.id = orders.user_id", sql, StringComparison.OrdinalIgnoreCase);

        var queryResult = _connection.Query<(string Name, double Total)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        var rows = (
            (Result<IReadOnlyList<(string, double)>, SqlError>.Ok<
                IReadOnlyList<(string, double)>,
                SqlError
            >)queryResult
        ).Value;

        // 7 orders across users with matching IDs
        Assert.Equal(7, rows.Count);
        Assert.Contains(rows, r => r.Name == "Alice" && r.Total == 150.00);
        Assert.Contains(rows, r => r.Name == "Alice" && r.Total == 75.50);
        Assert.Contains(rows, r => r.Name == "Bob" && r.Total == 200.00);
        Assert.Contains(rows, r => r.Name == "Diana" && r.Total == 300.00);
    }

    [Fact]
    public void LqlStatementConverter_InvalidSyntax_ReturnsError()
    {
        // Missing pipe operator
        var badLql = "users select name";
        var result = LqlStatementConverter.ToStatement(badLql);

        // Should either be an error result or a statement with a parse error
        if (result is Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError> error)
        {
            Assert.NotEmpty(error.Value.Message);
        }
        else
        {
            var stmt = ((Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>)result).Value;
            Assert.NotNull(stmt.ParseError);
            Assert.NotEmpty(stmt.ParseError.Message);
        }
    }

    [Fact]
    public void LqlStatementToSQLite_WithParseError_ReturnsError()
    {
        // Create a statement with a parse error directly
        var errorStatement = new LqlStatement { ParseError = SqlError.Create("Test parse error") };

        var sqlResult = errorStatement.ToSQLite();
        Assert.IsType<StringError>(sqlResult);
        var error = ((StringError)sqlResult).Value;
        Assert.Contains("Test parse error", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LqlStatementToSQLite_WithNullAstNode_ReturnsError()
    {
        var emptyStatement = new LqlStatement { AstNode = null };
        var sqlResult = emptyStatement.ToSQLite();
        Assert.IsType<StringError>(sqlResult);
    }

    [Fact]
    public void LqlStatementToSQLite_WithIdentifierNode_ReturnsSelectAll()
    {
        // An Identifier node should produce SELECT * FROM tableName
        var identifierStatement = new LqlStatement { AstNode = new Identifier("customers") };

        var sqlResult = identifierStatement.ToSQLite();
        Assert.IsType<StringOk>(sqlResult);
        var sql = ((StringOk)sqlResult).Value;
        Assert.Contains("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customers", sql);
    }

    [Fact]
    public void SQLiteContextDirect_BuildComplexQuery_GeneratesValidSQL()
    {
        // Use SQLiteContext directly to build a query
        var context = new SQLiteContext();
        context.SetBaseTable("users");
        context.SetSelectColumns([ColumnInfo.Named(name: "name"), ColumnInfo.Named(name: "email")]);
        context.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "status"),
                @operator: ComparisonOperator.Eq,
                right: "'active'"
            )
        );
        context.AddOrderBy([(Column: "name", Direction: "ASC")]);
        context.SetLimit("10");

        var sql = context.GenerateSQL();
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", sql);
        Assert.Contains("email", sql);
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);

        // Execute it
        var result = _connection.Query<(string Name, string Email)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetString(1))
        );
        var rows = (
            (Result<IReadOnlyList<(string, string)>, SqlError>.Ok<
                IReadOnlyList<(string, string)>,
                SqlError
            >)result
        ).Value;
        Assert.Equal(4, rows.Count); // 4 active users
        Assert.Equal("Alice", rows[0].Name); // First alphabetically
    }

    [Fact]
    public void SQLiteContextDirect_WithJoin_GeneratesValidJoinSQL()
    {
        var context = new SQLiteContext();
        context.SetBaseTable("users");
        context.AddJoin(
            joinType: "INNER JOIN",
            tableName: "orders",
            condition: "users.id = orders.user_id"
        );
        context.SetSelectColumns([
            ColumnInfo.Named(name: "name", tableAlias: "users"),
            ColumnInfo.Named(name: "total", tableAlias: "orders"),
        ]);

        var sql = context.GenerateSQL();
        Assert.Contains("INNER JOIN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders", sql);
        Assert.Contains("users.id = orders.user_id", sql);

        var result = _connection.Query<(string Name, double Total)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        var rows = (
            (Result<IReadOnlyList<(string, double)>, SqlError>.Ok<
                IReadOnlyList<(string, double)>,
                SqlError
            >)result
        ).Value;
        Assert.Equal(7, rows.Count);
    }
}
