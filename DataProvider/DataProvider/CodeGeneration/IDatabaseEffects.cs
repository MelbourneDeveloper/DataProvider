using Results;
using Selecta;

namespace DataProvider.CodeGeneration;

/// <summary>
/// Interface for database effects - operations that interact with the database
/// </summary>
public interface IDatabaseEffects
{
    /// <summary>
    /// Gets column metadata by executing the SQL query against the database.
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="sql">SQL query to execute</param>
    /// <param name="parameters">Query parameters</param>
    /// <returns>List of database columns with their metadata</returns>
    Task<Result<IReadOnlyList<DatabaseColumn>, SqlError>> GetColumnMetadataFromSqlAsync(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    );
}
