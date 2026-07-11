using System.Collections.ObjectModel;

namespace Nimblesite.Lql.Core;

/// <summary>
/// Represents a pipeline of operations.
/// </summary>
public sealed class Pipeline : INode
{
    /// <summary>
    /// Gets the steps in this pipeline.
    /// </summary>
    public Collection<IStep> Steps { get; } = [];
}
