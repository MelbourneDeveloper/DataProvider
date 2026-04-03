using Xunit;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// File-based tests for LQL aggregation operations - group by, having, etc.
/// </summary>
public partial class Nimblesite.Lql.CoreFileBasedTests
{
    [Theory]
    [InlineData("aggregation_groupby", "PostgreSql")]
    [InlineData("aggregation_groupby", "SqlServer")]
    [InlineData("aggregation_groupby", "SQLite")]
    [InlineData("having_clause", "PostgreSql")]
    [InlineData("having_clause", "SqlServer")]
    [InlineData("having_clause", "SQLite")]
    [InlineData("order_limit", "PostgreSql")]
    [InlineData("order_limit", "SqlServer")]
    [InlineData("order_limit", "SQLite")]
    [InlineData("offset_with_limit", "PostgreSql")]
    [InlineData("offset_with_limit", "SqlServer")]
    [InlineData("offset_with_limit", "SQLite")]
    public void Aggregation_FileBasedTest_ShouldTransformCorrectly(
        string testCaseName,
        string dialect
    ) => ExecuteFileBasedTest(testCaseName, dialect);
}
