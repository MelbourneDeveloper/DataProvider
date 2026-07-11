namespace Nimblesite.Lql.Core;

/// <summary>
/// Represents an identifier (table, column, or variable name).
/// </summary>
/// <param name="Name">The identifier name.</param>
public sealed record Identifier(string Name) : INode;
