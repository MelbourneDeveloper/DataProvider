namespace Sync.Postgres;

/// <summary>
/// Information about a column for trigger generation.
/// </summary>
/// <param name="Name">Column name.</param>
/// <param name="DataType">PostgreSQL data type.</param>
/// <param name="IsPrimaryKey">Whether this column is part of the primary key.</param>
public sealed record TriggerColumnInfo(string Name, string DataType, bool IsPrimaryKey);
