namespace Nimblesite.Lql.Core;

/// <summary>
/// Represents a HAVING operation.
/// </summary>
public sealed class HavingStep : StepBase
{
    /// <summary>
    /// Gets the having condition.
    /// </summary>
    public required string Condition { get; init; }
}
