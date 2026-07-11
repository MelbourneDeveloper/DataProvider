namespace Nimblesite.Sql.Model;

/// <summary>
/// Represents a UNION or UNION ALL operation
/// </summary>
public sealed record UnionOperation(string Query, bool IsUnionAll);
