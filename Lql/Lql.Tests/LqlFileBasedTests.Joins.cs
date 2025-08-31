using Xunit;

namespace Lql.Tests;

/// <summary>
/// File-based tests for LQL join operations
/// </summary>
public partial class LqlFileBasedTests
{
    /*
    TODO: many of these are not generating the correct SQL
    Note that the SQL Server version used to look like this but something changed.
    INSERT INTO report_table (users.id, users.name)
SELECT users.id, users.name
FROM (
    SELECT users.id, users.name
    FROM users u
    INNER JOIN orders o ON users.id = orders.user_id
    WHERE orders.status = 'completed'

    UNION

    SELECT a.archived_users.id, a.archived_users.name
    FROM archived_users a
) AS all_users
    */

    [Theory]
    [InlineData("join_simple", "PostgreSql")]
    [InlineData("join_simple", "SqlServer")]
    [InlineData("join_simple", "SQLite")]
    [InlineData("join_multiple", "PostgreSql")]
    [InlineData("join_multiple", "SqlServer")]
    [InlineData("join_multiple", "SQLite")]
    [InlineData("join_left", "PostgreSql")]
    [InlineData("join_left", "SqlServer")]
    [InlineData("join_left", "SQLite")]
    [InlineData("complex_join_union", "PostgreSql")]
    [InlineData("complex_join_union", "SqlServer")]
    [InlineData("complex_join_union", "SQLite")]
    public void Joins_FileBasedTest_ShouldTransformCorrectly(string testCaseName, string dialect) =>
        ExecuteFileBasedTest(testCaseName, dialect);
}
