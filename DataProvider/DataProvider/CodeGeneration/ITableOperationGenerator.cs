using Outcome;
using Selecta;

namespace DataProvider.CodeGeneration;

/// <summary>
/// Interface for generating table operation methods (INSERT, UPDATE)
/// </summary>
public interface ITableOperationGenerator
{
    /// <summary>
    /// Generates INSERT and UPDATE operations for a database table
    /// </summary>
    /// <param name="table">Database table metadata</param>
    /// <param name="config">Table configuration</param>
    /// <returns>Generated C# source code</returns>
    Result<string, SqlError> GenerateTableOperations(DatabaseTable table, TableConfig config);

    /// <summary>
    /// Generates an INSERT method for a database table
    /// </summary>
    /// <param name="table">Database table metadata</param>
    /// <returns>Generated INSERT method code</returns>
    Result<string, SqlError> GenerateInsertMethod(DatabaseTable table);

    /// <summary>
    /// Generates an UPDATE method for a database table
    /// </summary>
    /// <param name="table">Database table metadata</param>
    /// <returns>Generated UPDATE method code</returns>
    Result<string, SqlError> GenerateUpdateMethod(DatabaseTable table);
}
