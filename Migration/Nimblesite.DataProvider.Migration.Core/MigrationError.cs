namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Nimblesite.DataProvider.Migration.Core error with message and optional inner exception.
/// </summary>
/// <param name="Message">Error message</param>
/// <param name="InnerException">Optional inner exception</param>
public sealed record Nimblesite.DataProvider.Migration.CoreError(string Message, Exception? InnerException = null)
{
    /// <summary>
    /// Creates a migration error from a message.
    /// </summary>
    public static Nimblesite.DataProvider.Migration.CoreError FromMessage(string message) => new(message);

    /// <summary>
    /// Creates a migration error from an exception.
    /// </summary>
    public static Nimblesite.DataProvider.Migration.CoreError FromException(Exception ex) => new(ex.Message, ex);

    /// <inheritdoc />
    public override string ToString() =>
        InnerException is null ? Message : $"{Message}: {InnerException.Message}";
}
