namespace Nimblesite.Lql.Core.FunctionMapping;

/// <summary>
/// Represents a mapping of SQL operators and syntax differences
/// </summary>
public record SqlSyntaxMapping(
    string LimitClause,
    string OffsetClause,
    string DateCurrentFunction,
    string DateAddFunction,
    string StringLengthFunction,
    string StringConcatOperator,
    string IdentifierQuoteChar,
    bool SupportsBoolean
);
