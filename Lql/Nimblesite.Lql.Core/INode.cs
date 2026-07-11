namespace Nimblesite.Lql.Core;

/// <summary>
/// Marker interface for AST nodes (Pipeline, Identifier, etc.).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1040:Avoid empty interfaces",
    Justification = "Marker interface for type discrimination in AST nodes"
)]
public interface INode;
