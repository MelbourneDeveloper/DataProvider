using Selecta;

namespace Lql;

/// <summary>
/// Represents a parsed LQL statement that can be converted to various SQL dialects
/// </summary>
public class LqlStatement
{
    /// <summary>
    /// Gets the parsed AST node
    /// </summary>
    public INode? AstNode { get; init; }

    /// <summary>
    /// TODO: remove this! Use the Result type instead
    /// Don't use this property!
    /// </summary>
    public SqlError? ParseError { get; init; }
}
