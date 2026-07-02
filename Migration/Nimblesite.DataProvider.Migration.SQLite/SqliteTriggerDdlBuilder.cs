using GuardTranspileError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using GuardTranspileOk = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;

namespace Nimblesite.DataProvider.Migration.SQLite;

// Implements [MIG-TRIGGER-SQLITE] from docs/specs/declarative-triggers-spec.md
// (GitHub issue 82).

/// <summary>
/// Emits SQLite DDL for declarative trigger guards. One trigger per declared
/// event, named <c>usr_{event}_{trigger}_{table}</c> so the inspector can
/// read the definition back by name. SQLite only supports row-level
/// triggers, so <c>FOR EACH ROW</c> is always emitted.
/// </summary>
internal static class SqliteTriggerDdlBuilder
{
    private static readonly string[] EventTokens = ["insert", "update", "delete"];

    public static string GenerateCreate(CreateTriggerOperation op)
    {
        var predicate = Translate(TriggerDdlSupport.RequireRaiseWhen(op.Trigger), op.Trigger.Name);
        var message = TriggerDdlSupport.EscapedMessage(op.Trigger);
        return string.Join(
            ";\n",
            TriggerDdlSupport
                .RequireEvents(op.Trigger)
                .Select(triggerEvent => Trigger(op, triggerEvent, predicate, message))
        );
    }

    public static string GenerateDrop(DropTriggerOperation op) =>
        string.Join(
            ";\n",
            EventTokens.Select(token =>
                $"DROP TRIGGER IF EXISTS [{TriggerName(token, op.TriggerName, op.TableName)}]"
            )
        );

    private static string Trigger(
        CreateTriggerOperation op,
        TriggerEvent triggerEvent,
        string predicate,
        string message
    )
    {
        var verb = TriggerDdlSupport.EventVerb(triggerEvent);
        var name = TriggerName(verb.ToLowerInvariant(), op.Trigger.Name, op.TableName);
        return $"""
            CREATE TRIGGER IF NOT EXISTS [{name}]
            {TriggerDdlSupport.TimingKeyword(op.Trigger.Timing)} {verb} ON [{op.TableName}]
            FOR EACH ROW
            BEGIN
              SELECT RAISE(ABORT, '{message}')
              WHERE {predicate};
            END
            """;
    }

    private static string Translate(string lql, string triggerName)
    {
        var result = RlsPredicateTranspiler.TranslateGuardPredicate(
            lql,
            RlsPlatform.Sqlite,
            triggerName
        );
        return result switch
        {
            GuardTranspileOk ok => ok.Value,
            GuardTranspileError error => throw new InvalidOperationException(error.Value.Message),
        };
    }

    private static string TriggerName(string eventToken, string triggerName, string tableName) =>
        $"usr_{eventToken}_{triggerName}_{tableName}";
}
