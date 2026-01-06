using System.Diagnostics.CodeAnalysis;
using Antlr4.Runtime.Tree;

namespace DataProvider.SQLite.Parsing;

/// <summary>
/// Parameter extractor for SQLite using ANTLR parse tree
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class SqliteParameterExtractor
{
    /// <summary>
    /// Extracts parameter names from the provided ANTLR <see cref="IParseTree"/>.
    /// Supports positional (?), named (:name), @name, and $name styles.
    /// </summary>
    /// <param name="parseTree">The ANTLR parse tree.</param>
    /// <returns>A list of discovered parameter names.</returns>
    public static List<string> ExtractParameters(IParseTree parseTree)
    {
        var parameters = new HashSet<string>();
        ExtractParametersRecursive(parseTree, parameters);
        return [.. parameters];
    }

    /// <summary>
    /// Recursively traverses the parse tree, collecting parameter names.
    /// </summary>
    /// <param name="node">The current parse node.</param>
    /// <param name="parameters">The set to add parameters to.</param>
    private static void ExtractParametersRecursive(IParseTree node, HashSet<string> parameters)
    {
        // Check if this node represents a parameter
        if (node is ITerminalNode terminal)
        {
            var text = terminal.GetText();
            if (text.StartsWith('?'))
            {
                // SQLite positional parameter
                parameters.Add($"param{parameters.Count + 1}");
            }
            else if (text.StartsWith(':') && text.Length > 1)
            {
                // SQLite named parameter
                var paramName = text[1..];
                if (IsValidParameterName(paramName))
                    parameters.Add(paramName);
            }
            else if (text.StartsWith('@') && text.Length > 1)
            {
                // SQL Server style parameter (also supported by SQLite)
                var paramName = text[1..];
                if (IsValidParameterName(paramName))
                    parameters.Add(paramName);
            }
            else if (text.StartsWith('$') && text.Length > 1)
            {
                // PostgreSQL style parameter (also supported by SQLite)
                var paramName = text[1..];
                if (IsValidParameterName(paramName))
                    parameters.Add(paramName);
            }
        }

        // Recursively check child nodes
        for (int i = 0; i < node.ChildCount; i++)
        {
            ExtractParametersRecursive(node.GetChild(i), parameters);
        }
    }

    /// <summary>
    /// Validates a candidate parameter name.
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    private static bool IsValidParameterName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // Parameter name should start with letter or underscore
        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        // Rest should be letters, digits, or underscores
        return name.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}
