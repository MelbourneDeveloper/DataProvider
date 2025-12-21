using Outcome;

namespace Selecta;

/// <summary>
/// Abstraction for parsing SQL and extracting comprehensive metadata
/// </summary>
public interface ISqlParser
{
    /// <summary>
    /// Parse SQL text and extract comprehensive metadata including SELECT list, tables, parameters, and joins
    /// </summary>
    /// <param name="sql">The SQL text to parse</param>
    /// <returns>A Result containing either the parsed SQL statement metadata or an error message</returns>
    Result<SelectStatement, string> ParseSql(string sql);
}
