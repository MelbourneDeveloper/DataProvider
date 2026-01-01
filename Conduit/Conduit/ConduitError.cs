using System.Collections.Immutable;

namespace Conduit;

/// <summary>
/// Base type for Conduit pipeline errors. Use pattern matching on derived types.
/// Closed type hierarchy - no external inheritance allowed.
/// </summary>
public abstract record ConduitError
{
    /// <summary>
    /// Prevents external inheritance - this makes the type hierarchy "closed".
    /// </summary>
    private protected ConduitError() { }
}

/// <summary>
/// No handler was registered for the given request type.
/// </summary>
/// <param name="RequestType">The full type name of the unhandled request.</param>
public sealed record ConduitErrorNoHandler(string RequestType) : ConduitError;

/// <summary>
/// A handler threw an exception during execution.
/// </summary>
/// <param name="RequestType">The request type that caused the error.</param>
/// <param name="Message">Exception message.</param>
/// <param name="StackTrace">Stack trace for debugging.</param>
public sealed record ConduitErrorHandlerFailed(
    string RequestType,
    string Message,
    string? StackTrace
) : ConduitError
{
    /// <summary>
    /// Creates a ConduitErrorHandlerFailed from an exception.
    /// </summary>
    /// <param name="requestType">The request type name.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <returns>A new ConduitErrorHandlerFailed.</returns>
    public static ConduitErrorHandlerFailed FromException(
        string requestType,
        Exception exception
    ) => new(requestType, exception.Message, exception.StackTrace);
}

/// <summary>
/// Validation failed for the request.
/// </summary>
/// <param name="RequestType">The request type that failed validation.</param>
/// <param name="Errors">List of validation error messages.</param>
public sealed record ConduitErrorValidation(string RequestType, ImmutableList<string> Errors)
    : ConduitError;

/// <summary>
/// A behavior/middleware rejected the request.
/// </summary>
/// <param name="BehaviorName">Name of the behavior that rejected.</param>
/// <param name="Reason">Reason for rejection.</param>
public sealed record ConduitErrorBehaviorRejection(string BehaviorName, string Reason)
    : ConduitError;

/// <summary>
/// Request processing timed out.
/// </summary>
/// <param name="RequestType">The request type that timed out.</param>
/// <param name="TimeoutMs">Timeout in milliseconds.</param>
public sealed record ConduitErrorTimeout(string RequestType, int TimeoutMs) : ConduitError;

/// <summary>
/// A notification handler failed (non-fatal, other handlers continue).
/// </summary>
/// <param name="NotificationType">The notification type.</param>
/// <param name="HandlerIndex">Index of the failed handler.</param>
/// <param name="Message">Error message.</param>
public sealed record ConduitErrorNotificationHandler(
    string NotificationType,
    int HandlerIndex,
    string Message
) : ConduitError;
