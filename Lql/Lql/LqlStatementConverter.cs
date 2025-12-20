#pragma warning disable CS8509 // Exhaustive switch - Exhaustion analyzer handles this

using Lql.Parsing;
using Outcome;
using Selecta;

using ParseOk = Outcome.Result<Lql.INode, Selecta.SqlError>.Ok<Lql.INode, Selecta.SqlError>;
using ParseError = Outcome.Result<Lql.INode, Selecta.SqlError>.Error<Lql.INode, Selecta.SqlError>;
using StatementOk = Outcome.Result<Lql.LqlStatement, Selecta.SqlError>.Ok<
    Lql.LqlStatement,
    Selecta.SqlError
>;
using StatementError = Outcome.Result<Lql.LqlStatement, Selecta.SqlError>.Error<
    Lql.LqlStatement,
    Selecta.SqlError
>;

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

        return parseResult switch
        {
            ParseOk success => new StatementOk(new LqlStatement { AstNode = success.Value }),
            ParseError failure => new StatementError(failure.Value),
        };
    }
}
