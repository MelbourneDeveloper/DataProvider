using System.Collections.Frozen;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Outcome;
using Selecta;
using System.Diagnostics.CodeAnalysis;

namespace DataProvider.SQLite.Parsing;

/// <summary>
/// SQLite parser implementation using ANTLR grammar
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SqliteAntlrParser : ISqlParser
{
    /// <summary>
    /// Parses the specified SQL text and returns a Result containing either a SelectStatement or an error.
    /// </summary>
    /// <param name="sql">The SQL text to parse.</param>
    /// <returns>A Result containing either a parsed SelectStatement or an error message.</returns>
    public Result<SelectStatement, string> ParseSql(string sql)
    {
        try
        {
            var inputStream = new AntlrInputStream(sql);
            var lexer = new SQLiteLexer(inputStream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new SQLiteParser(tokens);

            // Parse the SQL
            var parseTree = parser.parse();

            // Extract parameters using visitor
            var parameterExtractor = new SqliteParameterExtractor();
            var parameters = SqliteParameterExtractor.ExtractParameters(parseTree);
            var parameterInfos = parameters.Select(p => new ParameterInfo(p)).ToList();

            // Determine query type
            var queryType = DetermineQueryType(parseTree);

            var statement = new SelectStatement { Parameters = parameterInfos.ToFrozenSet() };
            return new Result<SelectStatement, string>.Ok<SelectStatement, string>(statement);
        }
        catch (Exception ex)
        {
            return new Result<SelectStatement, string>.Error<SelectStatement, string>($"Failed to parse SQL: {ex}");
        }
    }

    /// <summary>
    /// Determines the SQL operation type by walking the parse tree.
    /// </summary>
    /// <param name="parseTree">The ANTLR parse tree.</param>
    /// <returns>The inferred query type such as SELECT, INSERT, UPDATE, or DELETE.</returns>
    private static string DetermineQueryType(IParseTree parseTree)
    {
        // Walk the parse tree to determine the query type
        var walker = new ParseTreeWalker();
        var queryTypeListener = new SqliteQueryTypeListener();
        walker.Walk(queryTypeListener, parseTree);
        return queryTypeListener.QueryType;
    }
}
