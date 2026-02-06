using System.Collections.ObjectModel;
using Selecta;

namespace Lql;

/// <summary>
/// Represents a SELECT DISTINCT operation.
/// </summary>
public sealed class SelectDistinctStep : StepBase
{
    /// <summary>
    /// Gets the columns to select.
    /// </summary>
    public Collection<ColumnInfo> Columns { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectDistinctStep"/> class.
    /// </summary>
    /// <param name="columns">The columns to select.</param>
    public SelectDistinctStep(IEnumerable<ColumnInfo> columns)
    {
        Columns = new(columns.ToList());
    }
}
