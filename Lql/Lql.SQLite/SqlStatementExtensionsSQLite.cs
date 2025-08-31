using Results;

namespace Lql.SQLite;

/// <summary>
/// SQLite-specific extension methods for SelectStatement
/// </summary>
public static class SqlStatementExtensionsSQLite
{
    /// <summary>
    /// Converts a LqlStatement to SQLite syntax
    /// TODO: this should not return a result because it can't fail
    /// </summary>
    /// <param name="statement">The LqlStatement to convert</param>
    /// <returns>A Result containing either SQLite SQL string or a SqlError</returns>
    public static Result<string, SqlError> ToSQLite(this LqlStatement statement)
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
                var sql = ConvertPipelineToSQLite(pipeline);
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
    /// Converts a Selecta.SelectStatement to SQLite syntax
    /// TODO: this should not return a result because it can't fail
    /// </summary>
    /// <param name="statement">The SelectStatement to convert</param>
    /// <returns>A Result containing either SQLite SQL string or a SqlError</returns>
    public static Result<string, SqlError> ToSQLite(this Selecta.SelectStatement statement)
    {
        try
        {
            var sql = SQLiteContext.ToSQLiteSql(statement);
            return new Result<string, SqlError>.Success(sql);
        }
        catch (Exception ex)
        {
            return new Result<string, SqlError>.Failure(SqlError.FromException(ex));
        }
    }

    private static string ConvertPipelineToSQLite(Pipeline pipeline)
    {
        var context = new SQLiteContext();
        return PipelineProcessor.ConvertPipelineToSql(pipeline, context);
    }
}
