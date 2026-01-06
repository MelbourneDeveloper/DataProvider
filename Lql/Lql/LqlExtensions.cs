namespace Lql;

/// <summary>
/// Extension methods for working with nodes and steps.
/// </summary>
internal static class LqlExtensions
{
    /// <summary>
    /// Wraps a node in an identity step.
    /// </summary>
    /// <param name="node">The node to wrap.</param>
    /// <returns>An identity step containing the node.</returns>
    public static IStep Wrap(this INode node) => new IdentityStep { Base = node };
}
