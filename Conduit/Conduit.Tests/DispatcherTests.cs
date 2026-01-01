using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Outcome;
using Xunit;

namespace Conduit.Tests;

/// <summary>
/// E2E tests for the Conduit dispatcher.
/// No mocking - uses real handlers and behaviors.
/// </summary>
public sealed class DispatcherTests
{
    private static readonly int[] ExpectedOrder = [1, 2, 3, 4];
    private readonly ILogger _logger = new TestLogger();

    [Fact]
    public async Task SendWithRegisteredHandlerReturnsSuccess()
    {
        // Arrange
        var registry = ConduitRegistry.Empty.AddHandler<TestRequest, TestResponse>(
            TestHandlers.Handle
        );

        var request = new TestRequest(Name: "Test");

        // Act
        var result = await Dispatcher
            .Send<TestRequest, TestResponse>(request: request, registry: registry, logger: _logger)
            .ConfigureAwait(false);

        // Assert
        Assert.True(result is Result<TestResponse, ConduitError>.Ok<TestResponse, ConduitError>);
        var ok = (Result<TestResponse, ConduitError>.Ok<TestResponse, ConduitError>)result;
        Assert.Equal("Hello, Test!", ok.Value.Message);
    }

    [Fact]
    public async Task SendWithNoHandlerReturnsNoHandlerError()
    {
        // Arrange
        var registry = ConduitRegistry.Empty;
        var request = new TestRequest(Name: "Test");

        // Act
        var result = await Dispatcher
            .Send<TestRequest, TestResponse>(request: request, registry: registry, logger: _logger)
            .ConfigureAwait(false);

        // Assert
        Assert.True(result is Result<TestResponse, ConduitError>.Error<TestResponse, ConduitError>);
        var error = (Result<TestResponse, ConduitError>.Error<TestResponse, ConduitError>)result;
        Assert.True(error.Value is ConduitErrorNoHandler);
    }

    [Fact]
    public async Task SendWithValidatorValidatesRequest()
    {
        // Arrange
        var registry = ConduitRegistry
            .Empty.AddHandler<TestRequest, TestResponse>(TestHandlers.Handle)
            .AddValidator<TestRequest>(TestHandlers.Validate);

        var request = new TestRequest(Name: ""); // Empty name fails validation

        // Act
        var result = await Dispatcher
            .Send<TestRequest, TestResponse>(request: request, registry: registry, logger: _logger)
            .ConfigureAwait(false);

        // Assert
        Assert.True(result is Result<TestResponse, ConduitError>.Error<TestResponse, ConduitError>);
        var error = (Result<TestResponse, ConduitError>.Error<TestResponse, ConduitError>)result;
        Assert.True(error.Value is ConduitErrorValidation);
    }

    [Fact]
    public async Task SendWithBehaviorExecutesBehavior()
    {
        // Arrange
        var behaviorExecuted = false;
        BehaviorHandler<TestRequest, TestResponse> trackingBehavior = async (
            ctx,
            next,
            logger,
            ct
        ) =>
        {
            behaviorExecuted = true;
            return await next(ctx, ct).ConfigureAwait(false);
        };

        var registry = ConduitRegistry
            .Empty.AddHandler<TestRequest, TestResponse>(TestHandlers.Handle)
            .AddBehavior(trackingBehavior);

        var request = new TestRequest(Name: "Test");

        // Act
        var result = await Dispatcher
            .Send<TestRequest, TestResponse>(request: request, registry: registry, logger: _logger)
            .ConfigureAwait(false);

        // Assert
        Assert.True(behaviorExecuted);
        Assert.True(result is Result<TestResponse, ConduitError>.Ok<TestResponse, ConduitError>);
    }

    [Fact]
    public async Task SendWithMultipleBehaviorsExecutesInOrder()
    {
        // Arrange
        var executionOrder = new List<int>();

        BehaviorHandler<TestRequest, TestResponse> behavior1 = async (ctx, next, logger, ct) =>
        {
            executionOrder.Add(1);
            var result = await next(ctx, ct).ConfigureAwait(false);
            executionOrder.Add(4);
            return result;
        };

        BehaviorHandler<TestRequest, TestResponse> behavior2 = async (ctx, next, logger, ct) =>
        {
            executionOrder.Add(2);
            var result = await next(ctx, ct).ConfigureAwait(false);
            executionOrder.Add(3);
            return result;
        };

        var registry = ConduitRegistry
            .Empty.AddHandler<TestRequest, TestResponse>(TestHandlers.Handle)
            .AddBehavior(behavior1)
            .AddBehavior(behavior2);

        var request = new TestRequest(Name: "Test");

        // Act
        await Dispatcher
            .Send<TestRequest, TestResponse>(request: request, registry: registry, logger: _logger)
            .ConfigureAwait(false);

        // Assert - First registered behavior is outermost
        Assert.Equal(ExpectedOrder, executionOrder);
    }

    [Fact]
    public async Task PublishWithMultipleHandlersNotifiesAll()
    {
        // Arrange
        var handlersCalled = new List<int>();

        NotificationHandler<TestNotification> handler1 = (notification, ct) =>
        {
            handlersCalled.Add(1);
            return Task.FromResult<Result<Unit, ConduitError>>(
                new Result<Unit, ConduitError>.Ok<Unit, ConduitError>(Unit.Value)
            );
        };

        NotificationHandler<TestNotification> handler2 = (notification, ct) =>
        {
            handlersCalled.Add(2);
            return Task.FromResult<Result<Unit, ConduitError>>(
                new Result<Unit, ConduitError>.Ok<Unit, ConduitError>(Unit.Value)
            );
        };

        var registry = ConduitRegistry
            .Empty.AddNotificationHandler(handler1)
            .AddNotificationHandler(handler2);

        var notification = new TestNotification(Message: "Test");

        // Act
        var result = await Dispatcher
            .Publish(notification: notification, registry: registry, logger: _logger)
            .ConfigureAwait(false);

        // Assert
        Assert.True(
            result is Result<NotificationResult, ConduitError>.Ok<NotificationResult, ConduitError>
        );
        var ok = (Result<NotificationResult, ConduitError>.Ok<
            NotificationResult,
            ConduitError
        >)result;
        Assert.Equal(2, ok.Value.HandlerCount);
        Assert.Equal(2, ok.Value.SuccessCount);
        Assert.Empty(ok.Value.Errors);
    }

    [Fact]
    public async Task PublishWithNoHandlersReturnsEmptyResult()
    {
        // Arrange
        var registry = ConduitRegistry.Empty;
        var notification = new TestNotification(Message: "Test");

        // Act
        var result = await Dispatcher
            .Publish(notification: notification, registry: registry, logger: _logger)
            .ConfigureAwait(false);

        // Assert
        Assert.True(
            result is Result<NotificationResult, ConduitError>.Ok<NotificationResult, ConduitError>
        );
        var ok = (Result<NotificationResult, ConduitError>.Ok<
            NotificationResult,
            ConduitError
        >)result;
        Assert.Equal(0, ok.Value.HandlerCount);
    }

    [Fact]
    public async Task SendWithLoggingBehaviorLogsRequestDetails()
    {
        // Arrange
        var testLogger = new TestLogger();
        var registry = ConduitRegistry
            .Empty.AddHandler<TestRequest, TestResponse>(TestHandlers.Handle)
            .AddBehavior(BuiltInBehaviors.Logging<TestRequest, TestResponse>());

        var request = new TestRequest(Name: "Test");

        // Act
        await Dispatcher
            .Send<TestRequest, TestResponse>(
                request: request,
                registry: registry,
                logger: testLogger
            )
            .ConfigureAwait(false);

        // Assert
        Assert.True(testLogger.LoggedMessages.Count >= 2);
    }

    [Fact]
    public async Task SendWithTimeoutBehaviorTimesOut()
    {
        // Arrange
        RequestHandler<TestRequest, TestResponse> slowHandler = async (request, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            return new Result<TestResponse, ConduitError>.Ok<TestResponse, ConduitError>(
                new TestResponse("Done")
            );
        };

        var registry = ConduitRegistry
            .Empty.AddHandler(slowHandler)
            .AddBehavior(
                BuiltInBehaviors.Timeout<TestRequest, TestResponse>(TimeSpan.FromMilliseconds(50))
            );

        var request = new TestRequest(Name: "Test");

        // Act
        var result = await Dispatcher
            .Send<TestRequest, TestResponse>(request: request, registry: registry, logger: _logger)
            .ConfigureAwait(false);

        // Assert
        Assert.True(result is Result<TestResponse, ConduitError>.Error<TestResponse, ConduitError>);
        var error = (Result<TestResponse, ConduitError>.Error<TestResponse, ConduitError>)result;
        Assert.True(error.Value is ConduitErrorTimeout);
    }
}

// Test types - defined as records per CLAUDE.md rules

internal sealed record TestRequest(string Name);

internal sealed record TestResponse(string Message);

internal sealed record TestNotification(string Message);

/// <summary>
/// Static handler methods per CLAUDE.md - no classes, static methods only.
/// </summary>
internal static class TestHandlers
{
    public static Task<Result<TestResponse, ConduitError>> Handle(
        TestRequest request,
        CancellationToken ct
    ) =>
        Task.FromResult<Result<TestResponse, ConduitError>>(
            new Result<TestResponse, ConduitError>.Ok<TestResponse, ConduitError>(
                new TestResponse($"Hello, {request.Name}!")
            )
        );

    public static ImmutableList<string> Validate(TestRequest request) =>
        string.IsNullOrWhiteSpace(request.Name) ? ["Name is required"] : [];
}

/// <summary>
/// Simple test logger that captures log messages.
/// </summary>
internal sealed class TestLogger : ILogger
{
    public List<string> LoggedMessages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    ) => LoggedMessages.Add(formatter(state, exception));
}
