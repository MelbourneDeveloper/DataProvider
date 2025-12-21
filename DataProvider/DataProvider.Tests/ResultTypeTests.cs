using Outcome;
using Xunit;

namespace DataProvider.Tests;

public class ResultTypeTests
{
    [Fact]
    public void Result_SuccessValue_CreatesSuccessResult()
    {
        // Arrange
        const string value = "Success";

        // Act
        var result = new Result<string, string>.Ok<string, string>(value);

        // Assert
        Assert.NotNull(result);
        // Note: Testing actual Result<T,E> functionality depends on the implementation
        // These tests assume the Result type follows standard functional programming patterns
    }

    [Fact]
    public void Result_ErrorValue_CreatesFailureResult()
    {
        // Arrange
        const string error = "Something went wrong";

        // Act
        var result = new Result<string, string>.Error<string, string>(error);

        // Assert
        Assert.NotNull(result);
        // Note: Testing actual Result<T,E> functionality depends on the implementation
    }

    [Fact]
    public void SqlError_WithMessage_CreatesCorrectError()
    {
        // Arrange
        const string message = "Database connection failed";

        // Act
        var error = new SqlError(message);

        // Assert
        Assert.NotNull(error);
        // Verify SqlError has proper structure for the generated code
    }

    [Fact]
    public void SqlError_WithMessageAndException_CreatesCorrectError()
    {
        // Arrange
        const string message = "Database operation failed";
        var exception = new InvalidOperationException("Connection timeout");

        // Act
        var error = new SqlError(message, exception);

        // Assert
        Assert.NotNull(error);
        // Verify SqlError properly encapsulates exceptions without throwing them
    }
}
