using Results;
using Selecta;

namespace DataProvider;

/// <summary>
/// Type aliases for code generation functions
/// </summary>
public static class CodeGenerators
{
    /// <summary>
    /// Function type for generating C# source code from SQL metadata
    /// </summary>
    /// <param name="fileName">The name of the SQL file</param>
    /// <param name="sql">The SQL content</param>
    /// <param name="statement">The parsed SQL statement metadata</param>
    /// <param name="hasCustomImplementation">Whether a custom implementation exists</param>
    /// <param name="groupingConfig">Optional grouping configuration for parent-child relationships</param>
    /// <returns>Result with generated C# source code or error</returns>
    public delegate Result<string, SqlError> GenerateCodeFunc(
        string fileName,
        string sql,
        SelectStatement statement,
        bool hasCustomImplementation,
        GroupingConfig? groupingConfig = null
    );

    /// <summary>
    /// Function type for generating C# source code with database metadata
    /// </summary>
    /// <param name="fileName">The name of the SQL file</param>
    /// <param name="sql">The SQL content</param>
    /// <param name="statement">The parsed SQL statement metadata</param>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="columnMetadata">Real column metadata from database</param>
    /// <param name="hasCustomImplementation">Whether a custom implementation exists</param>
    /// <param name="groupingConfig">Optional grouping configuration for parent-child relationships</param>
    /// <returns>Result with generated C# source code or error</returns>
    public delegate Result<string, SqlError> GenerateCodeWithMetadataFunc(
        string fileName,
        string sql,
        SelectStatement statement,
        string connectionString,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        bool hasCustomImplementation,
        GroupingConfig? groupingConfig = null
    );

    /// <summary>
    /// Function type for generating table operations code
    /// </summary>
    /// <param name="table">The database table metadata</param>
    /// <param name="config">The table configuration</param>
    /// <returns>Result with generated C# source code or error</returns>
    public delegate Result<string, SqlError> GenerateTableOperationsFunc(
        DatabaseTable table,
        TableConfig config
    );
}
