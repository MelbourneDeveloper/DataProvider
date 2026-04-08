using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

// Implements [CON-SHARED-CORE]. Dialect-agnostic codegen orchestration lifted
// from SqliteCodeGenerator.GenerateCodeWithMetadata + the grouped variant.
// Takes a CodeGenerationConfig (which already carries every dialect-specific
// function) and delegates the actual emission to it.
/// <summary>
/// Generic entry point that orchestrates the three codegen phases (model,
/// data access method, source file) using a
/// <see cref="CodeGenerationConfig"/> supplied by the platform library. The
/// SQLite and Postgres code generators both reduce to ~30-line shells around
/// this class.
/// </summary>
public static class SqlAntlrCodeGenerator
{
    /// <summary>
    /// Generates C# source for a SQL file using already-resolved column metadata.
    /// </summary>
    /// <param name="fileName">The SQL file base name (used for type + method names).</param>
    /// <param name="sql">The SQL content.</param>
    /// <param name="statement">The parsed statement metadata (parameters, etc.).</param>
    /// <param name="columnMetadata">Column metadata returned by the driver.</param>
    /// <param name="config">The dialect-specific code generation config.</param>
    /// <param name="groupingConfig">Optional grouping for parent-child shaping.</param>
    /// <returns>Generated source on success, an <see cref="SqlError"/> on failure.</returns>
    public static Result<string, SqlError> GenerateCodeWithMetadata(
        string fileName,
        string sql,
        SelectStatement statement,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        CodeGenerationConfig config,
        GroupingConfig? groupingConfig = null
    )
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Fail("fileName cannot be null or empty");
        if (string.IsNullOrWhiteSpace(sql))
            return Fail("sql cannot be null or empty");
        if (statement == null)
            return Fail("statement cannot be null");
        if (columnMetadata == null)
            return Fail("columnMetadata cannot be null");
        if (config == null)
            return Fail("config cannot be null");

        return groupingConfig != null
            ? GenerateGroupedVersionWithMetadata(
                fileName,
                sql,
                statement,
                columnMetadata,
                groupingConfig,
                config
            )
            : GenerateStandardVersion(fileName, sql, statement, columnMetadata, config);
    }

    private static Result<string, SqlError> GenerateStandardVersion(
        string fileName,
        string sql,
        SelectStatement statement,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        CodeGenerationConfig config
    )
    {
        var modelResult = config.GenerateModelType(fileName, columnMetadata);
        if (modelResult is Result<string, SqlError>.Error<string, SqlError> modelFailure)
            return modelFailure;

        var className = $"{fileName}Extensions";
        var dataAccessResult = config.GenerateDataAccessMethod(
            className,
            fileName,
            sql,
            statement.Parameters.ToList().AsReadOnly(),
            columnMetadata,
            config.ConnectionType
        );
        if (
            dataAccessResult
            is Result<string, SqlError>.Error<string, SqlError> dataAccessFailure
        )
            return dataAccessFailure;

        return config.GenerateSourceFile(
            config.TargetNamespace,
            ((Result<string, SqlError>.Ok<string, SqlError>)modelResult).Value,
            ((Result<string, SqlError>.Ok<string, SqlError>)dataAccessResult).Value
        );
    }

    private static Result<string, SqlError> GenerateGroupedVersionWithMetadata(
        string fileName,
        string sql,
        SelectStatement statement,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        GroupingConfig groupingConfig,
        CodeGenerationConfig config
    )
    {
        var rawRecordResult = config.GenerateRawRecordType($"{fileName}Raw", columnMetadata);
        if (rawRecordResult is Result<string, SqlError>.Error<string, SqlError> rawFailure)
            return rawFailure;

        var groupedMethodResult = config.GenerateGroupedQueryMethod(
            $"{fileName}Extensions",
            fileName,
            sql,
            statement.Parameters.ToList().AsReadOnly(),
            columnMetadata,
            groupingConfig,
            config.ConnectionType
        );
        if (
            groupedMethodResult
            is Result<string, SqlError>.Error<string, SqlError> methodFailure
        )
            return methodFailure;

        var groupedModelsResult = config.GenerateGroupedModels(
            groupingConfig.ParentEntity.Name,
            groupingConfig.ChildEntity.Name,
            groupingConfig.ParentEntity.Columns,
            groupingConfig.ChildEntity.Columns,
            columnMetadata
        );
        if (
            groupedModelsResult
            is Result<string, SqlError>.Error<string, SqlError> modelsFailure
        )
            return modelsFailure;

        var rawRecord = ((Result<string, SqlError>.Ok<string, SqlError>)rawRecordResult).Value;
        var method = ((Result<string, SqlError>.Ok<string, SqlError>)groupedMethodResult).Value;
        var models = ((Result<string, SqlError>.Ok<string, SqlError>)groupedModelsResult).Value;

        return config.GenerateSourceFile(
            config.TargetNamespace,
            $"{rawRecord}\n\n{models}",
            method
        );
    }

    private static Result<string, SqlError> Fail(string message) =>
        new Result<string, SqlError>.Error<string, SqlError>(new SqlError(message));
}
