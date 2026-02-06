using System.Collections.Immutable;
using Selecta;

namespace Lql;

/// <summary>
/// Represents a SELECT operation.
/// </summary>
public sealed class SelectStep : StepBase
{
    /// <summary>
    /// Gets the columns to select.
    /// </summary>
    public ImmutableArray<ColumnInfo> Columns { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectStep"/> class.
    /// </summary>
    /// <param name="columns">The columns to select.</param>
    public SelectStep(IEnumerable<ColumnInfo> columns)
    {
        Columns = [.. columns];
    }
}
