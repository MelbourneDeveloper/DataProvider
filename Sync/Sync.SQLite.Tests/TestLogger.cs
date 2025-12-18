namespace Sync.SQLite.Tests;

/// <summary>
/// Static logger instance for tests.
/// </summary>
public static class TestLogger
{
    /// <summary>
    /// Shared NullLogger instance for all tests.
    /// </summary>
    public static readonly ILogger L = NullLogger.Instance;
}
