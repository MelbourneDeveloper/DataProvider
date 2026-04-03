using Nimblesite.Lql.Core.Parsing;
using Outcome;
using Nimblesite.Sql.Model;
using StatementError = Outcome.Result<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>.Error<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>;
using StatementOk = Outcome.Result<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>.Ok<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.Lql.Core;

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
