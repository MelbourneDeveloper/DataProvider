namespace Nimblesite.DataProvider.Core;

/// <summary>
/// Configuration for grouping query results into parent-child relationships
/// </summary>
public record GroupingConfig(
    string QueryName,
    string GroupingStrategy,
    EntityConfig ParentEntity,
    EntityConfig ChildEntity
);

/// <summary>
/// Configuration for an entity in a grouping operation
/// </summary>
public record EntityConfig(
    string Name,
    IReadOnlyList<string> KeyColumns,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string>? ParentKeyColumns = null
);
