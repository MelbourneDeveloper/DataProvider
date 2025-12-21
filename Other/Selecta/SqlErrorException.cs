namespace Selecta;

/// <summary>
/// Exception that wraps a SqlError for proper error propagation
/// </summary>
public sealed class SqlErrorException : Exception
{
    /// <summary>
    /// Gets the associated <see cref="SqlError"/> if available.
    /// </summary>
    public SqlError? SqlError { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlErrorException"/> class with the specified <paramref name="sqlError"/>.
    /// The base exception message is set from <see cref="SqlError.Message"/>.
    /// </summary>
    /// <param name="sqlError">The SQL error to wrap.</param>
    public SqlErrorException(SqlError sqlError)
        : base(sqlError.Message)
    {
        SqlError = sqlError;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlErrorException"/> class with the specified <paramref name="sqlError"/>
    /// and an inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="sqlError">The SQL error to wrap.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public SqlErrorException(SqlError sqlError, Exception innerException)
        : base(sqlError.Message, innerException)
    {
        SqlError = sqlError;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlErrorException"/> class.
    /// </summary>
    public SqlErrorException()
    {
        SqlError = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlErrorException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public SqlErrorException(string message)
        : base(message)
    {
        SqlError = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlErrorException"/> class with a specified error message and a reference
    /// to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public SqlErrorException(string message, Exception innerException)
        : base(message, innerException)
    {
        SqlError = null;
    }
}
