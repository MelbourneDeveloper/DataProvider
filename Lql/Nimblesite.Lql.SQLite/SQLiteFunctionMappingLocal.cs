using System.Collections.Immutable;
using Nimblesite.Lql.Core.FunctionMapping;

namespace Nimblesite.Lql.SQLite;

/// <summary>
/// SQLite-specific function mapping implementation
/// </summary>
public sealed class SQLiteFunctionMappingLocal : FunctionMappingProviderBase
{
    /// <summary>
    /// Singleton instance of the SQLite function mapping
    /// </summary>
    public static readonly SQLiteFunctionMappingLocal Instance = new();

    /// <summary>
    /// Private constructor to enforce singleton pattern
    /// </summary>
    private SQLiteFunctionMappingLocal()
        : base(CreateFunctionMappings, CreateSyntaxMapping) { }

    /// <summary>
    /// Creates the SQLite function mappings
    /// </summary>
    /// <returns>Dictionary of function mappings</returns>
    private static ImmutableDictionary<string, FunctionMap> CreateFunctionMappings() =>
        new Dictionary<string, FunctionMap>
        {
            ["count"] = new(
                "count",
                "COUNT",
                RequiresSpecialHandling: true,
                SpecialHandler: args =>
                    args.Length == 1 && args[0] == "*"
                        ? "COUNT(*)"
                        : $"COUNT({string.Join(", ", args)})"
            ),
            ["sum"] = new("sum", "SUM"),
            ["avg"] = new("avg", "AVG"),
            ["min"] = new("min", "MIN"),
            ["max"] = new("max", "MAX"),
            ["coalesce"] = new("coalesce", "COALESCE"),
            ["length"] = new("length", "LENGTH"),
            ["upper"] = new("upper", "UPPER"),
            ["lower"] = new("lower", "LOWER"),
            ["substring"] = new(
                "substring",
                "SUBSTR",
                RequiresSpecialHandling: true,
                SpecialHandler: args =>
                    args.Length >= 3
                        ? $"substr({args[0]}, {args[1]}, {args[2]})"
                        : $"substr({string.Join(", ", args)})"
            ),
            ["current_date"] = new(
                "current_date",
                "DATETIME",
                RequiresSpecialHandling: true,
                SpecialHandler: _ => "datetime('now')"
            ),
        }.ToImmutableDictionary();

    /// <summary>
    /// Creates the SQLite syntax mapping
    /// </summary>
    /// <returns>The SQLite syntax mapping</returns>
    private static SqlSyntaxMapping CreateSyntaxMapping() =>
        new(
            LimitClause: "LIMIT {0}",
            OffsetClause: "OFFSET {0}",
            DateCurrentFunction: "datetime('now')",
            DateAddFunction: "datetime({2}, '+{0} {1}')",
            StringLengthFunction: "LENGTH",
            StringConcatOperator: "||",
            IdentifierQuoteChar: "\"",
            SupportsBoolean: false
        );
}
