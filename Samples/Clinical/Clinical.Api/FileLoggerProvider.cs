namespace Clinical.Api;

/// <summary>
/// Extension methods for adding file logging.
/// </summary>
public static class FileLoggingExtensions
{
    /// <summary>
    /// Adds file logging to the logging builder.
    /// </summary>
    public static ILoggingBuilder AddFileLogging(this ILoggingBuilder builder, string path)
    {
        // CA2000: DI container takes ownership and disposes when application shuts down
#pragma warning disable CA2000
        builder.Services.AddSingleton<ILoggerProvider>(new FileLoggerProvider(path));
#pragma warning restore CA2000
        return builder;
    }
}

/// <summary>
/// Simple file logger provider for writing logs to disk.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of FileLoggerProvider.
    /// </summary>
    public FileLoggerProvider(string path)
    {
        _path = path;
    }

    /// <summary>
    /// Creates a logger for the specified category.
    /// </summary>
    public ILogger CreateLogger(string categoryName) => new FileLogger(_path, categoryName, _lock);

    /// <summary>
    /// Disposes the provider.
    /// </summary>
    public void Dispose()
    {
        // Nothing to dispose - singleton managed by DI container
    }
}

/// <summary>
/// Simple file logger that appends log entries to a file.
/// </summary>
public sealed class FileLogger : ILogger
{
    private readonly string _path;
    private readonly string _category;
    private readonly object _lock;

    /// <summary>
    /// Initializes a new instance of FileLogger.
    /// </summary>
    public FileLogger(string path, string category, object lockObj)
    {
        _path = path;
        _category = category;
        _lock = lockObj;
    }

    /// <summary>
    /// Begins a logical operation scope.
    /// </summary>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <summary>
    /// Checks if the given log level is enabled.
    /// </summary>
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <summary>
    /// Writes a log entry to the file.
    /// </summary>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_category}: {message}";
        if (exception != null)
        {
            line += Environment.NewLine + exception;
        }

        lock (_lock)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }
}
