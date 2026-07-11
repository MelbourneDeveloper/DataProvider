namespace Nimblesite.Lql.Core;

/// <summary>
/// Represents a UNION operation.
/// </summary>
public sealed class UnionStep : StepBase
{
    /// <summary>
    /// Gets the other query to union with.
    /// </summary>
    public required string OtherQuery { get; init; }
}
