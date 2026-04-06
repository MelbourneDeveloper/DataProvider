namespace Nimblesite.Sql.Model;

/// <summary>
/// Represents the join relationships for one-to-many scenarios that need grouping
/// </summary>
public sealed class JoinGraph
{
    private readonly List<JoinRelationship> _relationships = [];

    /// <summary>
    /// Gets the number of join relationships
    /// </summary>
    public int Count => _relationships.Count;

    /// <summary>
    /// Adds a new join relationship to the graph
    /// </summary>
    /// <param name="leftTable">The left table name</param>
    /// <param name="rightTable">The right table name</param>
    /// <param name="condition">The join condition</param>
    /// <param name="joinType">The join type (default: INNER)</param>
    public void Add(
        string leftTable,
        string rightTable,
        string condition,
        string joinType = "INNER"
    ) => _relationships.Add(new JoinRelationship(leftTable, rightTable, condition, joinType));

    /// <summary>
    /// Gets all join relationships as a read-only collection
    /// </summary>
    /// <returns>The join relationships</returns>
    public IReadOnlyList<JoinRelationship> GetRelationships() => _relationships.AsReadOnly();
}
