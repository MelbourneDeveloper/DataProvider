using System.Collections.Immutable;

namespace DataProvider;

// Configuration classes for JSON deserialization specific to source generator
// Note: These are separate from the main DataProvider config classes to avoid conflicts
/// <summary>
/// Configuration for the DataProvider source generator when reading DataProvider.json at compile time.
/// </summary>
public class SourceGeneratorDataProviderConfiguration
{
    /// <summary>
    /// Gets or sets the database connection string used at compile time to fetch metadata.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of queries to generate from.
    /// </summary>
    public ImmutableList<QueryConfigItem> Queries { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of tables to generate operations for.
    /// </summary>
    public ImmutableList<TableConfigItem> Tables { get; set; } = [];
}
