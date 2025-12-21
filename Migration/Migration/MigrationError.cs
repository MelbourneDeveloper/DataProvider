namespace Migration;

/// <summary>
/// Migration error with message and optional inner exception.
/// </summary>
/// <param name="Message">Error message</param>
/// <param name="InnerException">Optional inner exception</param>
public sealed record MigrationError(string Message, Exception? InnerException = null)
{
    /// <summary>
    /// Creates a migration error from a message.
    /// </summary>
    public static MigrationError FromMessage(string message) => new(message);

    /// <summary>
    /// Creates a migration error from an exception.
    /// </summary>
    public static MigrationError FromException(Exception ex) => new(ex.Message, ex);

    /// <inheritdoc />
    public override string ToString() =>
        InnerException is null ? Message : $"{Message}: {InnerException.Message}";
}
