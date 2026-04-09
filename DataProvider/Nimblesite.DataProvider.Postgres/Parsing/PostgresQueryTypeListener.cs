using System.Diagnostics.CodeAnalysis;

namespace Nimblesite.DataProvider.Postgres.Parsing;

// Implements [CON-PARSER-ONLY]. Mirrors SqliteQueryTypeListener but hooks the
// PostgreSQL-rule names: Selectstmt / Insertstmt / Updatestmt / Deletestmt
// (no underscores, per the vendored grammar's casing).
/// <summary>
/// Listener that walks the PostgreSQL parse tree and records the top-level
/// statement kind (SELECT / INSERT / UPDATE / DELETE).
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class PostgresQueryTypeListener : PostgreSQLParserBaseListener
{
    /// <summary>
    /// Gets the detected query type. Defaults to <c>UNKNOWN</c> if no
    /// statement rule is entered before the walk finishes.
    /// </summary>
    public string QueryType { get; private set; } = "UNKNOWN";

    /// <inheritdoc />
    public override void EnterSelectstmt(PostgreSQLParser.SelectstmtContext context) =>
        QueryType = "SELECT";

    /// <inheritdoc />
    public override void EnterInsertstmt(PostgreSQLParser.InsertstmtContext context) =>
        QueryType = "INSERT";

    /// <inheritdoc />
    public override void EnterUpdatestmt(PostgreSQLParser.UpdatestmtContext context) =>
        QueryType = "UPDATE";

    /// <inheritdoc />
    public override void EnterDeletestmt(PostgreSQLParser.DeletestmtContext context) =>
        QueryType = "DELETE";
}
