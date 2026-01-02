using System.Collections.Immutable;

namespace Conduit;

/// <summary>
/// Represents a global behavior that applies to all request types.
/// </summary>
public enum GlobalBehaviorType
{
    /// <summary>Logs request start/end with correlation ID and timing.</summary>
    Logging,

    /// <summary>Catches exceptions and converts to ConduitError.</summary>
    ExceptionHandler,

    /// <summary>Applies timeout to requests.</summary>
    Timeout,

    /// <summary>Retries failed requests with exponential backoff.</summary>
    Retry,
}

/// <summary>
/// Configuration for a global behavior.
/// </summary>
/// <param name="Type">The type of behavior.</param>
/// <param name="TimeoutDuration">Timeout duration (for Timeout behavior).</param>
/// <param name="MaxRetries">Max retry attempts (for Retry behavior).</param>
/// <param name="RetryBaseDelayMs">Base delay in ms for retries (for Retry behavior).</param>
public sealed record GlobalBehaviorConfig(
    GlobalBehaviorType Type,
    TimeSpan? TimeoutDuration = null,
    int? MaxRetries = null,
    int? RetryBaseDelayMs = null
);

/// <summary>
/// Immutable registry of handlers, behaviors, and validators.
/// All modifications return new instances.
/// </summary>
/// <param name="RequestHandlers">Map of request type -> handler delegate.</param>
/// <param name="NotificationHandlers">Map of notification type -> list of handlers.</param>
/// <param name="GlobalBehaviors">List of global behaviors (applied to all requests).</param>
/// <param name="GlobalBehaviorConfigs">List of global behavior configurations that auto-apply to all handlers.</param>
/// <param name="Validators">Map of request type -> validator delegate.</param>
public sealed record ConduitRegistry(
    ImmutableDictionary<Type, Delegate> RequestHandlers,
    ImmutableDictionary<Type, ImmutableList<Delegate>> NotificationHandlers,
    ImmutableList<Delegate> GlobalBehaviors,
    ImmutableList<GlobalBehaviorConfig> GlobalBehaviorConfigs,
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

    /// <summary>
    /// Adds global logging behavior that applies to ALL handlers automatically.
    /// </summary>
    /// <param name="registry">The current registry.</param>
    /// <returns>New registry with logging enabled globally.</returns>
    public static ConduitRegistry WithGlobalLogging(this ConduitRegistry registry) =>
        registry with
        {
            GlobalBehaviorConfigs = registry.GlobalBehaviorConfigs.Add(
                new GlobalBehaviorConfig(GlobalBehaviorType.Logging)
            ),
        };

    /// <summary>
    /// Adds global exception handling behavior that applies to ALL handlers automatically.
    /// Converts exceptions to ConduitError instead of throwing.
    /// </summary>
    /// <param name="registry">The current registry.</param>
    /// <returns>New registry with exception handling enabled globally.</returns>
    public static ConduitRegistry WithGlobalExceptionHandling(this ConduitRegistry registry) =>
        registry with
        {
            GlobalBehaviorConfigs = registry.GlobalBehaviorConfigs.Add(
                new GlobalBehaviorConfig(GlobalBehaviorType.ExceptionHandler)
            ),
        };

    /// <summary>
    /// Adds global timeout behavior that applies to ALL handlers automatically.
    /// </summary>
    /// <param name="registry">The current registry.</param>
    /// <param name="timeout">The timeout duration for all requests.</param>
    /// <returns>New registry with timeout enabled globally.</returns>
    public static ConduitRegistry WithGlobalTimeout(
        this ConduitRegistry registry,
        TimeSpan timeout
    ) =>
        registry with
        {
            GlobalBehaviorConfigs = registry.GlobalBehaviorConfigs.Add(
                new GlobalBehaviorConfig(GlobalBehaviorType.Timeout, TimeoutDuration: timeout)
            ),
        };

    /// <summary>
    /// Adds global retry behavior that applies to ALL handlers automatically.
    /// Uses exponential backoff for failed requests.
    /// </summary>
    /// <param name="registry">The current registry.</param>
    /// <param name="maxRetries">Maximum number of retry attempts.</param>
    /// <param name="baseDelayMs">Base delay in milliseconds (doubles each retry).</param>
    /// <returns>New registry with retry enabled globally.</returns>
    public static ConduitRegistry WithGlobalRetry(
        this ConduitRegistry registry,
        int maxRetries = 3,
        int baseDelayMs = 100
    ) =>
        registry with
        {
            GlobalBehaviorConfigs = registry.GlobalBehaviorConfigs.Add(
                new GlobalBehaviorConfig(
                    GlobalBehaviorType.Retry,
                    MaxRetries: maxRetries,
                    RetryBaseDelayMs: baseDelayMs
                )
            ),
        };
}
