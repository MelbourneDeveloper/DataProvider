namespace Selecta;

/// <summary>
/// Represents a position in source code
/// </summary>
public sealed record SourcePosition(int Line, int Column, int StartIndex = 0, int StopIndex = 0);

/// <summary>
/// Represents an error that occurred during SQL parsing or generation
/// </summary>
public sealed record SqlError(
    string Message,
    Exception? Exception = null,
    SourcePosition? Position = null,
    string? Source = null
)
{
    /// <summary>
    /// The error code (if available)
    /// </summary>
    public int? ErrorCode { get; init; }

    /// <summary>
    /// The inner exception (if available)
    /// </summary>
    public Exception? InnerException { get; init; }

    /// <summary>
    /// Creates a new SqlError with the specified message
    /// </summary>
    /// <param name="message">The error message</param>
    /// <returns>A new SqlError instance</returns>
    public static SqlError Create(string message) => new(message);

    /// <summary>
    /// Creates a new SqlError with the specified message and error code
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="errorCode">The error code</param>
    /// <returns>A new SqlError instance</returns>
    public static SqlError Create(string message, int errorCode) =>
        new(message) { ErrorCode = errorCode };

    /// <summary>
    /// Creates a SqlError with position information
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="line">The line number (1-based)</param>
    /// <param name="column">The column number (0-based)</param>
    /// <param name="source">The source text that caused the error</param>
    /// <returns>A SqlError with position information</returns>
    public static SqlError WithPosition(
        string message,
        int line,
        int column,
        string? source = null
    ) => new(message, null, new SourcePosition(line, column), source);

    /// <summary>
    /// Creates a SqlError with position information from ANTLR token
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="line">The line number (1-based)</param>
    /// <param name="column">The column number (0-based)</param>
    /// <param name="startIndex">The start index in the source</param>
    /// <param name="stopIndex">The stop index in the source</param>
    /// <param name="source">The source text that caused the error</param>
    /// <returns>A SqlError with detailed position information</returns>
    public static SqlError WithDetailedPosition(
        string message,
        int line,
        int column,
        int startIndex,
        int stopIndex,
        string? source = null
    ) => new(message, null, new SourcePosition(line, column, startIndex, stopIndex), source);

    /// <summary>
    /// Creates a SqlError from an exception
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="position">Optional position information</param>
    /// <returns>A SqlError from the exception</returns>
    public static SqlError FromException(Exception? exception, SourcePosition? position = null) =>
        exception == null
            ? new SqlError("Null exception provided")
            : new SqlError(exception.Message, exception, position) { InnerException = exception };

    /// <summary>
    /// Gets a formatted error message with position information for VSCode
    /// </summary>
    public string FormattedMessage =>
        Position == null ? Message : $"{Message} at line {Position.Line}, column {Position.Column}";

    /// <summary>
    /// Gets a detailed error message with source context
    /// </summary>
    public string DetailedMessage
    {
        get
        {
            var message = FormattedMessage;

            if (Exception is not null)
            {
                message += $": {Exception.Message}";
                if (InnerException is not null && !ReferenceEquals(InnerException, Exception))
                {
                    message += $" | Inner: {InnerException.Message}";
                }
            }

            if (!string.IsNullOrEmpty(Source) && Position != null)
            {
                var lines = Source.Split('\n');
                if (Position.Line > 0 && Position.Line <= lines.Length)
                {
                    var errorLine = lines[Position.Line - 1];
                    var pointer = new string(' ', Position.Column) + "^";
                    message += $"\n{errorLine}\n{pointer}";
                }
            }

            return message;
        }
    }

    /// <summary>
    /// Deconstruct method for simple message and exception extraction
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="exception">The exception, if any</param>
    public void Deconstruct(out string message, out Exception? exception)
    {
        message = Message;
        exception = Exception;
    }
}
