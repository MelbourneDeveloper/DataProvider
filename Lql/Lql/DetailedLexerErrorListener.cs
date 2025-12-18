using Antlr4.Runtime;
using Selecta;

namespace Lql;

/// <summary>
/// Custom error listener for capturing detailed parse errors with position information for lexer
/// </summary>
internal sealed class DetailedLexerErrorListener : IAntlrErrorListener<int>
{
    private readonly List<SqlError> _errors = [];
    private readonly string _source;

    public DetailedLexerErrorListener(string source)
    {
        _source = source;
    }

    public IReadOnlyList<SqlError> Errors => _errors;

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        int offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e
    )
    {
        var error = SqlError.WithDetailedPosition(
            $"Syntax error: {msg}",
            line,
            charPositionInLine,
            0,
            0,
            _source
        );

        _errors.Add(error);
    }
}
