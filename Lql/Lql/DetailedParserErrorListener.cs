using Antlr4.Runtime;
using Selecta;

namespace Lql;

/// <summary>
/// Custom error listener for capturing detailed parse errors with position information for parser
/// </summary>
internal sealed class DetailedParserErrorListener : IAntlrErrorListener<IToken>
{
    private readonly List<SqlError> _errors = [];
    private readonly string _source;

    public DetailedParserErrorListener(string source)
    {
        _source = source;
    }

    public IReadOnlyList<SqlError> Errors => _errors;

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e
    )
    {
        var startIndex = offendingSymbol?.StartIndex ?? 0;
        var stopIndex = offendingSymbol?.StopIndex ?? 0;

        var error = SqlError.WithDetailedPosition(
            $"Syntax error: {msg}",
            line,
            charPositionInLine,
            startIndex,
            stopIndex,
            _source
        );

        _errors.Add(error);
    }
}
