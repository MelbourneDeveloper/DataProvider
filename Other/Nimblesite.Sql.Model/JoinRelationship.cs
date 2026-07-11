namespace Nimblesite.Sql.Model;

/// <summary>
/// Represents a single join relationship between two tables (for one-to-many relationships)
/// </summary>
public sealed record JoinRelationship(
    string LeftTable,
    string RightTable,
    string Condition,
    string JoinType = "INNER"
);
