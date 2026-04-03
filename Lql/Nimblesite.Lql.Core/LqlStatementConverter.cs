using Nimblesite.Lql.Core.Parsing;
using Outcome;
using Selecta;
using StatementError = Outcome.Result<Nimblesite.Lql.Core.Nimblesite.Lql.CoreStatement, Selecta.SqlError>.Error<
    Nimblesite.Lql.Core.Nimblesite.Lql.CoreStatement,
    Selecta.SqlError
>;
using StatementOk = Outcome.Result<Nimblesite.Lql.Core.Nimblesite.Lql.CoreStatement, Selecta.SqlError>.Ok<
    Nimblesite.Lql.Core.Nimblesite.Lql.CoreStatement,
    Selecta.SqlError
>;

namespace Nimblesite.Lql.Core;

/// <summary>
/// Converts LQL code to Nimblesite.Lql.CoreStatement and provides PostgreSQL generation.
/// </summary>
public static class Nimblesite.Lql.CoreStatementConverter
{
    /// <summary>
    /// Converts LQL code to a Nimblesite.Lql.CoreStatement using the Antlr parser.
    /// </summary>
    /// <param name="lqlCode">The LQL code to convert.</param>
    /// <returns>A Result containing either a Nimblesite.Lql.CoreStatement or a SqlError.</returns>
    public static Result<Nimblesite.Lql.CoreStatement, SqlError> ToStatement(string lqlCode)
    {
        var parseResult = Nimblesite.Lql.CoreCodeParser.Parse(lqlCode);

        return parseResult.Match<Result<Nimblesite.Lql.CoreStatement, SqlError>>(
            success => new StatementOk(new Nimblesite.Lql.CoreStatement { AstNode = success }),
            failure => new StatementError(failure)
        );
    }
}
