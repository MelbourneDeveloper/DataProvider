namespace Selecta;

/// <summary>
/// Represents a table in the FROM clause
/// </summary>
public sealed record TableInfo(string Name, string? Alias);
