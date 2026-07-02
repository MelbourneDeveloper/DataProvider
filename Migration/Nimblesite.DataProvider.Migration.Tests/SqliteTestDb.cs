using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Shared SQLite E2E test helpers: temp-file databases, schema application
/// via inspect → diff → apply, and row counting.
/// </summary>
internal static class SqliteTestDb
{
    private static readonly ILogger Logger = NullLogger.Instance;

    public static void WithDb(Action<SqliteConnection> test)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sqlitemig_{Guid.NewGuid()}.db");
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        try
        {
            test(connection);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    public static SchemaDefinition Inspect(SqliteConnection connection) =>
        ((SchemaResultOk)SqliteSchemaInspector.Inspect(connection, Logger)).Value;

    public static void ApplySchema(
        SqliteConnection connection,
        SchemaDefinition schema,
        bool allowDestructive = false
    )
    {
        var current = Inspect(connection);
        var ops = (
            (OperationsResultOk)
                SchemaDiff.Calculate(current, schema, allowDestructive, logger: Logger)
        ).Value;
        Apply(connection, ops, allowDestructive ? MigrationOptions.Destructive : null);
    }

    public static void Apply(
        SqliteConnection connection,
        IReadOnlyList<SchemaOperation> ops,
        MigrationOptions? options = null
    )
    {
        var result = MigrationRunner.Apply(
            connection,
            ops,
            SqliteDdlGenerator.Generate,
            options ?? MigrationOptions.Default,
            Logger
        );
        Assert.True(
            result is MigrationApplyResultOk,
            $"Migration failed: {(result as MigrationApplyResultError)?.Value}"
        );
    }

    public static void SetUser(SqliteConnection connection, string userId)
    {
        Execute(connection, "DELETE FROM [__rls_context]");
        Execute(connection, $"INSERT INTO [__rls_context]([current_user_id]) VALUES ('{userId}')");
    }

    public static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public static int CountRows(SqliteConnection connection, string tableName) =>
        Count(connection, $"SELECT COUNT(*) FROM [{tableName}]");

    public static int CountMasterRows(SqliteConnection connection, string type, string name) =>
        Count(
            connection,
            $"SELECT COUNT(*) FROM sqlite_master WHERE type='{type}' AND name='{name}'"
        );

    public static int Count(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
