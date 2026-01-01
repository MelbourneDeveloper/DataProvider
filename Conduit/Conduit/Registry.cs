using System.Collections.Immutable;

namespace Conduit;

/// <summary>
/// Immutable registry of handlers, behaviors, and validators.
/// All modifications return new instances.
/// </summary>
/// <param name="RequestHandlers">Map of request type -> handler delegate.</param>
/// <param name="NotificationHandlers">Map of notification type -> list of handlers.</param>
/// <param name="GlobalBehaviors">List of global behaviors (applied to all requests).</param>
/// <param name="Validators">Map of request type -> validator delegate.</param>
public sealed record ConduitRegistry(
    ImmutableDictionary<Type, Delegate> RequestHandlers,
    ImmutableDictionary<Type, ImmutableList<Delegate>> NotificationHandlers,
    ImmutableList<Delegate> GlobalBehaviors,
    ImmutableDictionary<Type, Delegate> Validators
)
{
    /// <summary>
    /// Creates an empty registry.
    /// </summary>
    public static ConduitRegistry Empty =>
        new(
            ImmutableDictionary<Type, Delegate>.Empty,
            ImmutableDictionary<Type, ImmutableList<Delegate>>.Empty,
            [],
            ImmutableDictionary<Type, Delegate>.Empty
        );
}

/// <summary>
/// Static methods for building registries functionally.
/// All methods return new immutable instances.
/// </summary>
public static class RegistryBuilder
{
    /// <summary>
    /// Registers a request handler.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="registry">The current registry.</param>
    /// <param name="handler">The handler to register.</param>
    /// <returns>New registry with the handler added.</returns>
    public static ConduitRegistry AddHandler<TRequest, TResponse>(
        this ConduitRegistry registry,
        RequestHandler<TRequest, TResponse> handler
    ) =>
        registry with
        {
            RequestHandlers = registry.RequestHandlers.SetItem(typeof(TRequest), handler),
        };

    /// <summary>
    /// Registers a notification handler (multiple handlers per notification allowed).
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="registry">The current registry.</param>
    /// <param name="handler">The handler to register.</param>
    /// <returns>New registry with the handler added.</returns>
    public static ConduitRegistry AddNotificationHandler<TNotification>(
        this ConduitRegistry registry,
        NotificationHandler<TNotification> handler
    )
    {
        var existing = registry.NotificationHandlers.TryGetValue(
            typeof(TNotification),
            out var handlers
        )
            ? handlers
            : [];

        return registry with
        {
            NotificationHandlers = registry.NotificationHandlers.SetItem(
                typeof(TNotification),
                existing.Add(handler)
            ),
        };
    }

    /// <summary>
    /// Registers a global behavior (applies to all requests).
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="registry">The current registry.</param>
    /// <param name="behavior">The behavior to register.</param>
    /// <returns>New registry with the behavior added.</returns>
    public static ConduitRegistry AddBehavior<TRequest, TResponse>(
        this ConduitRegistry registry,
        BehaviorHandler<TRequest, TResponse> behavior
    ) => registry with { GlobalBehaviors = registry.GlobalBehaviors.Add(behavior) };

    /// <summary>
    /// Registers a validator for a request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="registry">The current registry.</param>
    /// <param name="validator">The validation function.</param>
    /// <returns>New registry with the validator added.</returns>
    public static ConduitRegistry AddValidator<TRequest>(
        this ConduitRegistry registry,
        Func<TRequest, ImmutableList<string>> validator
    ) => registry with { Validators = registry.Validators.SetItem(typeof(TRequest), validator) };
}
