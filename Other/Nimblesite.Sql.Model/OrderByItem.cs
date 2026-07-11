namespace Nimblesite.Sql.Model;

/// <summary>
/// Represents an ORDER BY item with column and direction
/// </summary>
public sealed record OrderByItem(string Column, string Direction);
