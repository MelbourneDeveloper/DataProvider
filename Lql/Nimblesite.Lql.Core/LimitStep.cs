namespace Nimblesite.Lql.Core;

/// <summary>
/// Represents a LIMIT operation.
/// </summary>
public sealed class LimitStep : StepBase
{
    /// <summary>
    /// Gets the limit count.
    /// </summary>
    public required string Count { get; init; }
}
