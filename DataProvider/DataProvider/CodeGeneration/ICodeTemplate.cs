using Outcome;
using Selecta;

namespace DataProvider.CodeGeneration;

/// <summary>
/// Interface for code generation templates that can be customized by users
/// </summary>
public interface ICodeTemplate
{
    /// <summary>
    /// Generates the model/record type definition
    /// </summary>
    /// <param name="typeName">Name of the type to generate</param>
    /// <param name="columns">Database columns metadata</param>
    /// <returns>Generated C# code for the model type</returns>
    Result<string, SqlError> GenerateModelType(
        string typeName,
        IReadOnlyList<DatabaseColumn> columns
    );

    /// <summary>
    /// Generates the data access extension method
    /// </summary>
    /// <param name="methodName">Name of the extension method</param>
    /// <param name="returnTypeName">Name of the return type</param>
    /// <param name="sql">SQL query text</param>
    /// <param name="parameters">SQL parameters</param>
    /// <param name="columns">Database columns metadata</param>
    /// <returns>Generated C# code for the extension method</returns>
    Result<string, SqlError> GenerateDataAccessMethod(
        string methodName,
        string returnTypeName,
        string sql,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<DatabaseColumn> columns
    );

    /// <summary>
    /// Generates the complete source file with usings, namespace, and all generated code
    /// </summary>
    /// <param name="namespaceName">Target namespace</param>
    /// <param name="modelCode">Generated model type code</param>
    /// <param name="dataAccessCode">Generated data access code</param>
    /// <returns>Complete C# source file content</returns>
    Result<string, SqlError> GenerateSourceFile(
        string namespaceName,
        string modelCode,
        string dataAccessCode
    );

    /// <summary>
    /// Generates grouped parent-child model types
    /// </summary>
    /// <param name="groupingConfig">Configuration for parent-child grouping</param>
    /// <param name="columns">Database columns metadata</param>
    /// <returns>Generated C# code for grouped models</returns>
    Result<string, SqlError> GenerateGroupedModels(
        GroupingConfig groupingConfig,
        IReadOnlyList<DatabaseColumn> columns
    );
}
