using System.Collections.Immutable;

namespace Nimblesite.Lql.Core;

/// <summary>
/// Represents an ORDER BY operation.
/// </summary>
public sealed class OrderByStep : StepBase
{
    /// <summary>
    /// Gets the order items (column, direction).
    /// </summary>
    public ImmutableArray<(string Column, string Direction)> OrderItems { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderByStep"/> class.
    /// </summary>
    /// <param name="orderItems">The order items.</param>
    public OrderByStep(IEnumerable<(string Column, string Direction)> orderItems)
    {
        OrderItems = [.. orderItems];
    }
}
