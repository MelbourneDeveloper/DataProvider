namespace Nimblesite.DataProvider.Migration.Core;

// Implements [MIG-TRIGGER-SQLITE] and [MIG-TRIGGER-PG] shared DDL support
// from docs/specs/declarative-triggers-spec.md (GitHub issue 82).

/// <summary>
/// Shared helpers for platform trigger DDL builders: validation, error
/// message defaulting/escaping, and keyword mapping.
/// </summary>
public static class TriggerDdlSupport
{
    /// <summary>Returns the guard predicate or throws when missing.</summary>
    public static string RequireRaiseWhen(TriggerDefinition trigger) =>
        trigger.RaiseWhenLql is { } lql && !string.IsNullOrWhiteSpace(lql)
            ? lql
            : throw new InvalidOperationException(
                $"Trigger '{trigger.Name}' has no raiseWhen predicate"
            );

    /// <summary>Returns the distinct declared events or throws when empty.</summary>
    public static IReadOnlyList<TriggerEvent> RequireEvents(TriggerDefinition trigger) =>
        trigger.Events.Count > 0
            ? trigger.Events.Distinct().ToList()
            : throw new InvalidOperationException($"Trigger '{trigger.Name}' declares no events");

    /// <summary>
    /// Error message (defaulted from the trigger name when absent) with
    /// single quotes escaped for embedding in a SQL string literal.
    /// </summary>
    public static string EscapedMessage(TriggerDefinition trigger)
    {
        var message = string.IsNullOrWhiteSpace(trigger.ErrorMessage)
            ? $"TRIGGER-GUARD: {trigger.Name}"
            : trigger.ErrorMessage;
        return message.Replace("'", "''", StringComparison.Ordinal);
    }

    /// <summary>SQL verb for a trigger event.</summary>
    public static string EventVerb(TriggerEvent triggerEvent) =>
        triggerEvent switch
        {
            TriggerEvent.Insert => "INSERT",
            TriggerEvent.Update => "UPDATE",
            TriggerEvent.Delete => "DELETE",
            _ => throw new NotSupportedException($"Unknown trigger event: {triggerEvent}"),
        };

    /// <summary>SQL timing keyword for a trigger timing.</summary>
    public static string TimingKeyword(TriggerTiming timing) =>
        timing == TriggerTiming.After ? "AFTER" : "BEFORE";
}
