namespace Nimblesite.DataProvider.Migration.SQLite;

// Implements [MIG-TRIGGER-SQLITE] read-back from
// docs/specs/declarative-triggers-spec.md (GitHub issue 82).

/// <summary>
/// Reads declarative trigger guards back from <c>sqlite_master</c> so the
/// diff can match them by name. Only migration-managed <c>usr_</c> triggers
/// are considered; <c>rls_</c> triggers belong to the RLS inspector.
/// </summary>
internal static class SqliteTriggerSchemaInspector
{
    private static readonly string[] EventTokens = ["insert", "update", "delete"];

    public static IReadOnlyList<TriggerDefinition> Inspect(
        SqliteConnection connection,
        string tableName
    ) =>
        SqliteTriggerNames
            .Read(connection, tableName, $"usr_%_{tableName}")
            .Select(name => SqliteTriggerNames.Parse(name, "usr_", tableName, EventTokens))
            .OfType<ParsedTriggerName>()
            .GroupBy(p => p.BaseName, StringComparer.OrdinalIgnoreCase)
            .Select(ToTrigger)
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static TriggerDefinition ToTrigger(IGrouping<string, ParsedTriggerName> group) =>
        new()
        {
            Name = group.Key,
            Events = group.Select(p => ToEvent(p.EventToken)).Distinct().ToList(),
        };

    private static TriggerEvent ToEvent(string eventToken) =>
        eventToken switch
        {
            "insert" => TriggerEvent.Insert,
            "update" => TriggerEvent.Update,
            _ => TriggerEvent.Delete,
        };
}
