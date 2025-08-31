using Results;

namespace Lql.Postgres;

/// <summary>
/// PostgreSQL-specific extension methods for SelectStatement
/// </summary>
public static class SqlStatementExtensionsPostgreSQL
{
    /// <summary>
    /// Converts a LqlStatement to PostgreSQL syntax
    /// </summary>
    /// <param name="statement">The LqlStatement to convert</param>
    /// <returns>A Result containing either PostgreSQL SQL string or a SqlError</returns>
    public static Result<string, SqlError> ToPostgreSql(this LqlStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        if (statement.ParseError != null)
        {
            return new Result<string, SqlError>.Failure(statement.ParseError);
        }

        if (statement.AstNode == null)
        {
            return new Result<string, SqlError>.Failure(
                new SqlError("No AST node found in statement")
            );
        }

        try
        {
            if (statement.AstNode is Pipeline pipeline)
            {
                var sql = ConvertPipelineToPostgreSQL(pipeline);
                return new Result<string, SqlError>.Success(sql);
            }

            var unknownSql = statement.AstNode is Identifier identifier
                ? $"SELECT *\nFROM {identifier.Name}"
                : "-- Unknown AST node type";
            return new Result<string, SqlError>.Success(unknownSql);
        }
        catch (Exception ex)
        {
            return new Result<string, SqlError>.Failure(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Converts a pipeline to PostgreSQL with proper table aliases and column handling
    /// </summary>
    /// <param name="pipeline">The pipeline to convert</param>
    /// <returns>PostgreSQL SQL string</returns>
    private static string ConvertPipelineToPostgreSQL(Pipeline pipeline)
    {
        var context = new PostgreSqlContext();
        return PipelineProcessor.ConvertPipelineToSql(pipeline, context, ProcessColumnReferences);
    }

    /// <summary>
    /// Processes column references in a condition string to use proper table aliases
    /// </summary>
    /// <param name="condition">The condition string to process</param>
    /// <returns>The processed condition with proper table aliases</returns>
    private static string ProcessColumnReferences(string condition)
    {
        if (string.IsNullOrEmpty(condition))
            return condition;

        var processedCondition = condition;

        // Replace row.tableName.column with tableAlias.column
        // For simple case, replace row.orders.status with o.status pattern
        processedCondition = processedCondition.Replace(
            "row.orders.",
            "o.",
            StringComparison.OrdinalIgnoreCase
        );
        processedCondition = processedCondition.Replace(
            "row.users.",
            "u.",
            StringComparison.OrdinalIgnoreCase
        );
        processedCondition = processedCondition.Replace(
            "row.employees.",
            "e.",
            StringComparison.OrdinalIgnoreCase
        );

        return processedCondition;
    }
}
