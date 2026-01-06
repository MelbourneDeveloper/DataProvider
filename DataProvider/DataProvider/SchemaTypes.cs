using Outcome;
using Selecta;

namespace DataProvider;

/// <summary>
/// Represents a database column with its metadata
/// </summary>
/// <example>
/// <code>
/// // Create a column definition
/// var column = new DatabaseColumn
/// {
///     Name = "Id",
///     SqlType = "uniqueidentifier",
///     CSharpType = "Guid",
///     IsNullable = false,
///     IsPrimaryKey = true,
///     IsIdentity = false,
///     IsComputed = false
/// };
///
/// // Create a string column with max length
/// var nameColumn = new DatabaseColumn
/// {
///     Name = "Name",
///     SqlType = "nvarchar(100)",
///     CSharpType = "string",
///     IsNullable = false,
///     MaxLength = 100
/// };
/// </code>
/// </example>
public sealed record DatabaseColumn
{
    /// <summary>
    /// Column name
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// SQL data type (e.g., "int", "varchar(50)", "decimal(10,2)")
    /// </summary>
    public string SqlType { get; init; } = string.Empty;

    /// <summary>
    /// Corresponding C# type (e.g., "int", "string", "decimal")
    /// </summary>
    public string CSharpType { get; init; } = string.Empty;

    /// <summary>
    /// Whether the column allows null values
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Whether the column is part of the primary key
    /// </summary>
    public bool IsPrimaryKey { get; init; }

    /// <summary>
    /// Whether the column is an identity/auto-increment column
    /// </summary>
    public bool IsIdentity { get; init; }

    /// <summary>
    /// Whether the column is computed
    /// </summary>
    public bool IsComputed { get; init; }

    /// <summary>
    /// Maximum length for string columns
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// Precision for decimal columns
    /// </summary>
    public int? Precision { get; init; }

    /// <summary>
    /// Scale for decimal columns
    /// </summary>
    public int? Scale { get; init; }
}

/// <summary>
/// Represents a database table with its columns
/// </summary>
/// <example>
/// <code>
/// // Create a table definition with columns
/// var table = new DatabaseTable
/// {
///     Schema = "dbo",
///     Name = "Patients",
///     Columns = new List&lt;DatabaseColumn&gt;
///     {
///         new DatabaseColumn { Name = "Id", SqlType = "uniqueidentifier", IsPrimaryKey = true },
///         new DatabaseColumn { Name = "Name", SqlType = "nvarchar(100)" },
///         new DatabaseColumn { Name = "DateOfBirth", SqlType = "date", IsNullable = true }
///     }.AsReadOnly()
/// };
///
/// // Access computed column lists
/// var pkColumns = table.PrimaryKeyColumns;
/// var insertColumns = table.InsertableColumns;
/// var updateColumns = table.UpdateableColumns;
/// </code>
/// </example>
public sealed record DatabaseTable
{
    /// <summary>
    /// Table schema
    /// </summary>
    public string Schema { get; init; } = string.Empty;

    /// <summary>
    /// Table name
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// List of columns in the table
    /// </summary>
    public IReadOnlyList<DatabaseColumn> Columns { get; init; } =
        new List<DatabaseColumn>().AsReadOnly();

    /// <summary>
    /// Primary key columns
    /// </summary>
    public IReadOnlyList<DatabaseColumn> PrimaryKeyColumns =>
        Columns.Where(c => c.IsPrimaryKey).ToList().AsReadOnly();

    /// <summary>
    /// Non-identity columns suitable for INSERT operations
    /// </summary>
    public IReadOnlyList<DatabaseColumn> InsertableColumns =>
        Columns.Where(c => !c.IsIdentity && !c.IsComputed).ToList().AsReadOnly();

    /// <summary>
    /// Non-primary key columns suitable for UPDATE operations
    /// </summary>
    public IReadOnlyList<DatabaseColumn> UpdateableColumns =>
        Columns.Where(c => !c.IsPrimaryKey && !c.IsIdentity && !c.IsComputed).ToList().AsReadOnly();
}

/// <summary>
/// Represents metadata about a SQL query result
/// </summary>
public sealed record SqlQueryMetadata
{
    /// <summary>
    /// List of columns returned by the query
    /// </summary>
    public IReadOnlyList<DatabaseColumn> Columns { get; init; } =
        new List<DatabaseColumn>().AsReadOnly();

    /// <summary>
    /// The SQL query text
    /// </summary>
    public string SqlText { get; init; } = string.Empty;
}

/// <summary>
/// Abstraction for inspecting database schema
/// </summary>
internal interface ISchemaInspector
{
    /// <summary>
    /// Gets table information including columns and their metadata
    /// </summary>
    /// <param name="schema">Table schema</param>
    /// <param name="tableName">Table name</param>
    /// <returns>Table information or null if not found</returns>
    Task<DatabaseTable?> GetTableAsync(string schema, string tableName);

    /// <summary>
    /// Gets all tables in the database
    /// </summary>
    /// <returns>List of all tables</returns>
    Task<IReadOnlyList<DatabaseTable>> GetAllTablesAsync();

    /// <summary>
    /// Gets metadata about the columns returned by a SQL query
    /// </summary>
    /// <param name="sqlQuery">The SQL query to analyze</param>
    /// <returns>Result containing metadata about the query result columns</returns>
    Task<Result<SqlQueryMetadata, SqlError>> GetSqlQueryMetadataAsync(string sqlQuery);
}
