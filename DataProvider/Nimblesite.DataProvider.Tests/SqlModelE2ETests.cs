using System.Data;
using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: SQL model types (SelectStatementBuilder, WhereCondition, ColumnInfo,
/// ComparisonOperator, JoinGraph, PredicateBuilder) -> ToSQLite -> execute -> verify.
/// </summary>
public sealed class SqlModelE2ETests : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"model_e2e_{Guid.NewGuid()}.db"
    );

    private readonly SqliteConnection _connection;

    public SqlModelE2ETests()
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

    private sealed record Employee(
        string Id,
        string Name,
        string Department,
        double Salary,
        int YearsExp
    );

    private void CreateSchemaAndSeed()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Employees (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Department TEXT NOT NULL,
                Salary REAL NOT NULL,
                YearsExp INTEGER NOT NULL
            );
            CREATE TABLE Departments (
                Name TEXT PRIMARY KEY,
                Budget REAL NOT NULL,
                HeadCount INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        using var seedCmd = _connection.CreateCommand();
        seedCmd.CommandText = """
            INSERT INTO Employees VALUES ('e1', 'Alice', 'Engineering', 95000, 5);
            INSERT INTO Employees VALUES ('e2', 'Bob', 'Engineering', 105000, 8);
            INSERT INTO Employees VALUES ('e3', 'Charlie', 'Sales', 75000, 3);
            INSERT INTO Employees VALUES ('e4', 'Diana', 'Sales', 85000, 6);
            INSERT INTO Employees VALUES ('e5', 'Eve', 'Marketing', 70000, 2);
            INSERT INTO Employees VALUES ('e6', 'Frank', 'Engineering', 120000, 12);
            INSERT INTO Employees VALUES ('e7', 'Grace', 'Marketing', 80000, 4);
            INSERT INTO Employees VALUES ('e8', 'Hank', 'Sales', 90000, 7);
            INSERT INTO Departments VALUES ('Engineering', 500000, 3);
            INSERT INTO Departments VALUES ('Sales', 300000, 3);
            INSERT INTO Departments VALUES ('Marketing', 200000, 2);
            """;
        seedCmd.ExecuteNonQuery();
    }

    [Fact]
    public void SelectStatementBuilder_ComplexWhereConditions_ExecuteCorrectly()
    {
        // Build: SELECT Name, Salary FROM Employees
        //        WHERE (Department = 'Engineering' AND Salary > 100000)
        //           OR (Department = 'Sales' AND YearsExp > 5)
        var builder = new SelectStatementBuilder();
        builder.AddTable(name: "Employees");
        builder.AddSelectColumn(name: "Name");
        builder.AddSelectColumn(name: "Salary");
        builder.AddSelectColumn(name: "Department");
        builder.AddWhereCondition(WhereCondition.OpenParen());
        builder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Department"),
                @operator: ComparisonOperator.Eq,
                right: "'Engineering'"
            )
        );
        builder.AddWhereCondition(WhereCondition.And());
        builder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Salary"),
                @operator: ComparisonOperator.GreaterThan,
                right: "100000"
            )
        );
        builder.AddWhereCondition(WhereCondition.CloseParen());
        builder.AddWhereCondition(WhereCondition.Or());
        builder.AddWhereCondition(WhereCondition.OpenParen());
        builder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Department"),
                @operator: ComparisonOperator.Eq,
                right: "'Sales'"
            )
        );
        builder.AddWhereCondition(WhereCondition.And());
        builder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "YearsExp"),
                @operator: ComparisonOperator.GreaterThan,
                right: "5"
            )
        );
        builder.AddWhereCondition(WhereCondition.CloseParen());
        builder.AddOrderBy(column: "Name", direction: "ASC");
        var stmt = builder.Build();

        var sql = ((StringOk)stmt.ToSQLite()).Value;
        Assert.Contains("(", sql);
        Assert.Contains(")", sql);
        Assert.Contains("AND", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OR", sql, StringComparison.OrdinalIgnoreCase);

        var result = _connection.Query<(string Name, double Salary, string Dept)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1), r.GetString(2))
        );
        var rows = (
            (Result<IReadOnlyList<(string, double, string)>, SqlError>.Ok<
                IReadOnlyList<(string, double, string)>,
                SqlError
            >)result
        ).Value;

        // Bob (Eng, 105k), Frank (Eng, 120k), Diana (Sales, 6yr), Hank (Sales, 7yr)
        Assert.Equal(4, rows.Count);
        Assert.Contains(rows, r => r.Name == "Bob" && r.Salary == 105000);
        Assert.Contains(rows, r => r.Name == "Frank" && r.Salary == 120000);
        Assert.Contains(rows, r => r.Name == "Diana" && r.Dept == "Sales");
        Assert.Contains(rows, r => r.Name == "Hank" && r.Dept == "Sales");
    }

    [Fact]
    public void ComparisonOperators_AllOperators_ProduceCorrectSQL()
    {
        // Test each comparison operator's ToSql output
        Assert.Equal("=", ComparisonOperator.Eq.ToSql());
        Assert.Equal("<>", ComparisonOperator.NotEq.ToSql());
        Assert.Equal(">", ComparisonOperator.GreaterThan.ToSql());
        Assert.Equal("<", ComparisonOperator.LessThan.ToSql());
        Assert.Equal(">=", ComparisonOperator.GreaterOrEq.ToSql());
        Assert.Equal("<=", ComparisonOperator.LessOrEq.ToSql());
        Assert.Equal("LIKE", ComparisonOperator.Like.ToSql());
        Assert.Equal("IN", ComparisonOperator.In.ToSql());
        Assert.Equal("IS NULL", ComparisonOperator.IsNull.ToSql());
        Assert.Equal("IS NOT NULL", ComparisonOperator.IsNotNull.ToSql());

        // Test LessThan in a real query
        var ltBuilder = new SelectStatementBuilder();
        ltBuilder.AddTable(name: "Employees");
        ltBuilder.AddSelectColumn(name: "Name");
        ltBuilder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Salary"),
                @operator: ComparisonOperator.LessThan,
                right: "80000"
            )
        );
        var ltSql = ((StringOk)ltBuilder.Build().ToSQLite()).Value;
        var ltResult = _connection.Query<string>(sql: ltSql, mapper: r => r.GetString(0));
        var ltRows = (
            (Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError>)ltResult
        ).Value;
        Assert.Equal(2, ltRows.Count); // Charlie (75k), Eve (70k)

        // Test GreaterOrEq
        var geBuilder = new SelectStatementBuilder();
        geBuilder.AddTable(name: "Employees");
        geBuilder.AddSelectColumn(name: "Name");
        geBuilder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Salary"),
                @operator: ComparisonOperator.GreaterOrEq,
                right: "95000"
            )
        );
        var geSql = ((StringOk)geBuilder.Build().ToSQLite()).Value;
        var geResult = _connection.Query<string>(sql: geSql, mapper: r => r.GetString(0));
        var geRows = (
            (Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError>)geResult
        ).Value;
        Assert.Equal(3, geRows.Count); // Alice (95k), Bob (105k), Frank (120k)

        // Test LIKE
        var likeBuilder = new SelectStatementBuilder();
        likeBuilder.AddTable(name: "Employees");
        likeBuilder.AddSelectColumn(name: "Name");
        likeBuilder.AddWhereCondition(
            WhereCondition.Comparison(
                left: ColumnInfo.Named(name: "Name"),
                @operator: ComparisonOperator.Like,
                right: "'%a%'"
            )
        );
        var likeSql = ((StringOk)likeBuilder.Build().ToSQLite()).Value;
        var likeResult = _connection.Query<string>(sql: likeSql, mapper: r => r.GetString(0));
        var likeRows = (
            (Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError>)likeResult
        ).Value;
        // Grace, Diana, Frank, Charlie, Hank all contain 'a'
        Assert.True(likeRows.Count >= 3);
    }

    [Fact]
    public void ColumnInfoFactory_AllTypes_WorkCorrectly()
    {
        // Named column
        var named = ColumnInfo.Named(name: "Salary", tableAlias: "e", alias: "emp_salary");
        Assert.IsType<NamedColumn>(named);
        var namedCol = (NamedColumn)named;
        Assert.Equal("Salary", namedCol.Name);
        Assert.Equal("e", namedCol.TableAlias);
        Assert.Equal("emp_salary", namedCol.Alias);

        // Wildcard column
        var wildcard = ColumnInfo.Wildcard(tableAlias: "e");
        Assert.IsType<WildcardColumn>(wildcard);
        Assert.Equal("e", ((WildcardColumn)wildcard).TableAlias);

        // Wildcard without table alias
        var wildcardAll = ColumnInfo.Wildcard();
        Assert.IsType<WildcardColumn>(wildcardAll);
        Assert.Null(((WildcardColumn)wildcardAll).TableAlias);

        // Expression column
        var expr = ColumnInfo.FromExpression(expression: "COUNT(*)", alias: "total_count");
        Assert.IsType<ExpressionColumn>(expr);
        Assert.Equal("COUNT(*)", ((ExpressionColumn)expr).Expression);
        Assert.Equal("total_count", expr.Alias);

        // Use expression column in a real query
        var builder = new SelectStatementBuilder();
        builder.AddTable(name: "Employees");
        builder.AddSelectColumn(ColumnInfo.Named(name: "Department"));
        builder.AddSelectColumn(
            ColumnInfo.FromExpression(expression: "COUNT(*)", alias: "emp_count")
        );
        builder.AddGroupBy([ColumnInfo.Named(name: "Department")]);
        builder.AddOrderBy(column: "Department", direction: "ASC");
        var stmt = builder.Build();
        var sql = ((StringOk)stmt.ToSQLite()).Value;

        var result = _connection.Query<(string Dept, long Count)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetInt64(1))
        );
        var rows = (
            (Result<IReadOnlyList<(string, long)>, SqlError>.Ok<
                IReadOnlyList<(string, long)>,
                SqlError
            >)result
        ).Value;
        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, r => r.Dept == "Engineering" && r.Count == 3);
        Assert.Contains(rows, r => r.Dept == "Sales" && r.Count == 3);
        Assert.Contains(rows, r => r.Dept == "Marketing" && r.Count == 2);
    }

    [Fact]
    public void JoinGraph_BuildAndQuery_WorksCorrectly()
    {
        var graph = new JoinGraph();
        graph.Add(
            leftTable: "Employees",
            rightTable: "Departments",
            condition: "Employees.Department = Departments.Name",
            joinType: "INNER"
        );
        Assert.Equal(1, graph.Count);

        var relationships = graph.GetRelationships();
        Assert.Single(relationships);
        Assert.Equal("Employees", relationships[0].LeftTable);
        Assert.Equal("Departments", relationships[0].RightTable);
        Assert.Equal("INNER", relationships[0].JoinType);

        // Build a statement with the JoinGraph
        var builder = new SelectStatementBuilder();
        builder.AddTable(name: "Employees");
        builder.AddTable(name: "Departments");
        builder.AddJoin(
            leftTable: "Employees",
            rightTable: "Departments",
            condition: "Employees.Department = Departments.Name"
        );
        builder.AddSelectColumn(ColumnInfo.Named(name: "Name", tableAlias: "Employees"));
        builder.AddSelectColumn(ColumnInfo.Named(name: "Budget", tableAlias: "Departments"));
        builder.AddOrderBy(column: "Employees.Name", direction: "ASC");
        var stmt = builder.Build();

        Assert.True(stmt.HasJoins);
        Assert.Equal(2, stmt.Tables.Count);

        var sql = ((StringOk)stmt.ToSQLite()).Value;
        Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);

        var result = _connection.Query<(string Name, double Budget)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        var rows = (
            (Result<IReadOnlyList<(string, double)>, SqlError>.Ok<
                IReadOnlyList<(string, double)>,
                SqlError
            >)result
        ).Value;
        Assert.Equal(8, rows.Count); // All 8 employees with their department budget
        Assert.Contains(rows, r => r.Name == "Alice" && r.Budget == 500000);
        Assert.Contains(rows, r => r.Name == "Charlie" && r.Budget == 300000);
        Assert.Contains(rows, r => r.Name == "Eve" && r.Budget == 200000);
    }

    [Fact]
    public void SqlError_FactoryMethods_CreateCorrectErrors()
    {
        // Simple error
        var simple = SqlError.Create("Something went wrong");
        Assert.Equal("Something went wrong", simple.Message);
        Assert.Null(simple.Exception);
        Assert.Null(simple.Position);

        // Error with code
        var coded = SqlError.Create("DB error", errorCode: 42);
        Assert.Equal("DB error", coded.Message);
        Assert.Equal(42, coded.ErrorCode);

        // Error with position
        var positioned = SqlError.WithPosition(
            message: "Syntax error",
            line: 5,
            column: 10,
            source: "SELECT * FORM users"
        );
        Assert.Equal("Syntax error", positioned.Message);
        Assert.NotNull(positioned.Position);
        Assert.Equal(5, positioned.Position!.Line);
        Assert.Equal(10, positioned.Position.Column);
        Assert.Equal("SELECT * FORM users", positioned.Source);
        Assert.Contains("5", positioned.FormattedMessage);

        // Error with detailed position
        var detailed = SqlError.WithDetailedPosition(
            message: "Unknown token",
            line: 3,
            column: 15,
            startIndex: 30,
            stopIndex: 35,
            source: "query text here"
        );
        Assert.Equal(3, detailed.Position!.Line);
        Assert.Equal(15, detailed.Position.Column);
        Assert.Equal(30, detailed.Position.StartIndex);
        Assert.Equal(35, detailed.Position.StopIndex);

        // Error from exception
        var ex = new InvalidOperationException("test exception");
        var fromEx = SqlError.FromException(ex);
        Assert.Contains("test exception", fromEx.Message);
        Assert.Equal(ex, fromEx.Exception);

        // Deconstruct
        var (message, exception) = fromEx;
        Assert.Contains("test exception", message);
        Assert.Equal(ex, exception);
    }

    [Fact]
    public void WhereConditionFactory_AllTypes_WorkCorrectly()
    {
        // Comparison
        var comparison = WhereCondition.Comparison(
            left: ColumnInfo.Named(name: "Age"),
            @operator: ComparisonOperator.GreaterThan,
            right: "30"
        );
        Assert.IsType<ComparisonCondition>(comparison);
        var comp = (ComparisonCondition)comparison;
        Assert.Equal("30", comp.Right);

        // Logical operators
        var and = WhereCondition.And();
        Assert.IsType<LogicalOperator>(and);
        Assert.Equal("AND", ((LogicalOperator)and).ToSql());

        var or = WhereCondition.Or();
        Assert.IsType<LogicalOperator>(or);
        Assert.Equal("OR", ((LogicalOperator)or).ToSql());

        // Parentheses
        var open = WhereCondition.OpenParen();
        Assert.IsType<Parenthesis>(open);
        Assert.True(((Parenthesis)open).IsOpening);

        var close = WhereCondition.CloseParen();
        Assert.IsType<Parenthesis>(close);
        Assert.False(((Parenthesis)close).IsOpening);

        // Expression
        var expr = WhereCondition.FromExpression("1 = 1");
        Assert.IsType<ExpressionCondition>(expr);
        Assert.Equal("1 = 1", ((ExpressionCondition)expr).Expression);
    }

    [Fact]
    public void SelectStatementBuilder_HavingClause_ExecutesCorrectly()
    {
        var builder = new SelectStatementBuilder();
        builder.AddTable(name: "Employees");
        builder.AddSelectColumn(ColumnInfo.Named(name: "Department"));
        builder.AddSelectColumn(
            ColumnInfo.FromExpression(expression: "AVG(Salary)", alias: "AvgSalary")
        );
        builder.AddGroupBy([ColumnInfo.Named(name: "Department")]);
        builder.WithHaving("AVG(Salary) > 80000");
        builder.AddOrderBy(column: "Department", direction: "ASC");
        var stmt = builder.Build();

        Assert.Equal("AVG(Salary) > 80000", stmt.HavingCondition);

        var sql = ((StringOk)stmt.ToSQLite()).Value;
        Assert.Contains("HAVING", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AVG(Salary) > 80000", sql);

        var result = _connection.Query<(string Dept, double Avg)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        var rows = (
            (Result<IReadOnlyList<(string, double)>, SqlError>.Ok<
                IReadOnlyList<(string, double)>,
                SqlError
            >)result
        ).Value;

        // Engineering avg = (95k+105k+120k)/3 = 106.67k > 80k
        // Sales avg = (75k+85k+90k)/3 = 83.33k > 80k
        // Marketing avg = (70k+80k)/2 = 75k < 80k (excluded)
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Dept == "Engineering");
        Assert.Contains(rows, r => r.Dept == "Sales");
        Assert.DoesNotContain(rows, r => r.Dept == "Marketing");
    }

    [Fact]
    public void DatabaseTableRecord_Properties_WorkCorrectly()
    {
        var table = new DatabaseTable
        {
            Name = "TestTable",
            Schema = "dbo",
            Columns =
            [
                new DatabaseColumn
                {
                    Name = "Id",
                    CSharpType = "Guid",
                    SqlType = "TEXT",
                    IsPrimaryKey = true,
                },
                new DatabaseColumn
                {
                    Name = "AutoId",
                    CSharpType = "int",
                    SqlType = "INTEGER",
                    IsIdentity = true,
                },
                new DatabaseColumn
                {
                    Name = "Computed",
                    CSharpType = "string",
                    SqlType = "TEXT",
                    IsComputed = true,
                },
                new DatabaseColumn
                {
                    Name = "Name",
                    CSharpType = "string",
                    SqlType = "TEXT",
                },
                new DatabaseColumn
                {
                    Name = "Email",
                    CSharpType = "string",
                    SqlType = "TEXT",
                    IsNullable = true,
                    MaxLength = 255,
                },
            ],
        };

        Assert.Equal("TestTable", table.Name);
        Assert.Equal("dbo", table.Schema);
        Assert.Equal(5, table.Columns.Count);

        // PrimaryKeyColumns
        Assert.Single(table.PrimaryKeyColumns);
        Assert.Equal("Id", table.PrimaryKeyColumns[0].Name);

        // InsertableColumns (excludes identity and computed)
        Assert.Equal(3, table.InsertableColumns.Count);
        Assert.DoesNotContain(table.InsertableColumns, c => c.Name == "AutoId");
        Assert.DoesNotContain(table.InsertableColumns, c => c.Name == "Computed");

        // UpdateableColumns (excludes PK, identity, computed)
        Assert.Equal(2, table.UpdateableColumns.Count);
        Assert.Contains(table.UpdateableColumns, c => c.Name == "Name");
        Assert.Contains(table.UpdateableColumns, c => c.Name == "Email");
    }
}
