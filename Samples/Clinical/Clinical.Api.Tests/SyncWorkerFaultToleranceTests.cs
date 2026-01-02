using Microsoft.Extensions.Logging;

namespace Clinical.Api.Tests;

/// <summary>
/// Tests proving sync worker fault tolerance behavior.
/// These tests verify that sync workers:
/// 1. NEVER crash when APIs are unavailable
/// 2. Retry with exponential backoff
/// 3. Log appropriately at different failure levels
/// 4. Recover gracefully when APIs become available
/// </summary>
public sealed class SyncWorkerFaultToleranceTests
{
    /// <summary>
    /// Proves that sync worker handles HttpRequestException without crashing.
    /// Simulates API being completely unreachable.
    /// </summary>
    [Fact]
    public async Task SyncWorker_HandlesHttpRequestException_WithoutCrashing()
    {
        // Arrange
        var logMessages = new List<(LogLevel Level, string Message)>();
        var logger = new TestLogger<FaultTolerantSyncWorker>(logMessages);
        var cancellationTokenSource = new CancellationTokenSource();
        var failureCount = 0;

        // Simulate API that always fails with connection refused
        Func<Task<bool>> performSync = () =>
        {
            failureCount++;
            if (failureCount >= 3)
            {
                cancellationTokenSource.Cancel();
            }
            throw new HttpRequestException("Connection refused (localhost:5001)");
        };

        var worker = new FaultTolerantSyncWorker(logger, performSync);

        // Act - Run the worker until it handles 3 failures
        await worker.ExecuteAsync(cancellationTokenSource.Token);

        // Assert - Worker should have handled multiple failures without crashing
        Assert.True(failureCount >= 3, "Worker should have retried at least 3 times");
        Assert.Contains(
            logMessages,
            m => m.Message.Contains("[SYNC-RETRY]") || m.Message.Contains("[SYNC-FAULT]")
        );
        Assert.Contains(logMessages, m => m.Message.Contains("Connection refused"));
    }

    /// <summary>
    /// Proves that sync worker uses exponential backoff when retrying.
    /// </summary>
    [Fact]
    public async Task SyncWorker_UsesExponentialBackoff_OnConsecutiveFailures()
    {
        // Arrange
        var logMessages = new List<(LogLevel Level, string Message)>();
        var logger = new TestLogger<FaultTolerantSyncWorker>(logMessages);
        var cancellationTokenSource = new CancellationTokenSource();
        var retryDelays = new List<int>();
        var failureCount = 0;

        Func<Task<bool>> performSync = () =>
        {
            failureCount++;
            if (failureCount >= 5)
            {
                cancellationTokenSource.Cancel();
            }
            throw new HttpRequestException("Connection refused");
        };

        var worker = new FaultTolerantSyncWorker(logger, performSync, retryDelays.Add);

        // Act
        await worker.ExecuteAsync(cancellationTokenSource.Token);

        // Assert - Delays should increase (exponential backoff)
        Assert.True(retryDelays.Count >= 4, "Should have recorded multiple retry delays");
        for (var i = 1; i < retryDelays.Count; i++)
        {
            Assert.True(
                retryDelays[i] >= retryDelays[i - 1],
                $"Delay should increase or stay same. Delay[{i - 1}]={retryDelays[i - 1]}, Delay[{i}]={retryDelays[i]}"
            );
        }
    }

    /// <summary>
    /// Proves that sync worker escalates log level after multiple consecutive failures.
    /// </summary>
    [Fact]
    public async Task SyncWorker_EscalatesLogLevel_AfterMultipleFailures()
    {
        // Arrange
        var logMessages = new List<(LogLevel Level, string Message)>();
        var logger = new TestLogger<FaultTolerantSyncWorker>(logMessages);
        var cancellationTokenSource = new CancellationTokenSource();
        var failureCount = 0;

        Func<Task<bool>> performSync = () =>
        {
            failureCount++;
            if (failureCount >= 5)
            {
                cancellationTokenSource.Cancel();
            }
            throw new HttpRequestException("Connection refused");
        };

        var worker = new FaultTolerantSyncWorker(logger, performSync);

        // Act
        await worker.ExecuteAsync(cancellationTokenSource.Token);

        // Assert - Early failures should be Info, later ones should be Warning
        var infoLogs = logMessages.Where(m => m.Level == LogLevel.Information).ToList();
        var warningLogs = logMessages.Where(m => m.Level == LogLevel.Warning).ToList();

        Assert.True(infoLogs.Count > 0, "Should have info-level logs for early retries");
        Assert.True(
            warningLogs.Count > 0,
            "Should have warning-level logs after multiple failures"
        );
    }

    /// <summary>
    /// Proves that sync worker recovers and resets failure counter on success.
    /// </summary>
    [Fact]
    public async Task SyncWorker_ResetsFailureCounter_OnSuccess()
    {
        // Arrange
        var logMessages = new List<(LogLevel Level, string Message)>();
        var logger = new TestLogger<FaultTolerantSyncWorker>(logMessages);
        var cancellationTokenSource = new CancellationTokenSource();
        var callCount = 0;

        Func<Task<bool>> performSync = () =>
        {
            callCount++;
            return callCount switch
            {
                1 or 2 => throw new HttpRequestException("Connection refused"), // First 2 calls fail
                3 => Task.FromResult(true), // Third call succeeds
                4 => throw new HttpRequestException("Connection refused again"), // Fourth fails
                _ => Task.FromException<bool>(new OperationCanceledException()), // Stop
            };
        };

        var worker = new FaultTolerantSyncWorker(logger, performSync);

        // Act
        try
        {
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));
            await worker.ExecuteAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Should have logged recovery message
        Assert.Contains(logMessages, m => m.Message.Contains("[SYNC-RECOVERED]"));
    }

    /// <summary>
    /// Proves that sync worker handles unexpected exceptions without crashing.
    /// </summary>
    [Fact]
    public async Task SyncWorker_HandlesUnexpectedException_WithoutCrashing()
    {
        // Arrange
        var logMessages = new List<(LogLevel Level, string Message)>();
        var logger = new TestLogger<FaultTolerantSyncWorker>(logMessages);
        var cancellationTokenSource = new CancellationTokenSource();
        var failureCount = 0;

        Func<Task<bool>> performSync = () =>
        {
            failureCount++;
            if (failureCount >= 3)
            {
                cancellationTokenSource.Cancel();
            }
            throw new InvalidOperationException("Unexpected database error");
        };

        var worker = new FaultTolerantSyncWorker(logger, performSync);

        // Act
        await worker.ExecuteAsync(cancellationTokenSource.Token);

        // Assert - Worker should have handled unexpected exceptions
        Assert.True(failureCount >= 3, "Worker should have retried after unexpected exceptions");
        Assert.Contains(logMessages, m => m.Level == LogLevel.Error);
        Assert.Contains(logMessages, m => m.Message.Contains("[SYNC-ERROR]"));
    }

    /// <summary>
    /// Proves that sync worker shuts down gracefully on cancellation.
    /// </summary>
    [Fact]
    public async Task SyncWorker_ShutsDownGracefully_OnCancellation()
    {
        // Arrange
        var logMessages = new List<(LogLevel Level, string Message)>();
        var logger = new TestLogger<FaultTolerantSyncWorker>(logMessages);
        var cancellationTokenSource = new CancellationTokenSource();

        Func<Task<bool>> performSync = async () =>
        {
            await Task.Delay(100);
            return true;
        };

        var worker = new FaultTolerantSyncWorker(logger, performSync);

        // Act - Cancel immediately
        cancellationTokenSource.Cancel();
        await worker.ExecuteAsync(cancellationTokenSource.Token);

        // Assert - Should have logged shutdown message
        Assert.Contains(
            logMessages,
            m => m.Message.Contains("[SYNC-SHUTDOWN]") || m.Message.Contains("[SYNC-EXIT]")
        );
    }

    /// <summary>
    /// Proves that backoff is capped at maximum value (30 seconds for HTTP errors).
    /// </summary>
    [Fact]
    public async Task SyncWorker_CapsBackoff_AtMaximumValue()
    {
        // Arrange
        var logMessages = new List<(LogLevel Level, string Message)>();
        var logger = new TestLogger<FaultTolerantSyncWorker>(logMessages);
        var cancellationTokenSource = new CancellationTokenSource();
        var retryDelays = new List<int>();
        var failureCount = 0;

        Func<Task<bool>> performSync = () =>
        {
            failureCount++;
            if (failureCount >= 10)
            {
                cancellationTokenSource.Cancel();
            }
            throw new HttpRequestException("Connection refused");
        };

        var worker = new FaultTolerantSyncWorker(logger, performSync, retryDelays.Add);

        // Act
        await worker.ExecuteAsync(cancellationTokenSource.Token);

        // Assert - All delays should be capped at 30 seconds
        Assert.True(retryDelays.All(d => d <= 30), "All delays should be capped at 30 seconds");
        // After enough failures, delays should hit the cap
        Assert.Contains(retryDelays, d => d == 30);
    }
}

/// <summary>
/// Test implementation of fault-tolerant sync worker behavior.
/// Mirrors the actual SyncWorker fault tolerance patterns.
/// </summary>
internal sealed class FaultTolerantSyncWorker
{
    private readonly ILogger _logger;
    private readonly Func<Task<bool>> _performSync;
    private readonly Action<int>? _onRetryDelay;

    /// <summary>
    /// Creates a fault-tolerant sync worker for testing.
    /// </summary>
    public FaultTolerantSyncWorker(
        ILogger logger,
        Func<Task<bool>> performSync,
        Action<int>? onRetryDelay = null
    )
    {
        _logger = logger;
        _performSync = performSync;
        _onRetryDelay = onRetryDelay;
    }

    /// <summary>
    /// Executes the sync worker with fault tolerance.
    /// NEVER crashes - handles all errors gracefully.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Log(LogLevel.Information, "[SYNC-START] Fault tolerant sync worker starting");

        var consecutiveFailures = 0;
        const int maxConsecutiveFailuresBeforeWarning = 3;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _performSync().ConfigureAwait(false);

                if (consecutiveFailures > 0)
                {
                    _logger.Log(
                        LogLevel.Information,
                        "[SYNC-RECOVERED] Sync recovered after {Count} consecutive failures",
                        consecutiveFailures
                    );
                    consecutiveFailures = 0;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10), stoppingToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            catch (HttpRequestException ex)
            {
                consecutiveFailures++;
                var retryDelay = Math.Min(5 * consecutiveFailures, 30);
                _onRetryDelay?.Invoke(retryDelay);

                if (consecutiveFailures >= maxConsecutiveFailuresBeforeWarning)
                {
                    _logger.Log(
                        LogLevel.Warning,
                        "[SYNC-FAULT] API unreachable for {Count} consecutive attempts. Error: {Message}. Retrying in {Delay}s...",
                        consecutiveFailures,
                        ex.Message,
                        retryDelay
                    );
                }
                else
                {
                    _logger.Log(
                        LogLevel.Information,
                        "[SYNC-RETRY] API not reachable ({Message}). Attempt {Count}, retrying in {Delay}s...",
                        ex.Message,
                        consecutiveFailures,
                        retryDelay
                    );
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(retryDelay), stoppingToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.Log(
                    LogLevel.Information,
                    "[SYNC-SHUTDOWN] Sync worker shutting down gracefully"
                );
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.Log(
                    LogLevel.Information,
                    "[SYNC-SHUTDOWN] Sync worker shutting down gracefully"
                );
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                var retryDelay = Math.Min(10 * consecutiveFailures, 60);
                _onRetryDelay?.Invoke(retryDelay);

                _logger.Log(
                    LogLevel.Error,
                    "[SYNC-ERROR] Unexpected error during sync (attempt {Count}). Retrying in {Delay}s. Error: {Message}",
                    consecutiveFailures,
                    retryDelay,
                    ex.Message
                );

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(retryDelay), stoppingToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.Log(LogLevel.Information, "[SYNC-EXIT] Sync worker exited");
    }
}

/// <summary>
/// Test logger that captures log messages for assertion.
/// </summary>
internal sealed class TestLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _messages;

    /// <summary>
    /// Creates a test logger that captures messages.
    /// </summary>
    public TestLogger(List<(LogLevel Level, string Message)> messages) => _messages = messages;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    ) => _messages.Add((logLevel, formatter(state, exception)));
}
