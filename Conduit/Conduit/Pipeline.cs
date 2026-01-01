using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Conduit;

/// <summary>
/// Static methods for composing behaviors into a pipeline.
/// Pure functions, no mutable state.
/// </summary>
public static class Pipeline
{
    /// <summary>
    /// Composes a list of behaviors into a single executable delegate.
    /// Behaviors are executed in order (first registered = outermost).
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="behaviors">List of behaviors to compose.</param>
    /// <param name="handler">The innermost handler.</param>
    /// <param name="logger">Logger passed to all behaviors.</param>
    /// <returns>A composed delegate that executes the full pipeline.</returns>
    public static NextHandler<TRequest, TResponse> Compose<TRequest, TResponse>(
        ImmutableList<BehaviorHandler<TRequest, TResponse>> behaviors,
        RequestHandler<TRequest, TResponse> handler,
        ILogger logger
    )
    {
        // Start with the innermost handler wrapped as NextHandler
        NextHandler<TRequest, TResponse> current = (ctx, ct) => handler(ctx.Request, ct);

        // Wrap each behavior around the current delegate (reverse order so first added is outermost)
        foreach (var behavior in behaviors.Reverse())
        {
            var next = current;
            current = (ctx, ct) => behavior(ctx, next, logger, ct);
        }

        return current;
    }

    /// <summary>
    /// Filters global behaviors to get only those matching the request/response types.
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="globalBehaviors">All registered global behaviors.</param>
    /// <returns>Behaviors that match the types.</returns>
    public static ImmutableList<BehaviorHandler<TRequest, TResponse>> GetTypedBehaviors<
        TRequest,
        TResponse
    >(ImmutableList<Delegate> globalBehaviors) =>
        [.. globalBehaviors.OfType<BehaviorHandler<TRequest, TResponse>>()];
}
