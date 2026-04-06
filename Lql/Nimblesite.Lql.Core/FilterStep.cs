using Nimblesite.Sql.Model;

namespace Nimblesite.Lql.Core;

/// <summary>
/// Represents a filter (WHERE) operation.
/// </summary>
public sealed class FilterStep : StepBase
{
    /// <summary>
    /// Gets the filter condition.
    /// </summary>
    public required WhereCondition Condition { get; init; }
}
