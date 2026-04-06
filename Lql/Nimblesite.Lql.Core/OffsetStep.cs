namespace Nimblesite.Lql.Core;

/// <summary>
/// Represents an OFFSET operation.
/// </summary>
public sealed class OffsetStep : StepBase
{
    /// <summary>
    /// Gets the offset count.
    /// </summary>
    public required string Count { get; init; }
}
