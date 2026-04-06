namespace Nimblesite.Lql.Core;

/// <summary>
/// Base class for pipeline steps.
/// </summary>
public abstract class StepBase : IStep
{
    /// <summary>
    /// Gets or sets the base node for this step.
    /// </summary>
    public required INode Base { get; init; }
}
