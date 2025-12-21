using System.Globalization;
using System.Text;
using Outcome;
using Selecta;

namespace DataProvider.CodeGeneration;

/// <summary>
/// Default implementation of the code generation template
/// </summary>
public class DefaultCodeTemplate : ICodeTemplate
{
    /// <summary>
    /// Generates the model/record type definition
    /// </summary>
    public virtual Result<string, SqlError> GenerateModelType(
        string typeName,
        IReadOnlyList<DatabaseColumn> columns
    ) => ModelGenerator.GenerateRecordType(typeName, columns);

    /// <summary>
    /// Generates the data access extension method
    /// </summary>
    public virtual Result<string, SqlError> GenerateDataAccessMethod(
        string methodName,
        string returnTypeName,
        string sql,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        var className = string.Create(CultureInfo.InvariantCulture, $"{methodName}Extensions");
        return DataAccessGenerator.GenerateQueryMethod(
            className,
            methodName,
            returnTypeName,
            sql,
            parameters,
            columns,
            "SqliteConnection"
        );
    }

    /// <summary>
    /// Generates the complete source file with usings, namespace, and all generated code
    /// </summary>
    public virtual Result<string, SqlError> GenerateSourceFile(
        string namespaceName,
        string modelCode,
        string dataAccessCode
    )
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("namespaceName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(modelCode) && string.IsNullOrWhiteSpace(dataAccessCode))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("At least one of modelCode or dataAccessCode must be provided")
            );

        var sb = new StringBuilder();

        // Generate using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Microsoft.Data.Sqlite;");
        sb.AppendLine("using Outcome;");
        sb.AppendLine("using Selecta;");
        sb.AppendLine();

        // Generate namespace
        sb.AppendLine(CultureInfo.InvariantCulture, $"namespace {namespaceName};");
        sb.AppendLine();

        // Add data access code if provided
        if (!string.IsNullOrWhiteSpace(dataAccessCode))
        {
            sb.AppendLine(dataAccessCode);
            sb.AppendLine();
        }

        // Add model code if provided
        if (!string.IsNullOrWhiteSpace(modelCode))
        {
            sb.Append(modelCode);
        }

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates grouped parent-child model types
    /// </summary>
    public virtual Result<string, SqlError> GenerateGroupedModels(
        GroupingConfig groupingConfig,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        if (groupingConfig == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("groupingConfig cannot be null")
            );

        if (columns == null || columns.Count == 0)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("columns cannot be null or empty")
            );

        return ModelGenerator.GenerateGroupedRecordTypes(
            groupingConfig.ParentEntity.Name,
            groupingConfig.ChildEntity.Name,
            groupingConfig.ParentEntity.Columns,
            groupingConfig.ChildEntity.Columns,
            columns
        );
    }
}
