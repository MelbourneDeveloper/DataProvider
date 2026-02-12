using Xunit;

namespace Lql.Tests;

/// <summary>
/// File-based tests for basic LQL operations - select, filter, simple joins
/// </summary>
public partial class LqlFileBasedTests
{
    [Theory]
    [InlineData("simple_select", "PostgreSql")]
    [InlineData("simple_select", "SqlServer")]
    [InlineData("simple_select", "SQLite")]
    [InlineData("filter_simple_age", "PostgreSql")]
    [InlineData("filter_simple_age", "SqlServer")]
    [InlineData("filter_simple_age", "SQLite")]
    [InlineData("simple_filter_and", "PostgreSql")]
    [InlineData("simple_filter_and", "SqlServer")]
    [InlineData("simple_filter_and", "SQLite")]
    [InlineData("filter_multiple_conditions", "PostgreSql")]
    [InlineData("filter_multiple_conditions", "SqlServer")]
    [InlineData("filter_multiple_conditions", "SQLite")]
    [InlineData("filter_complex_and_or", "PostgreSql")]
    [InlineData("filter_complex_and_or", "SqlServer")]
    [InlineData("filter_complex_and_or", "SQLite")]
    [InlineData("filter_like", "PostgreSql")]
    [InlineData("filter_like", "SqlServer")]
    [InlineData("filter_like", "SQLite")]
    [InlineData("select_with_alias", "PostgreSql")]
    [InlineData("select_with_alias", "SqlServer")]
    [InlineData("select_with_alias", "SQLite")]
    public void BasicOperations_FileBasedTest_ShouldTransformCorrectly(
        string testCaseName,
        string dialect
    ) => ExecuteFileBasedTest(testCaseName, dialect);
}
