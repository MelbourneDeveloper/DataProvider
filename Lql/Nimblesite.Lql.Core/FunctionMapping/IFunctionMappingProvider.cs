namespace Nimblesite.Lql.Core.FunctionMapping;

/// <summary>
/// Interface for providing platform-specific function mappings
/// </summary>
public interface IFunctionMappingProvider
{
    /// <summary>
    /// Gets the function mapping for a specific Lql function
    /// </summary>
    /// <param name="lqlFunction">The Lql function name</param>
    /// <returns>The function mapping or null if not found</returns>
    FunctionMap? GetFunctionMapping(string lqlFunction);

    /// <summary>
    /// Gets the syntax mapping for this provider
    /// </summary>
    /// <returns>The syntax mapping</returns>
    SqlSyntaxMapping GetSyntaxMapping();

    /// <summary>
    /// Transpiles a function call from Lql to the target dialect
    /// </summary>
    /// <param name="functionName">The function name</param>
    /// <param name="arguments">The function arguments</param>
    /// <returns>The transpiled function call</returns>
    string TranspileFunction(string functionName, params string[] arguments);

    /// <summary>
    /// Formats a LIMIT clause for the target dialect
    /// </summary>
    /// <param name="count">The limit count</param>
    /// <returns>The formatted LIMIT clause</returns>
    string FormatLimitClause(string count);

    /// <summary>
    /// Formats an OFFSET clause for the target dialect
    /// </summary>
    /// <param name="count">The offset count</param>
    /// <returns>The formatted OFFSET clause</returns>
    string FormatOffsetClause(string count);

    /// <summary>
    /// Formats an identifier for the target dialect
    /// </summary>
    /// <param name="identifier">The identifier name</param>
    /// <returns>The formatted identifier</returns>
    string FormatIdentifier(string identifier);
}
