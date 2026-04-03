using System.Collections.Immutable;

namespace Nimblesite.DataProvider.Core;

/// <summary>
/// Represents table operation generation settings for the source generator.
/// </summary>
public class TableConfigItem
{
    /// <summary>
    /// Gets or sets the database schema name.
    /// </summary>
    public string Schema { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to generate insert operations.
    /// </summary>
    public bool GenerateInsert { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to generate update operations.
    /// </summary>
    public bool GenerateUpdate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to generate delete operations.
    /// </summary>
    public bool GenerateDelete { get; set; }

    /// <summary>
    /// Gets or sets the list of columns to exclude from generated operations.
    /// </summary>
    public ImmutableList<string> ExcludeColumns { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of primary key column names.
    /// </summary>
    public ImmutableList<string> PrimaryKeyColumns { get; set; } = [];
}
