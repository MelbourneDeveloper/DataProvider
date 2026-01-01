using Microsoft.Extensions.Logging;
using Outcome;

namespace Conduit;

/// <summary>
/// Handler delegate for requests that return a response.
/// Async-only, returns Result for explicit error handling.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="request">The request to handle.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Result with response or error.</returns>
public delegate Task<Result<TResponse, ConduitError>> RequestHandler<in TRequest, TResponse>(
    TRequest request,
    CancellationToken cancellationToken
);

/// <summary>
/// Handler delegate for notifications (fan-out pattern).
/// Returns Unit on success, error on failure.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
/// <param name="notification">The notification to handle.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Result with Unit or error.</returns>
public delegate Task<Result<Unit, ConduitError>> NotificationHandler<in TNotification>(
    TNotification notification,
    CancellationToken cancellationToken
);

/// <summary>
/// The next delegate in the middleware chain.
/// Behaviors call this to continue processing.
/// </summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
/// <param name="context">The pipeline context.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Result with response or error.</returns>
public delegate Task<Result<TResponse, ConduitError>> NextHandler<TRequest, TResponse>(
    PipelineContext<TRequest, TResponse> context,
    CancellationToken cancellationToken
);

/// <summary>
/// Behavior delegate that wraps handler execution.
/// Can inspect/modify context, short-circuit, or add cross-cutting behavior.
/// </summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
/// <param name="context">The pipeline context.</param>
/// <param name="next">The next delegate to call (or not, to short-circuit).</param>
/// <param name="logger">Logger for behavior operations.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Result with response or error.</returns>
public delegate Task<Result<TResponse, ConduitError>> BehaviorHandler<TRequest, TResponse>(
    PipelineContext<TRequest, TResponse> context,
    NextHandler<TRequest, TResponse> next,
    ILogger logger,
    CancellationToken cancellationToken
);
