namespace Nimblesite.DataProvider.Core;

/// <summary>
/// Represents a query configuration entry for the source generator.
/// </summary>
public class QueryConfigItem
{
    /// <summary>
    /// Gets or sets the logical name of the query.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the SQL file.
    /// </summary>
    public string SqlFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to an optional grouping configuration file.
    /// </summary>
    public string GroupingFile { get; set; } = string.Empty;
}
