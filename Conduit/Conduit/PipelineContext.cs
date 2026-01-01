using System.Collections.Immutable;

namespace Conduit;

/// <summary>
/// Immutable context passed through the pipeline.
/// Contains the request and metadata for behaviors to use.
/// </summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
/// <param name="Request">The request being processed.</param>
/// <param name="RequestType">Full type name of the request.</param>
/// <param name="StartTime">When processing started.</param>
/// <param name="CorrelationId">Unique ID for tracing.</param>
/// <param name="Properties">Bag of additional properties for behaviors to share.</param>
public sealed record PipelineContext<TRequest, TResponse>(
    TRequest Request,
    string RequestType,
    DateTimeOffset StartTime,
    string CorrelationId,
    ImmutableDictionary<string, object> Properties
);

/// <summary>
/// Static helper methods for PipelineContext.
/// </summary>
public static class PipelineContext
{
    /// <summary>
    /// Creates a new pipeline context for a request.
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="request">The request to process.</param>
    /// <returns>New pipeline context.</returns>
    public static PipelineContext<TRequest, TResponse> Create<TRequest, TResponse>(
        TRequest request
    ) =>
        new(
            Request: request,
            RequestType: typeof(TRequest).FullName ?? typeof(TRequest).Name,
            StartTime: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid().ToString(),
            Properties: ImmutableDictionary<string, object>.Empty
        );

    /// <summary>
    /// Adds a property to the pipeline context (returns new immutable context).
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="context">The current context.</param>
    /// <param name="key">Property key.</param>
    /// <param name="value">Property value.</param>
    /// <returns>New context with the property added.</returns>
    public static PipelineContext<TRequest, TResponse> WithProperty<TRequest, TResponse>(
        PipelineContext<TRequest, TResponse> context,
        string key,
        object value
    ) => context with { Properties = context.Properties.SetItem(key, value) };

    /// <summary>
    /// Gets a property from the context.
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <typeparam name="TValue">Property value type.</typeparam>
    /// <param name="context">The context.</param>
    /// <param name="key">Property key.</param>
    /// <returns>The property value or default if not found.</returns>
    public static TValue? GetProperty<TRequest, TResponse, TValue>(
        PipelineContext<TRequest, TResponse> context,
        string key
    ) =>
        context.Properties.TryGetValue(key, out var value) && value is TValue typedValue
            ? typedValue
            : default;
}
