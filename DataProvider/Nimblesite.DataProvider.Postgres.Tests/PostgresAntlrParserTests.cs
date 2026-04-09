using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Nimblesite.DataProvider.Parsing.Tests;
using Nimblesite.DataProvider.Postgres.Parsing;
using Nimblesite.Sql.Model;

namespace Nimblesite.DataProvider.Postgres.Tests;

// Concrete Postgres binding for the shared SqlParserContractTests. Every
// test in the base class runs against PostgresAntlrParser. No Postgres-
// specific test logic lives here — just the hooks the base needs.
public sealed class PostgresAntlrParserTests : SqlParserContractTests
{
    protected override ISqlParser CreateParser() => new PostgresAntlrParser();

    protected override IParseTree ParseRawTree(string sql)
    {
        var input = new AntlrInputStream(sql);
        var lexer = new PostgreSQLLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new PostgreSQLParser(tokens);
        return parser.root();
    }

    // The vendored antlr/grammars-v4 PostgreSQL grammar generates these
    // rule contexts without underscores.
    protected override string SelectStmtRuleSuffix => "SelectstmtContext";

    protected override string InsertStmtRuleSuffix => "InsertstmtContext";

    protected override string UpdateStmtRuleSuffix => "UpdatestmtContext";

    protected override string DeleteStmtRuleSuffix => "DeletestmtContext";

    protected override string CaseInsensitiveLikeOperator => "ILIKE";

    protected override string TextCastExpression(string column) => $"{column}::text";
}
