namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Shared PostgreSQL E2E test helper: applies a desired schema via
/// inspect → diff → apply and asserts the migration succeeded.
/// </summary>
internal static class PostgresTestDb
{
    public static void ApplySchema(
        NpgsqlConnection connection,
        SchemaDefinition desired,
        ILogger logger,
        bool allowDestructive = false
    )
    {
        var current = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(connection, "public", logger)
        ).Value;
        var ops = (
            (OperationsResultOk)
                SchemaDiff.Calculate(current, desired, allowDestructive, logger: logger)
        ).Value;

        var apply = MigrationRunner.Apply(
            connection,
            ops,
            PostgresDdlGenerator.Generate,
            allowDestructive ? MigrationOptions.Destructive : MigrationOptions.Default,
            logger
        );
        Assert.True(
            apply is MigrationApplyResultOk,
            $"Migration failed: {(apply as MigrationApplyResultError)?.Value}"
        );
    }
}
