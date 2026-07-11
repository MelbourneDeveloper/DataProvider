using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Nimblesite.DataProvider.Core.Parsing;

namespace Nimblesite.DataProvider.Tests;

// Implements [CON-SHARED-CORE]. Targets edge cases in the dialect-agnostic
// AntlrSqlParameterExtractor that real ANTLR parsers rarely emit, by
// driving the walker directly with hand-built TerminalNodeImpls.
public sealed class AntlrSqlParameterExtractorTests
{
    private static TerminalNodeImpl Terminal(string text)
    {
        var token = new CommonToken(0, text);
        return new TerminalNodeImpl(token);
    }

    private sealed class FakeRoot : IParseTree
    {
        private readonly IParseTree[] _children;

        public FakeRoot(params IParseTree[] children) => _children = children;

        public IParseTree Parent
        {
            get => null!;
            set { }
        }

        public Interval SourceInterval => Interval.Invalid;
        public int ChildCount => _children.Length;

        public IParseTree GetChild(int i) => _children[i];

        ITree ITree.GetChild(int i) => _children[i];

        ITree ITree.Parent => null!;

        public object Payload => this;

        public string GetText() => string.Concat(_children.Select(c => c.GetText()));

        public T Accept<T>(IParseTreeVisitor<T> visitor) => default!;

        public string ToStringTree(Antlr4.Runtime.Parser parser) => GetText();

        public string ToStringTree() => GetText();
    }

    [Fact]
    public void Positional_question_becomes_param1_param2()
    {
        var tree = new FakeRoot(Terminal("?"), Terminal("?"));
        var result = AntlrSqlParameterExtractor.ExtractParameters(tree);
        Assert.Equal(2, result.Count);
        Assert.Equal("param1", result[0]);
        Assert.Equal("param2", result[1]);
    }

    [Fact]
    public void Skips_single_char_non_positional_tokens()
    {
        // ":" alone is shorter than 2 characters after the prefix check, so
        // the extractor must skip it without crashing.
        var tree = new FakeRoot(Terminal(":"), Terminal("@"), Terminal("$"));
        var result = AntlrSqlParameterExtractor.ExtractParameters(tree);
        Assert.Empty(result);
    }

    [Fact]
    public void Rejects_numeric_first_char()
    {
        // "$1" is Postgres positional; Core rejects it because "1" is not a
        // valid C# identifier start.
        var tree = new FakeRoot(Terminal("$1"), Terminal(":9name"));
        var result = AntlrSqlParameterExtractor.ExtractParameters(tree);
        Assert.Empty(result);
    }

    [Fact]
    public void Rejects_invalid_interior_character()
    {
        // "@foo-bar" — dash is not a valid identifier character.
        var tree = new FakeRoot(Terminal("@foo-bar"));
        var result = AntlrSqlParameterExtractor.ExtractParameters(tree);
        Assert.Empty(result);
    }

    [Fact]
    public void Skips_empty_terminal_text()
    {
        var tree = new FakeRoot(Terminal(string.Empty));
        var result = AntlrSqlParameterExtractor.ExtractParameters(tree);
        Assert.Empty(result);
    }

    [Fact]
    public void Accepts_underscore_leading_name()
    {
        var tree = new FakeRoot(Terminal("@_private"), Terminal(":name_with_underscore"));
        var result = AntlrSqlParameterExtractor.ExtractParameters(tree);
        Assert.Equal(2, result.Count);
        Assert.Contains("_private", result);
        Assert.Contains("name_with_underscore", result);
    }

    [Fact]
    public void Mixed_positional_and_named()
    {
        var tree = new FakeRoot(Terminal("?"), Terminal("@id"), Terminal("?"));
        var result = AntlrSqlParameterExtractor.ExtractParameters(tree);
        Assert.Equal(3, result.Count);
        Assert.Contains("param1", result);
        Assert.Contains("id", result);
        Assert.Contains("param3", result);
    }
}
