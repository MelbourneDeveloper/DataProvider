using GuardTranspileError = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;
using GuardTranspileOk = Outcome.Result<
    string,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<string, Nimblesite.DataProvider.Migration.Core.MigrationError>;

namespace Nimblesite.DataProvider.Migration.Postgres;

// Implements [MIG-TRIGGER-PG] from docs/specs/declarative-triggers-spec.md
// (GitHub issue 82).

/// <summary>
/// Emits PostgreSQL DDL for declarative trigger guards: one plpgsql guard
/// function (<c>{table}_{trigger}_trgfn</c>) that raises the declared error
/// when the guard predicate holds, plus one trigger wired to the declared
/// events. <c>RAISE ... USING MESSAGE</c> avoids format-string
/// interpretation of <c>%</c> in user messages.
/// </summary>
internal static class PostgresTriggerDdlBuilder
{
    public static string GenerateCreate(CreateTriggerOperation op)
    {
        var predicate = Translate(TriggerDdlSupport.RequireRaiseWhen(op.Trigger), op.Trigger.Name);
        var functionName = FunctionName(op.TableName, op.Trigger.Name);
        var events = string.Join(
            " OR ",
            TriggerDdlSupport.RequireEvents(op.Trigger).Select(TriggerDdlSupport.EventVerb)
        );
        var level = op.Trigger.ForEachRow ? "ROW" : "STATEMENT";
        return $"""
            CREATE OR REPLACE FUNCTION "{op.Schema}"."{functionName}"()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $trigger_guard$
            BEGIN
              IF {predicate} THEN
                RAISE EXCEPTION USING MESSAGE = '{TriggerDdlSupport.EscapedMessage(op.Trigger)}';
              END IF;
              IF TG_OP = 'DELETE' THEN
                RETURN OLD;
              END IF;
              RETURN NEW;
            END
            $trigger_guard$;
            DROP TRIGGER IF EXISTS "{op.Trigger.Name}" ON "{op.Schema}"."{op.TableName}";
            CREATE TRIGGER "{op.Trigger.Name}"
            {TriggerDdlSupport.TimingKeyword(
                op.Trigger.Timing
            )} {events} ON "{op.Schema}"."{op.TableName}"
            FOR EACH {level}
            EXECUTE FUNCTION "{op.Schema}"."{functionName}"()
            """;
    }

    public static string GenerateDrop(DropTriggerOperation op) =>
        $"""
            DROP TRIGGER IF EXISTS "{op.TriggerName}" ON "{op.Schema}"."{op.TableName}";
            DROP FUNCTION IF EXISTS "{op.Schema}"."{FunctionName(op.TableName, op.TriggerName)}"()
            """;

    private static string Translate(string lql, string triggerName)
    {
        var result = RlsPredicateTranspiler.TranslateGuardPredicate(
            lql,
            RlsPlatform.Postgres,
            triggerName
        );
        return result switch
        {
            GuardTranspileOk ok => ok.Value,
            GuardTranspileError error => throw new InvalidOperationException(error.Value.Message),
        };
    }

    private static string FunctionName(string tableName, string triggerName) =>
        $"{tableName}_{triggerName}_trgfn";
}
