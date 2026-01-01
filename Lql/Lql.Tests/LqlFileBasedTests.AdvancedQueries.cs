using Xunit;

namespace Lql.Tests;

/// <summary>
/// File-based tests for advanced LQL features - subqueries, CTEs, window functions, etc.
/// </summary>
internal partial class LqlFileBasedTests
{
    [Theory]
    [InlineData("window_function", "PostgreSql")]
    [InlineData("window_function", "SqlServer")]
    [InlineData("window_function", "SQLite")]
    // TODO: Enable when parser supports these features:
    // [InlineData("subquery_nested", "PostgreSql")] - Parenthesized pipeline expressions not yet supported
    // [InlineData("cte_with", "PostgreSql")] - WITH clauses not yet supported
    // [InlineData("exists_subquery", "PostgreSql")] - EXISTS subqueries not yet supported
    // [InlineData("in_subquery", "PostgreSql")] - IN subqueries not yet supported
    public void AdvancedQueries_FileBasedTest_ShouldTransformCorrectly(
        string testCaseName,
        string dialect
    ) => ExecuteFileBasedTest(testCaseName, dialect);
}
