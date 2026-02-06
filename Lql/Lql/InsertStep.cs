using System.Collections.ObjectModel;

namespace Lql;

/// <summary>
/// Represents an INSERT operation.
/// </summary>
public sealed class InsertStep : StepBase
{
    /// <summary>
    /// Gets the target table name.
    /// </summary>
    public string Table { get; init; }

    /// <summary>
    /// Gets the column names for the insert.
    /// </summary>
    public Collection<string> Columns { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InsertStep"/> class.
    /// </summary>
    /// <param name="table">The target table name.</param>
    /// <param name="columns">The columns to insert into.</param>
    public InsertStep(string table, IEnumerable<string> columns)
    {
        Table = table;
        Columns = new(columns.ToList());
    }
}
