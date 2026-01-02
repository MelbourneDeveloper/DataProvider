using Lql.Parsing;
using Outcome;
using Selecta;
using StatementError = Outcome.Result<Lql.LqlStatement, Selecta.SqlError>.Error<
    Lql.LqlStatement,
    Selecta.SqlError
>;
using StatementOk = Outcome.Result<Lql.LqlStatement, Selecta.SqlError>.Ok<
    Lql.LqlStatement,
    Selecta.SqlError
>;

namespace Lql;

/// <summary>
/// Converts LQL code to LqlStatement and provides PostgreSQL generation.
/// </summary>
public static class LqlStatementConverter
{
    /// <summary>
    /// Converts LQL code to a LqlStatement using the Antlr parser.
    /// </summary>
    /// <param name="lqlCode">The LQL code to convert.</param>
    /// <returns>A Result containing either a LqlStatement or a SqlError.</returns>
    public static Result<LqlStatement, SqlError> ToStatement(string lqlCode)
    {
        var parseResult = LqlCodeParser.Parse(lqlCode);

        return parseResult.Match<Result<LqlStatement, SqlError>>(
            success => new StatementOk(new LqlStatement { AstNode = success }),
            failure => new StatementError(failure)
        );
    }
}
