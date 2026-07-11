namespace Nimblesite.Lql.Core;

/// <summary>
/// Represents a step in a pipeline.
/// </summary>
public interface IStep
{
    /// <summary>
    /// Gets the base node for this step.
    /// </summary>
    INode Base { get; }
}
