namespace Nimblesite.Lql.Core;

/// <summary>
/// Represents a UNION ALL operation.
/// </summary>
public sealed class UnionAllStep : StepBase
{
    /// <summary>
    /// Gets the other query to union with.
    /// </summary>
    public required string OtherQuery { get; init; }
}
