using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Outcome;

namespace Conduit;

/// <summary>
/// Pre-built behaviors for common cross-cutting concerns.
/// All behaviors are static methods returning behavior delegates.
/// </summary>
public static class BuiltInBehaviors
{
    /// <summary>
    /// Creates a logging behavior that logs request/response details.
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <returns>A behavior delegate for logging.</returns>
    public static BehaviorHandler<TRequest, TResponse> Logging<TRequest, TResponse>() =>
        async (context, next, logger, ct) =>
        {
            logger.LogInformation(
                "CONDUIT: [{CorrelationId}] START {RequestType}",
                context.CorrelationId,
                context.RequestType
            );

            var stopwatch = Stopwatch.StartNew();
            var result = await next(context, ct).ConfigureAwait(false);
            stopwatch.Stop();

            _ = result switch
            {
                Result<TResponse, ConduitError>.Ok<TResponse, ConduitError> => LogSuccess(
                    logger,
                    context.CorrelationId,
                    context.RequestType,
                    stopwatch.ElapsedMilliseconds
                ),
                Result<TResponse, ConduitError>.Error<TResponse, ConduitError> err => LogError(
                    logger,
                    context.CorrelationId,
                    context.RequestType,
                    stopwatch.ElapsedMilliseconds,
                    err.Value
                ),
            };

            return result;
        };

    private static bool LogSuccess(
        ILogger logger,
        string correlationId,
        string requestType,
        long elapsedMs
    )
    {
        logger.LogInformation(
            "CONDUIT: [{CorrelationId}] SUCCESS {RequestType} in {ElapsedMs}ms",
            correlationId,
            requestType,
            elapsedMs
        );
        return true;
    }

    private static bool LogError(
        ILogger logger,
        string correlationId,
        string requestType,
        long elapsedMs,
        ConduitError error
    )
    {
        logger.LogWarning(
            "CONDUIT: [{CorrelationId}] FAILED {RequestType} in {ElapsedMs}ms: {Error}",
            correlationId,
            requestType,
            elapsedMs,
            error
        );
        return false;
    }

    /// <summary>
    /// Creates a timeout behavior that cancels requests exceeding the timeout.
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>A behavior delegate for timeout.</returns>
    public static BehaviorHandler<TRequest, TResponse> Timeout<TRequest, TResponse>(
        TimeSpan timeout
    ) =>
        async (context, next, logger, ct) =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            try
            {
                return await next(context, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(
                    "CONDUIT: [{CorrelationId}] TIMEOUT {RequestType} after {TimeoutMs}ms",
                    context.CorrelationId,
                    context.RequestType,
                    timeout.TotalMilliseconds
                );

                return new Result<TResponse, ConduitError>.Error<TResponse, ConduitError>(
                    new ConduitErrorTimeout(context.RequestType, (int)timeout.TotalMilliseconds)
                );
            }
        };

    /// <summary>
    /// Creates a validation behavior using a validation function.
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="validate">Function that returns validation errors (empty if valid).</param>
    /// <returns>A behavior delegate for validation.</returns>
    public static BehaviorHandler<TRequest, TResponse> Validation<TRequest, TResponse>(
        Func<TRequest, ImmutableList<string>> validate
    ) =>
        async (context, next, logger, ct) =>
        {
            var errors = validate(context.Request);

            if (!errors.IsEmpty)
            {
                logger.LogWarning(
                    "CONDUIT: [{CorrelationId}] VALIDATION FAILED {RequestType}: {Errors}",
                    context.CorrelationId,
                    context.RequestType,
                    string.Join(", ", errors)
                );

                return new Result<TResponse, ConduitError>.Error<TResponse, ConduitError>(
                    new ConduitErrorValidation(context.RequestType, errors)
                );
            }

            return await next(context, ct).ConfigureAwait(false);
        };

    /// <summary>
    /// Creates a retry behavior with exponential backoff.
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="maxRetries">Maximum number of retry attempts.</param>
    /// <param name="baseDelayMs">Base delay in milliseconds (doubles each retry).</param>
    /// <param name="shouldRetry">Predicate to determine if error is retryable.</param>
    /// <returns>A behavior delegate for retrying.</returns>
    public static BehaviorHandler<TRequest, TResponse> Retry<TRequest, TResponse>(
        int maxRetries = 3,
        int baseDelayMs = 100,
        Func<ConduitError, bool>? shouldRetry = null
    ) =>
        async (context, next, logger, ct) =>
        {
            var retryPredicate = shouldRetry ?? (error => error is ConduitErrorHandlerFailed);
            var attempt = 0;
            Result<TResponse, ConduitError> result;

            do
            {
                result = await next(context, ct).ConfigureAwait(false);

                var shouldRetryResult = result switch
                {
                    Result<TResponse, ConduitError>.Ok<TResponse, ConduitError> => false,
                    Result<TResponse, ConduitError>.Error<TResponse, ConduitError> err => attempt
                        < maxRetries
                        && retryPredicate(err.Value),
                };

                if (!shouldRetryResult)
                {
                    break;
                }

                attempt++;
                var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);

                logger.LogWarning(
                    "CONDUIT: [{CorrelationId}] RETRY {Attempt}/{MaxRetries} for {RequestType} after {DelayMs}ms",
                    context.CorrelationId,
                    attempt,
                    maxRetries,
                    context.RequestType,
                    delay
                );

                await Task.Delay(delay, ct).ConfigureAwait(false);
            } while (attempt <= maxRetries);

            return result;
        };

    /// <summary>
    /// Creates an exception handling behavior that converts exceptions to ConduitError.
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <returns>A behavior delegate for exception handling.</returns>
    public static BehaviorHandler<TRequest, TResponse> ExceptionHandler<TRequest, TResponse>() =>
        async (context, next, logger, ct) =>
        {
            try
            {
                return await next(context, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "CONDUIT: [{CorrelationId}] EXCEPTION in {RequestType}",
                    context.CorrelationId,
                    context.RequestType
                );

                return new Result<TResponse, ConduitError>.Error<TResponse, ConduitError>(
                    ConduitErrorHandlerFailed.FromException(context.RequestType, ex)
                );
            }
        };
}
