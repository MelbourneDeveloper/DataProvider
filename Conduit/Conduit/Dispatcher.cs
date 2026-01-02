using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Outcome;

namespace Conduit;

/// <summary>
/// Result of publishing a notification to all handlers.
/// </summary>
/// <param name="HandlerCount">Number of handlers that received the notification.</param>
/// <param name="SuccessCount">Number of handlers that succeeded.</param>
/// <param name="Errors">List of errors from failed handlers (non-fatal).</param>
public sealed record NotificationResult(
    int HandlerCount,
    int SuccessCount,
    ImmutableList<ConduitErrorNotificationHandler> Errors
);

/// <summary>
/// Dispatches requests to registered handlers through the behavior pipeline.
/// All functions are static with explicit dependencies.
/// </summary>
public static class Dispatcher
{
    /// <summary>
    /// Sends a request through the pipeline and returns the result.
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="registry">The handler registry.</param>
    /// <param name="logger">Logger for dispatch operations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with response or error.</returns>
    public static async Task<Result<TResponse, ConduitError>> Send<TRequest, TResponse>(
        TRequest request,
        ConduitRegistry registry,
        ILogger logger,
        CancellationToken cancellationToken = default
    )
    {
        var requestTypeName = typeof(TRequest).FullName ?? typeof(TRequest).Name;

        logger.LogDebug("CONDUIT: Dispatching request {RequestType}", typeof(TRequest).Name);

        // Look up handler
        if (!registry.RequestHandlers.TryGetValue(typeof(TRequest), out var handlerDelegate))
        {
            logger.LogError("CONDUIT: No handler registered for {RequestType}", requestTypeName);
            return new Result<TResponse, ConduitError>.Error<TResponse, ConduitError>(
                new ConduitErrorNoHandler(requestTypeName)
            );
        }

        var handler = (RequestHandler<TRequest, TResponse>)handlerDelegate;

        // Run validation if registered
        var validationResult = RunValidation(request, registry, logger);
        if (
            validationResult is Result<Unit, ConduitError>.Error<Unit, ConduitError> validationError
        )
        {
            return new Result<TResponse, ConduitError>.Error<TResponse, ConduitError>(
                validationError.Value
            );
        }

        // Create context
        var context = PipelineContext.Create<TRequest, TResponse>(request);

        // Get all behaviors (global configs + typed) and compose pipeline
        var behaviors = Pipeline.GetAllBehaviors<TRequest, TResponse>(registry);
        var pipeline = Pipeline.Compose(behaviors, handler, logger);

        try
        {
            logger.LogDebug(
                "CONDUIT: Executing pipeline with {BehaviorCount} behaviors",
                behaviors.Count
            );

            return await pipeline(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("CONDUIT: Request cancelled {RequestType}", typeof(TRequest).Name);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "CONDUIT: Handler exception for {RequestType}",
                typeof(TRequest).Name
            );
            return new Result<TResponse, ConduitError>.Error<TResponse, ConduitError>(
                ConduitErrorHandlerFailed.FromException(requestTypeName, ex)
            );
        }
    }

    /// <summary>
    /// Publishes a notification to all registered handlers.
    /// Handlers execute in parallel, errors are collected but don't stop other handlers.
    /// </summary>
    /// <typeparam name="TNotification">Notification type.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="registry">The handler registry.</param>
    /// <param name="logger">Logger for publish operations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with notification result containing success/error counts.</returns>
    public static async Task<Result<NotificationResult, ConduitError>> Publish<TNotification>(
        TNotification notification,
        ConduitRegistry registry,
        ILogger logger,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug(
            "CONDUIT: Publishing notification {NotificationType}",
            typeof(TNotification).Name
        );

        if (
            !registry.NotificationHandlers.TryGetValue(
                typeof(TNotification),
                out var handlerDelegates
            )
        )
        {
            logger.LogDebug(
                "CONDUIT: No handlers for notification {NotificationType}",
                typeof(TNotification).Name
            );
            return new Result<NotificationResult, ConduitError>.Ok<
                NotificationResult,
                ConduitError
            >(new NotificationResult(HandlerCount: 0, SuccessCount: 0, Errors: []));
        }

        var handlers = handlerDelegates
            .Select(d => (NotificationHandler<TNotification>)d)
            .ToImmutableList();

        logger.LogInformation("CONDUIT: Publishing to {HandlerCount} handlers", handlers.Count);

        var tasks = handlers.Select(
            async (handler, index) =>
            {
                try
                {
                    var result = await handler(notification, cancellationToken)
                        .ConfigureAwait(false);
                    return result switch
                    {
                        Result<Unit, ConduitError>.Ok<Unit, ConduitError> => (
                            Success: true,
                            Error: null
                        ),
                        Result<Unit, ConduitError>.Error<Unit, ConduitError> err => (
                            Success: false,
                            Error: (ConduitErrorNotificationHandler?)
                                new ConduitErrorNotificationHandler(
                                    typeof(TNotification).Name,
                                    index,
                                    err.Value.ToString() ?? "Unknown error"
                                )
                        ),
                    };
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "CONDUIT: Notification handler {Index} threw exception",
                        index
                    );
                    return (
                        Success: false,
                        Error: (ConduitErrorNotificationHandler?)
                            new ConduitErrorNotificationHandler(
                                typeof(TNotification).Name,
                                index,
                                ex.Message
                            )
                    );
                }
            }
        );

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var successCount = results.Count(r => r.Success);
        var errors = results
            .Where(r => r.Error is not null)
            .Select(r => r.Error!)
            .ToImmutableList();

        logger.LogInformation(
            "CONDUIT: Published to {Total} handlers, {Success} succeeded, {Failed} failed",
            handlers.Count,
            successCount,
            errors.Count
        );

        return new Result<NotificationResult, ConduitError>.Ok<NotificationResult, ConduitError>(
            new NotificationResult(handlers.Count, successCount, errors)
        );
    }

    private static Result<Unit, ConduitError> RunValidation<TRequest>(
        TRequest request,
        ConduitRegistry registry,
        ILogger logger
    )
    {
        if (!registry.Validators.TryGetValue(typeof(TRequest), out var validatorDelegate))
        {
            return new Result<Unit, ConduitError>.Ok<Unit, ConduitError>(Unit.Value);
        }

        logger.LogDebug("CONDUIT: Running validation for {RequestType}", typeof(TRequest).Name);

        var validator = (Func<TRequest, ImmutableList<string>>)validatorDelegate;
        var errors = validator(request);

        if (errors.IsEmpty)
        {
            return new Result<Unit, ConduitError>.Ok<Unit, ConduitError>(Unit.Value);
        }

        logger.LogWarning(
            "CONDUIT: Validation failed for {RequestType}: {Errors}",
            typeof(TRequest).Name,
            string.Join(", ", errors)
        );

        return new Result<Unit, ConduitError>.Error<Unit, ConduitError>(
            new ConduitErrorValidation(typeof(TRequest).Name, errors)
        );
    }
}
