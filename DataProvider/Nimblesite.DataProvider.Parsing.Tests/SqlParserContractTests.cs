using Antlr4.Runtime.Tree;
using Nimblesite.Sql.Model;
using Outcome;
using Xunit;

namespace Nimblesite.DataProvider.Parsing.Tests;

// Shared parser contract. Every concrete ISqlParser implementation
// (SQLite + Postgres today, SQL Server next) inherits from this and must
// pass every test. Dialect-specific SQL (ILIKE, ::cast, $N) is exposed via
// abstract hooks so each subclass can describe its own dialect without
// forking the test logic.
//
// Implements [TEST-PARSER] + [CON-PARSER-ONLY] — both parsers get the same
// floor of behavioural coverage.
/// <summary>
/// Abstract contract tests that every dialect's <see cref="ISqlParser"/>
/// implementation must satisfy. Concrete subclasses supply the parser
/// factory and a handful of dialect hooks for the SQL snippets + rule
/// names that differ.
/// </summary>
public abstract class SqlParserContractTests
{
    /// <summary>Builds a fresh parser for a single test.</summary>
    protected abstract ISqlParser CreateParser();

    /// <summary>
    /// Parses <paramref name="sql"/> all the way through the dialect's ANTLR
    /// parser and returns the raw root parse tree. Tests use this to assert
    /// on rule contexts + terminal nodes. Implementations construct their
    /// own lexer / parser / token stream here.
    /// </summary>
    protected abstract IParseTree ParseRawTree(string sql);

    // ──────────────────────────────────────────────────────────────────
    // Rule-name suffix hooks. Each dialect's ANTLR-generated rule contexts
    // have different casing (SQLite uses `Select_stmtContext`, Postgres
    // uses `SelectstmtContext`). Concrete subclasses override these.
    // ──────────────────────────────────────────────────────────────────
    protected abstract string SelectStmtRuleSuffix { get; }

    protected abstract string InsertStmtRuleSuffix { get; }

    protected abstract string UpdateStmtRuleSuffix { get; }

    protected abstract string DeleteStmtRuleSuffix { get; }

    // ──────────────────────────────────────────────────────────────────
    // Dialect-specific SQL snippets. SQLite rewrites ILIKE → LIKE because
    // its grammar has no ILIKE, and uses CAST(x AS text) instead of ::cast.
    // Each subclass supplies its own variant so the shared test text just
    // talks about "case-insensitive LIKE" and "text cast".
    // ──────────────────────────────────────────────────────────────────
    protected abstract string CaseInsensitiveLikeOperator { get; }

    protected abstract string TextCastExpression(string column);

    protected virtual bool SupportsReturningClause => true;

    // ──────────────────────────────────────────────────────────────────
    // Helpers — only depend on the abstract hooks above, so every test
    // body is dialect-neutral.
    // ──────────────────────────────────────────────────────────────────
    protected static SelectStatement AssertOk(Result<SelectStatement, string> result)
    {
        Assert.True(
            result is Result<SelectStatement, string>.Ok<SelectStatement, string>,
            result is Result<SelectStatement, string>.Error<SelectStatement, string> err
                ? $"Parse failed: {err.Value}"
                : "Parse failed"
        );
        return ((Result<SelectStatement, string>.Ok<SelectStatement, string>)result).Value;
    }

    protected static int CountDescendants(IParseTree tree, string ruleSuffix)
    {
        var count = 0;
        CountDescendantsRecursive(tree, ruleSuffix, ref count);
        return count;
    }

    private static void CountDescendantsRecursive(IParseTree node, string ruleSuffix, ref int count)
    {
        if (
            node is Antlr4.Runtime.ParserRuleContext ctx
            && ctx.GetType().Name.EndsWith(ruleSuffix, StringComparison.Ordinal)
        )
        {
            count++;
        }
        for (var i = 0; i < node.ChildCount; i++)
        {
            CountDescendantsRecursive(node.GetChild(i), ruleSuffix, ref count);
        }
    }

    protected static bool ContainsTerminal(IParseTree tree, string text)
    {
        if (
            tree is ITerminalNode terminal
            && string.Equals(terminal.GetText(), text, StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }
        for (var i = 0; i < tree.ChildCount; i++)
        {
            if (ContainsTerminal(tree.GetChild(i), text))
                return true;
        }
        return false;
    }

    // ══════════════════════════════════════════════════════════════════
    // Shared parse-tree shape tests.
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void Parses_simple_select()
    {
        var stmt = AssertOk(CreateParser().ParseSql("SELECT id FROM users"));
        Assert.Empty(stmt.Parameters);

        var tree = ParseRawTree("SELECT id FROM users");
        Assert.True(CountDescendants(tree, SelectStmtRuleSuffix) >= 1);
    }

    [Fact]
    public void Parses_select_with_where_clause()
    {
        var stmt = AssertOk(CreateParser().ParseSql("SELECT id, name FROM users WHERE id = @id"));
        Assert.Single(stmt.Parameters);
        Assert.Contains(stmt.Parameters, p => p.Name == "id");
    }

    [Fact]
    public void Parses_inner_join()
    {
        const string sql = "SELECT a.id, b.name FROM a JOIN b ON a.b_id = b.id";
        var stmt = AssertOk(CreateParser().ParseSql(sql));
        Assert.Empty(stmt.Parameters);

        var tree = ParseRawTree(sql);
        Assert.True(ContainsTerminal(tree, "JOIN"));
    }

    [Fact]
    public void Parses_left_join_with_where()
    {
        const string sql =
            "SELECT a.id, b.title FROM posts a LEFT JOIN comments b ON a.id = b.post_id WHERE a.id = @postId";
        var stmt = AssertOk(CreateParser().ParseSql(sql));
        Assert.Single(stmt.Parameters);
        Assert.Contains(stmt.Parameters, p => p.Name == "postId");

        var tree = ParseRawTree(sql);
        Assert.True(ContainsTerminal(tree, "LEFT"));
        Assert.True(ContainsTerminal(tree, "JOIN"));
    }

    [Fact]
    public void Parses_cte()
    {
        const string sql =
            "WITH recent AS (SELECT id FROM orders WHERE created_at > @since) SELECT id FROM recent";
        var stmt = AssertOk(CreateParser().ParseSql(sql));
        Assert.Single(stmt.Parameters);
        Assert.Contains(stmt.Parameters, p => p.Name == "since");

        var tree = ParseRawTree(sql);
        Assert.True(ContainsTerminal(tree, "WITH"));
    }

    [Fact]
    public void Parses_insert_with_parameters()
    {
        var returning = SupportsReturningClause ? " RETURNING id" : "";
        var sql = $"INSERT INTO users (name, email) VALUES (@name, @email){returning}";
        var stmt = AssertOk(CreateParser().ParseSql(sql));
        Assert.Equal(2, stmt.Parameters.Count);
        Assert.Contains(stmt.Parameters, p => p.Name == "name");
        Assert.Contains(stmt.Parameters, p => p.Name == "email");

        var tree = ParseRawTree(sql);
        Assert.True(CountDescendants(tree, InsertStmtRuleSuffix) >= 1);
    }

    [Fact]
    public void Parses_update_with_parameters()
    {
        var returning = SupportsReturningClause ? " RETURNING id" : "";
        var sql = $"UPDATE users SET name = @name WHERE id = @id{returning}";
        var stmt = AssertOk(CreateParser().ParseSql(sql));
        Assert.Equal(2, stmt.Parameters.Count);

        var tree = ParseRawTree(sql);
        Assert.True(CountDescendants(tree, UpdateStmtRuleSuffix) >= 1);
    }

    [Fact]
    public void Parses_delete_with_parameters()
    {
        var returning = SupportsReturningClause ? " RETURNING id" : "";
        var sql = $"DELETE FROM users WHERE id = @id{returning}";
        var stmt = AssertOk(CreateParser().ParseSql(sql));
        Assert.Single(stmt.Parameters);

        var tree = ParseRawTree(sql);
        Assert.True(CountDescendants(tree, DeleteStmtRuleSuffix) >= 1);
    }

    [Fact]
    public void Parses_case_insensitive_like()
    {
        var op = CaseInsensitiveLikeOperator;
        var sql = $"SELECT id FROM users WHERE email {op} @pattern";
        var stmt = AssertOk(CreateParser().ParseSql(sql));
        Assert.Single(stmt.Parameters);

        var tree = ParseRawTree(sql);
        Assert.True(ContainsTerminal(tree, op));
    }

    [Fact]
    public void Parses_text_cast()
    {
        var sql = $"SELECT {TextCastExpression("id")} FROM users";
        var stmt = AssertOk(CreateParser().ParseSql(sql));
        Assert.Empty(stmt.Parameters);
    }

    [Fact]
    public void Parses_function_call_with_wildcard()
    {
        const string sql = "SELECT count(*) FROM users";
        var stmt = AssertOk(CreateParser().ParseSql(sql));
        Assert.Empty(stmt.Parameters);

        var tree = ParseRawTree(sql);
        Assert.True(ContainsTerminal(tree, "*"));
    }

    [Fact]
    public void Parses_group_by_having()
    {
        const string sql =
            "SELECT user_id, count(*) FROM orders GROUP BY user_id HAVING count(*) > @min";
        var stmt = AssertOk(CreateParser().ParseSql(sql));
        Assert.Single(stmt.Parameters);

        var tree = ParseRawTree(sql);
        Assert.True(ContainsTerminal(tree, "GROUP"));
        Assert.True(ContainsTerminal(tree, "HAVING"));
    }

    [Fact]
    public void Parses_order_by_limit_offset()
    {
        const string sql =
            "SELECT id FROM users ORDER BY created_at DESC LIMIT @limit OFFSET @offset";
        var stmt = AssertOk(CreateParser().ParseSql(sql));
        Assert.Equal(2, stmt.Parameters.Count);
        Assert.Contains(stmt.Parameters, p => p.Name == "limit");
        Assert.Contains(stmt.Parameters, p => p.Name == "offset");

        var tree = ParseRawTree(sql);
        Assert.True(ContainsTerminal(tree, "ORDER"));
        Assert.True(ContainsTerminal(tree, "LIMIT"));
        Assert.True(ContainsTerminal(tree, "OFFSET"));
    }

    [Fact]
    public void Parses_qualified_column_references()
    {
        const string sql = "SELECT u.id, u.name FROM users u";
        var stmt = AssertOk(CreateParser().ParseSql(sql));
        Assert.Empty(stmt.Parameters);

        var tree = ParseRawTree(sql);
        Assert.True(ContainsTerminal(tree, "u"));
        Assert.True(ContainsTerminal(tree, "id"));
    }

    [Fact]
    public void Parses_mixed_case_alias()
    {
        const string sql = "SELECT name AS UserName FROM users";
        AssertOk(CreateParser().ParseSql(sql));

        var tree = ParseRawTree(sql);
        Assert.True(ContainsTerminal(tree, "AS"));
        Assert.True(ContainsTerminal(tree, "UserName"));
    }

    [Fact]
    public void Parses_subquery_in_from()
    {
        const string sql = "SELECT t.id FROM (SELECT id FROM users) AS t";
        AssertOk(CreateParser().ParseSql(sql));

        var tree = ParseRawTree(sql);
        Assert.True(CountDescendants(tree, SelectStmtRuleSuffix) >= 1);
    }

    [Fact]
    public void Parses_exists_subquery()
    {
        const string sql =
            "SELECT id FROM users u WHERE EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id)";
        AssertOk(CreateParser().ParseSql(sql));

        var tree = ParseRawTree(sql);
        Assert.True(ContainsTerminal(tree, "EXISTS"));
    }

    [Fact]
    public void Parses_not_exists_subquery()
    {
        const string sql =
            "SELECT id FROM users u WHERE NOT EXISTS (SELECT 1 FROM bans b WHERE b.user_id = u.id)";
        AssertOk(CreateParser().ParseSql(sql));

        var tree = ParseRawTree(sql);
        Assert.True(ContainsTerminal(tree, "NOT"));
        Assert.True(ContainsTerminal(tree, "EXISTS"));
    }

    [Fact]
    public void Malformed_sql_does_not_throw()
    {
        // The contract is that ParseSql catches thrown exceptions and
        // returns them as Result.Error. A parser that throws is buggy.
        var result = CreateParser().ParseSql("NOT A VALID SQL @#$");
        Assert.True(
            result
                is Result<SelectStatement, string>.Error<SelectStatement, string>
                    or Result<SelectStatement, string>.Ok<SelectStatement, string>
        );
    }

    // ══════════════════════════════════════════════════════════════════
    // Shared parameter-extraction tests.
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void At_id_is_extracted()
    {
        var stmt = AssertOk(CreateParser().ParseSql("SELECT * FROM users WHERE id = @id"));
        Assert.Single(stmt.Parameters);
        Assert.Equal("id", stmt.Parameters.Single().Name);
    }

    [Fact]
    public void At_user_id_with_underscore()
    {
        var stmt = AssertOk(CreateParser().ParseSql("SELECT * FROM users WHERE id = @user_id"));
        Assert.Contains(stmt.Parameters, p => p.Name == "user_id");
    }

    [Fact]
    public void At_first_name_with_underscore()
    {
        var stmt = AssertOk(
            CreateParser().ParseSql("SELECT * FROM users WHERE first_name = @first_name")
        );
        Assert.Contains(stmt.Parameters, p => p.Name == "first_name");
    }

    [Fact]
    public void Mixed_at_params()
    {
        var stmt = AssertOk(
            CreateParser().ParseSql("SELECT * FROM users WHERE a = @alpha AND b = @beta")
        );
        Assert.Contains(stmt.Parameters, p => p.Name == "alpha");
        Assert.Contains(stmt.Parameters, p => p.Name == "beta");
        Assert.Equal(2, stmt.Parameters.Count);
    }

    [Fact]
    public void Repeated_params_are_deduped()
    {
        var stmt = AssertOk(
            CreateParser().ParseSql("SELECT * FROM users WHERE id = @id OR parent_id = @id")
        );
        Assert.Single(stmt.Parameters);
        Assert.Equal("id", stmt.Parameters.Single().Name);
    }

    [Fact]
    public void Case_sensitive_params_are_distinct()
    {
        var stmt = AssertOk(
            CreateParser().ParseSql("SELECT * FROM users WHERE id = @Id OR other = @id")
        );
        Assert.Contains(stmt.Parameters, p => p.Name == "Id");
        Assert.Contains(stmt.Parameters, p => p.Name == "id");
        Assert.Equal(2, stmt.Parameters.Count);
    }
}
