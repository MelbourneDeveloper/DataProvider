using System.Diagnostics.CodeAnalysis;
using Nimblesite.DataProvider.Core;
using Nimblesite.DataProvider.Core.CodeGeneration;
using Nimblesite.DataProvider.Postgres.CodeGeneration;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Postgres;

// Implements [CON-SHARED-CORE]. Thin shell that constructs a
// Postgres-flavoured CodeGenerationConfig and delegates to
// Core.SqlAntlrCodeGenerator. All orchestration (model/method/source-file
// emission) lives in Core.
/// <summary>
/// Postgres entry point to the shared ANTLR-based code generator.
/// </summary>
[ExcludeFromCodeCoverage]
public static class PostgresCodeGenerator
{
    /// <summary>
    /// Generates C# source for a SQL file using real database metadata.
    /// </summary>
    /// <param name="fileName">The SQL file base name.</param>
    /// <param name="sql">The SQL content.</param>
    /// <param name="statement">The parsed statement metadata.</param>
    /// <param name="columnMetadata">Real column metadata from the database.</param>
    /// <param name="groupingConfig">Optional grouping configuration.</param>
    /// <param name="config">Optional custom code generation configuration.</param>
    /// <returns>Result with generated source or an error.</returns>
    public static Result<string, SqlError> GenerateCodeWithMetadata(
        string fileName,
        string sql,
        SelectStatement statement,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        GroupingConfig? groupingConfig = null,
        CodeGenerationConfig? config = null
    ) =>
        SqlAntlrCodeGenerator.GenerateCodeWithMetadata(
            fileName,
            sql,
            statement,
            columnMetadata,
            config ?? CreateDefaultPostgresConfig(),
            groupingConfig
        );

    /// <summary>
    /// Gets column metadata by executing the SQL query against the database.
    /// </summary>
    /// <param name="connectionString">Postgres connection string.</param>
    /// <param name="sql">SQL query to execute.</param>
    /// <param name="parameters">Query parameters.</param>
    /// <returns>Column metadata on success; <see cref="SqlError"/> on failure.</returns>
    public static Task<
        Result<IReadOnlyList<DatabaseColumn>, SqlError>
    > GetColumnMetadataFromSqlAsync(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    ) =>
        PostgresDatabaseEffects
            .Create()
            .GetColumnMetadataFromSqlAsync(connectionString, sql, parameters);

    private static CodeGenerationConfig CreateDefaultPostgresConfig()
    {
        var databaseEffects = PostgresDatabaseEffects.Create();
        var tableOperationGenerator = new DefaultTableOperationGenerator("NpgsqlConnection");

        return new CodeGenerationConfig(
            databaseEffects.GetColumnMetadataFromSqlAsync,
            tableOperationGenerator
        )
        {
            ConnectionType = "NpgsqlConnection",
            TargetNamespace = "Generated",
        };
    }
}
