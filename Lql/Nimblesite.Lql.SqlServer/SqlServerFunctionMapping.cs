using System.Collections.Immutable;
using Nimblesite.Lql.Core.FunctionMapping;

namespace Nimblesite.Lql.SqlServer;

/// <summary>
/// SQL Server-specific function mapping implementation
/// </summary>
public sealed class SqlServerFunctionMapping : FunctionMappingProviderBase
{
    /// <summary>
    /// Singleton instance of the SQL Server function mapping
    /// </summary>
    public static readonly SqlServerFunctionMapping Instance = new();

    /// <summary>
    /// Private constructor to enforce singleton pattern
    /// </summary>
    private SqlServerFunctionMapping()
        : base(CreateFunctionMappings, CreateSyntaxMapping) { }

    /// <summary>
    /// Creates the SQL Server function mappings
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
            ["extract"] = new(
                "extract",
                "DATEPART",
                RequiresSpecialHandling: true,
                SpecialHandler: args => $"DATEPART({args[0]}, {args[1]})"
            ),
            ["date_trunc"] = new(
                "date_trunc",
                "DATETRUNC",
                RequiresSpecialHandling: true,
                SpecialHandler: args => $"DATETRUNC({args[0]}, {args[1]})"
            ),
            ["current_date"] = new(
                "current_date",
                "GETDATE",
                RequiresSpecialHandling: true,
                SpecialHandler: _ => "GETDATE()"
            ),
            ["length"] = new("length", "LEN"),
            ["upper"] = new("upper", "UPPER"),
            ["lower"] = new("lower", "LOWER"),
            ["substring"] = new("substring", "SUBSTRING"),
            ["exists"] = new(
                "exists",
                "EXISTS",
                RequiresSpecialHandling: true,
                SpecialHandler: args => $"EXISTS ({string.Join(" ", args)})"
            ),
            ["row_number"] = new("row_number", "ROW_NUMBER"),
            ["rank"] = new("rank", "RANK"),
            ["dense_rank"] = new("dense_rank", "DENSE_RANK"),
            ["lag"] = new("lag", "LAG"),
            ["lead"] = new("lead", "LEAD"),
        }.ToImmutableDictionary();

    /// <summary>
    /// Creates the SQL Server syntax mapping
    /// </summary>
    /// <returns>The SQL Server syntax mapping</returns>
    private static SqlSyntaxMapping CreateSyntaxMapping() =>
        new(
            LimitClause: "TOP {0}",
            OffsetClause: "OFFSET {0} ROWS",
            DateCurrentFunction: "GETDATE()",
            DateAddFunction: "DATEADD({1}, {0}, {2})",
            StringLengthFunction: "LEN",
            StringConcatOperator: "+",
            IdentifierQuoteChar: "[",
            SupportsBoolean: false
        );
}
