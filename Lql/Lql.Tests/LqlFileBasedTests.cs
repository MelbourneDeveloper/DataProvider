using Lql.Postgres;
using Lql.SQLite;
using Lql.SqlServer;
using Outcome;
using Selecta;
using Xunit;

namespace Lql.Tests;

/// <summary>
/// File-based tests for LQL to PostgreSQL transformation.
/// Tests read LQL input and expected SQL output from external files.
/// </summary>
public partial class LqlFileBasedTests
{
    private static readonly string TestDataDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "TestData"
    );

    /// <summary>
    /// Shared method to execute file-based tests
    /// </summary>
    /// <param name="testCaseName">Name of the test case</param>
    /// <param name="dialect">SQL dialect (PostgreSql or SqlServer)</param>
    private static void ExecuteFileBasedTest(string testCaseName, string dialect)
    {
        // Arrange
        string lqlFile = Path.Combine(TestDataDirectory, "Lql", $"{testCaseName}.lql");
        string expectedSqlFile = Path.Combine(
            TestDataDirectory,
            "ExpectedSql",
            dialect,
            $"{testCaseName}.sql"
        );

        Assert.True(File.Exists(lqlFile), $"LQL test file {lqlFile} should exist");
        Assert.True(
            File.Exists(expectedSqlFile),
            $"Expected SQL file {expectedSqlFile} should exist"
        );

        string lqlCode = File.ReadAllText(lqlFile);
        string expectedSql = File.ReadAllText(expectedSqlFile).Trim();

        // Act
        var statementResult = LqlStatementConverter.ToStatement(lqlCode);
        if (statementResult is Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError> failure)
        {
            throw new InvalidOperationException(
                $"Parsing failed for {testCaseName}: {failure.Value.DetailedMessage}"
            );
        }
        Assert.IsType<Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>>(statementResult);
        var statement = ((Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>)statementResult).Value;

        Result<string, SqlError> sqlResult = dialect switch
        {
            "PostgreSql" => statement.ToPostgreSql(),
            "SqlServer" => statement.ToSqlServer(),
            "SQLite" => statement.ToSQLite(),
            _ => throw new ArgumentException($"Unsupported dialect: {dialect}"),
        };

        Assert.IsType<Result<string, SqlError>.Ok<string, SqlError>>(sqlResult);
        var actualSql = ((Result<string, SqlError>.Ok<string, SqlError>)sqlResult).Value;

        if (expectedSql != actualSql)
        {
            Console.WriteLine($"=== TEST FAILURE: {testCaseName} ({dialect}) ===");
            Console.WriteLine($"Expected:\n{expectedSql}");
            Console.WriteLine($"Actual:\n{actualSql}");
            Console.WriteLine("===");
        }
        Assert.Equal(expectedSql, actualSql);
    }

    [Fact]
    public void GetAllFileBasedTests_ShouldHaveMatchingFiles()
    {
        // Arrange
        string lqlDirectory = Path.Combine(TestDataDirectory, "Lql");
        string postgreSqlDirectory = Path.Combine(TestDataDirectory, "ExpectedSql", "PostgreSql");
        string sqlServerDirectory = Path.Combine(TestDataDirectory, "ExpectedSql", "SqlServer");

        if (
            !Directory.Exists(lqlDirectory)
            || !Directory.Exists(postgreSqlDirectory)
            || !Directory.Exists(sqlServerDirectory)
        )
        {
            // Skip test if directories don't exist yet
            return;
        }

        // Act
        var lqlFiles = Directory
            .GetFiles(lqlDirectory, "*.lql")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet();

        var postgreSqlFiles = Directory
            .GetFiles(postgreSqlDirectory, "*.sql")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet();

        var sqlServerFiles = Directory
            .GetFiles(sqlServerDirectory, "*.sql")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet();

        // Assert
        Assert.True(
            lqlFiles.SetEquals(postgreSqlFiles),
            "Every LQL test file should have a corresponding PostgreSQL expected SQL file"
        );

        Assert.True(
            lqlFiles.SetEquals(sqlServerFiles),
            "Every LQL test file should have a corresponding SQL Server expected SQL file"
        );
    }

    [Fact]
    public void PerformanceTest_LargeQuery_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        const string largeLqlCode = """
            -- Complex query with multiple operations
            let base_users =
                users
                |> join(user_profiles, on = users.id = user_profiles.user_id)
                |> join(user_settings, on = users.id = user_settings.user_id)
                |> filter(fn(row) => row.users.status = 'active' and row.user_profiles.verified = true)

            let user_orders =
                base_users
                |> join(orders, on = users.id = orders.user_id)
                |> join(order_items, on = orders.id = order_items.order_id)
                |> join(products, on = order_items.product_id = products.id)
                |> join(categories, on = products.category_id = categories.id)

            let aggregated_data =
                user_orders
                |> group_by(users.id, users.name, categories.name)
                |> select(
                    users.id,
                    users.name,
                    categories.name as category_name,
                    count(distinct orders.id) as order_count,
                    sum(order_items.quantity * products.price) as total_spent,
                    avg(products.rating) as avg_product_rating
                )
                |> having(fn(group) => count(distinct orders.id) > 5)

            aggregated_data
            |> filter(fn(row) => row.total_spent > 1000)
            |> order_by(total_spent desc, order_count desc)
            |> limit(100)
            """;

        // Act & Assert
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var statementResult = LqlStatementConverter.ToStatement(largeLqlCode);
        Assert.IsType<Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>>(statementResult);
        var statement = ((Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>)statementResult).Value;

        var sqlResult = statement.ToPostgreSql();
        Assert.IsType<Result<string, SqlError>.Ok<string, SqlError>>(sqlResult);

        stopwatch.Stop();
        Assert.True(
            stopwatch.ElapsedMilliseconds < 5000,
            "Large LQL query transformation should complete within 5 seconds"
        );
    }

    [Fact]
    public void StressTest_MultipleQueries_ShouldNotLeakMemory()
    {
        // Arrange
        const string lqlCode = """
            users |> join(orders, on = users.id = orders.user_id) |> select(users.name, orders.total)
            """;

        // Act & Assert
        for (int i = 0; i < 1000; i++)
        {
            var statementResult = LqlStatementConverter.ToStatement(lqlCode);
            Assert.IsType<Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>>(statementResult);
            var statement = ((Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>)statementResult).Value;

            var sqlResult = statement.ToPostgreSql();
            Assert.IsType<Result<string, SqlError>.Ok<string, SqlError>>(sqlResult);
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Memory usage should be reasonable after processing many queries
        long memoryUsage = GC.GetTotalMemory(false);
        Assert.True(
            memoryUsage < 100_000_000,
            "Memory usage should not exceed 100MB after processing 1000 queries"
        );
    }
}
