namespace DataProvider;

/// <summary>
/// Configuration for DataProvider code generation
/// </summary>
/// <example>
/// <code>
/// // Configure tables for code generation
/// var config = new DataProviderConfig
/// {
///     ConnectionString = "Data Source=app.db",
///     Tables = new List&lt;TableConfig&gt;
///     {
///         new TableConfig { Schema = "main", Name = "users", GenerateDelete = true },
///         new TableConfig { Schema = "main", Name = "orders" }
///     }.AsReadOnly()
/// };
/// </code>
/// </example>
public sealed record DataProviderConfig
{
    /// <summary>
    /// List of tables to generate code for
    /// </summary>
    public IReadOnlyList<TableConfig> Tables { get; init; } = new List<TableConfig>().AsReadOnly();

    /// <summary>
    /// Connection string for schema inspection (optional, used at build time)
    /// </summary>
    public string? ConnectionString { get; init; }
}

/// <summary>
/// Configuration for a single table
/// </summary>
/// <example>
/// <code>
/// // Configure a table with custom settings
/// var tableConfig = new TableConfig
/// {
///     Schema = "dbo",
///     Name = "Patients",
///     GenerateInsert = true,
///     GenerateUpdate = true,
///     GenerateDelete = false,
///     ExcludeColumns = new List&lt;string&gt; { "computed_column" }.AsReadOnly(),
///     PrimaryKeyColumns = new List&lt;string&gt; { "Id" }.AsReadOnly()
/// };
/// </code>
/// </example>
public sealed record TableConfig
{
    /// <summary>
    /// Table schema (e.g., "dbo" for SQL Server, "main" for SQLite)
    /// </summary>
    public string Schema { get; init; } = string.Empty;

    /// <summary>
    /// Table name
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether to generate Insert extension methods
    /// </summary>
    public bool GenerateInsert { get; init; } = true;

    /// <summary>
    /// Whether to generate Update extension methods
    /// </summary>
    public bool GenerateUpdate { get; init; } = true;

    /// <summary>
    /// Whether to generate Delete extension methods
    /// </summary>
    public bool GenerateDelete { get; init; }

    /// <summary>
    /// Columns to exclude from generation (e.g., computed columns)
    /// </summary>
    public IReadOnlyList<string> ExcludeColumns { get; init; } = new List<string>().AsReadOnly();

    /// <summary>
    /// Primary key columns (auto-detected if not specified)
    /// </summary>
    public IReadOnlyList<string> PrimaryKeyColumns { get; init; } = new List<string>().AsReadOnly();
}
