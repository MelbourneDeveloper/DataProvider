using Lql.Parsing;
using Outcome;
using Selecta;

namespace Lql;

/// <summary>
/// Converts LQL code to LqlStatement and provides PostgreSQL generation
/// </summary>
public static class LqlStatementConverter
{
    /// <summary>
    /// Converts LQL code to a LqlStatement using the Antlr parser
    /// </summary>
    /// <param name="lqlCode">The LQL code to convert</param>
    /// <returns>A Result containing either a LqlStatement or a SqlError</returns>
    public static Result<LqlStatement, SqlError> ToStatement(string lqlCode)
    {
        var parseResult = LqlCodeParser.Parse(lqlCode);

#pragma warning disable EXHAUSTION001 // Result type is exhaustive
        return parseResult switch
        {
            Result<INode, SqlError>.Ok<INode, SqlError> success => new Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError>(
                new LqlStatement { AstNode = success.Value }
            ),
            Result<INode, SqlError>.Error<INode, SqlError> failure => new Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError>(
                failure.Value
            ),
            _ => throw new InvalidOperationException("Unexpected Result type")
        };
#pragma warning restore EXHAUSTION001
    }
}
