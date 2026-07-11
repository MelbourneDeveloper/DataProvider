using System.Collections.Immutable;

namespace Nimblesite.Lql.Core;

/// <summary>
/// Represents a GROUP BY operation.
/// </summary>
public sealed class GroupByStep : StepBase
{
    /// <summary>
    /// Gets the columns to group by.
    /// </summary>
    public ImmutableArray<string> Columns { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupByStep"/> class.
    /// </summary>
    /// <param name="columns">The columns to group by.</param>
    public GroupByStep(IEnumerable<string> columns)
    {
        Columns = [.. columns];
    }
}
