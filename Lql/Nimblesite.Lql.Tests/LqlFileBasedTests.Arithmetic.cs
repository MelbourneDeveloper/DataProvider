using Xunit;

namespace Nimblesite.Lql.Tests;

/// <summary>
/// File-based tests for LQL arithmetic operations and expressions
/// </summary>
public partial class Nimblesite.Lql.CoreFileBasedTests
{
    [Theory]
    [InlineData("arithmetic_basic", "PostgreSql")]
    [InlineData("arithmetic_basic", "SqlServer")]
    [InlineData("arithmetic_basic", "SQLite")]
    [InlineData("arithmetic_brackets", "PostgreSql")]
    [InlineData("arithmetic_brackets", "SqlServer")]
    [InlineData("arithmetic_brackets", "SQLite")]
    [InlineData("arithmetic_comparisons", "PostgreSql")]
    [InlineData("arithmetic_comparisons", "SqlServer")]
    [InlineData("arithmetic_comparisons", "SQLite")]
    [InlineData("arithmetic_functions", "PostgreSql")]
    [InlineData("arithmetic_functions", "SqlServer")]
    [InlineData("arithmetic_functions", "SQLite")]
    [InlineData("arithmetic_case", "PostgreSql")]
    [InlineData("arithmetic_case", "SqlServer")]
    [InlineData("arithmetic_case", "SQLite")]
    [InlineData("arithmetic_complex_nested", "PostgreSql")]
    [InlineData("arithmetic_complex_nested", "SqlServer")]
    [InlineData("arithmetic_complex_nested", "SQLite")]
    [InlineData("arithmetic_simple", "PostgreSql")]
    [InlineData("arithmetic_simple", "SqlServer")]
    [InlineData("arithmetic_simple", "SQLite")]
    public void Arithmetic_FileBasedTest_ShouldTransformCorrectly(
        string testCaseName,
        string dialect
    ) => ExecuteFileBasedTest(testCaseName, dialect);
}
