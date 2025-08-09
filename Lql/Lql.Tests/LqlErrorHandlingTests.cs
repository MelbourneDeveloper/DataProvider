using Results;
using Xunit;

namespace Lql.Tests;

// TODO: THIS IS TOO VERBOSE!!!
// Do something similar to the expected SQL in TestData/ExpectedSql
// Text in, expected error message out!!!
// Just have one function that iterates through files and checks the error message

/// <summary>
/// Tests for error handling in LQL to PostgreSQL transformation.
/// Tests invalid syntax, malformed queries, and edge cases using Result types.
/// </summary>
public class LqlErrorHandlingTests
{
    [Fact]
    public void EmptyInput_ShouldReturnError()
    {
        // Arrange
        const string lqlCode = "";

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;
        Assert.Contains("Empty LQL input", failure.ErrorValue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WhitespaceOnlyInput_ShouldReturnError()
    {
        // Arrange
        const string lqlCode = "   \n\t   \n   ";

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;
        Assert.Contains("whitespace", failure.ErrorValue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidSyntax_ShouldReturnError()
    {
        // Arrange
        const string lqlCode = """
            users |> select(invalid syntax here
            """;

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;
        Assert.Contains("Syntax error", failure.ErrorValue.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.ErrorValue.Position);
        Assert.True(failure.ErrorValue.Position!.Line > 0);
        Assert.True(failure.ErrorValue.Position.Column >= 0);
    }

    [Fact]
    public void MissingPipeOperator_ShouldReturnError()
    {
        // Arrange
        const string lqlCode = """
            users select(users.id, users.name)
            """;

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;
        Assert.Contains("Syntax error", failure.ErrorValue.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.ErrorValue.Position);
    }

    [Fact]
    public void InvalidTableName_ShouldReturnError()
    {
        // Arrange
        const string lqlCode = """
            123_invalid_table |> select(id, name)
            """;

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;
        Assert.Contains("Syntax error", failure.ErrorValue.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.ErrorValue.Position);
    }

    [Fact]
    public void UnclosedParentheses_ShouldReturnError()
    {
        // Arrange
        const string lqlCode = """
            users |> select(users.id, users.name
            """;

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;
        Assert.Contains("Syntax error", failure.ErrorValue.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.ErrorValue.Position);
    }

    [Fact]
    public void InvalidJoinSyntax_ShouldReturnError()
    {
        // Arrange
        const string lqlCode = """
            users |> join(orders, invalid_join_syntax)
            """;

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;
        Assert.Contains("Syntax error", failure.ErrorValue.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.ErrorValue.Position);
    }

    [Fact]
    public void MissingOnClauseInJoin_ShouldReturnError()
    {
        // Arrange
        const string lqlCode = """
            users |> join(orders)
            """;

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;
        Assert.Contains("Syntax error", failure.ErrorValue.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.ErrorValue.Position);
    }

    [Fact]
    public void InvalidFilterFunction_ShouldReturnError()
    {
        // Arrange
        const string lqlCode = """
            users |> filter(invalid_filter_function)
            """;

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;
        Assert.Contains("Syntax error", failure.ErrorValue.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.ErrorValue.Position);
    }

    [Fact]
    public void UndefinedVariable_ShouldReturnError()
    {
        // Arrange
        const string lqlCode = """
            undefined_variable |> select(id, name)
            """;

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;
        Assert.Contains("Syntax error", failure.ErrorValue.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.ErrorValue.Position);
    }

    [Fact]
    public void CircularReference_ShouldReturnError()
    {
        // Arrange
        const string lqlCode = """
            let a = b |> select(id)
            let b = a |> select(name)
            a
            """;

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;
        Assert.Contains("Syntax error", failure.ErrorValue.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.ErrorValue.Position);
    }

    /*
    TODO
    [Fact]
    public void InvalidColumnReference_ShouldReturnError()
    {
        // Arrange
        const string lqlCode = """
            users |> select(nonexistent_table.column)
            """;

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;
        Assert.Contains("Syntax error", failure.ErrorValue.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.ErrorValue.Position);
    }
    */

    [Fact]
    public void ErrorMessage_ShouldIncludeLineAndColumn()
    {
        // Arrange
        const string lqlCode = """
            users |> select(
                users.id,
                users.name,
                invalid_syntax_here
            """;

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Failure>(result);
        var failure = (Result<LqlStatement, SqlError>.Failure)result;

        // Check that the error message includes position information
        Assert.Contains("line", failure.ErrorValue.FormattedMessage, StringComparison.Ordinal);
        Assert.Contains("column", failure.ErrorValue.FormattedMessage, StringComparison.Ordinal);

        // Check that position information is available
        Assert.NotNull(failure.ErrorValue.Position);
        Assert.True(failure.ErrorValue.Position!.Line > 0);
        Assert.True(failure.ErrorValue.Position.Column >= 0);
    }

    [Fact]
    public void ValidSyntax_ShouldReturnSuccess()
    {
        // Arrange
        const string lqlCode = """
            users |> select(users.id, users.name)
            """;

        // Act
        var result = LqlStatementConverter.ToStatement(lqlCode);

        // Assert
        Assert.IsType<Result<LqlStatement, SqlError>.Success>(result);
        var success = (Result<LqlStatement, SqlError>.Success)result;
        Assert.NotNull(success.Value);
    }
}
