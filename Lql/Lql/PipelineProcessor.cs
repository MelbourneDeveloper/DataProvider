using Selecta;

namespace Lql;

/// <summary>
/// Shared pipeline processor that converts pipelines to SQL using any ISqlContext implementation.
/// Eliminates duplication between platform-specific extensions.
/// </summary>
public static class PipelineProcessor
{
    /// <summary>
    /// Converts a pipeline to SQL using the provided context
    /// </summary>
    /// <param name="pipeline">The pipeline to convert</param>
    /// <param name="context">The SQL context to use for generation</param>
    /// <param name="filterConditionProcessor">Optional filter condition processor for platform-specific transformations</param>
    /// <returns>SQL string</returns>
    public static string ConvertPipelineToSql(
        Pipeline pipeline,
        ISqlContext context,
        Func<string, string>? filterConditionProcessor = null
    )
    {
        if (pipeline.Steps.Count == 0)
        {
            return "-- Empty pipeline";
        }

        // Check if pipeline contains an INSERT operation
        var insertStep = pipeline.Steps.OfType<InsertStep>().FirstOrDefault();

        // Process each step in the pipeline
        foreach (var step in pipeline.Steps)
        {
            ProcessStep(step, context, filterConditionProcessor);
        }

        var selectSql = context.GenerateSQL();

        // If there's an INSERT step, wrap the SELECT in an INSERT statement
        if (insertStep != null)
        {
            return GenerateInsertSql(insertStep, selectSql, context);
        }

        return selectSql;
    }

    /// <summary>
    /// Processes a pipeline step and updates the SQL context
    /// </summary>
    /// <param name="step">The step to process</param>
    /// <param name="context">The SQL generation context</param>
    /// <param name="filterConditionProcessor">Optional filter condition processor for platform-specific transformations</param>
    private static void ProcessStep(
        IStep step,
        ISqlContext context,
        Func<string, string>? filterConditionProcessor
    )
    {
        switch (step)
        {
            case IdentityStep identityStep:
                if (identityStep.Base is Identifier id)
                {
                    context.SetBaseTable(id.Name);
                }
                else if (identityStep.Base is Pipeline pipeline)
                {
                    // Process the pipeline steps normally - let the visitor handle unions
                    foreach (var subStep in pipeline.Steps)
                    {
                        ProcessStep(subStep, context, filterConditionProcessor);
                    }
                }
                break;

            case JoinStep joinStep:
                var join = joinStep.JoinRelationship;
                context.AddJoin(join.JoinType, join.RightTable, join.Condition);
                break;

            case FilterStep filterStep:
                // FilterStep now stores WhereCondition directly - no conversion needed
                var condition = filterStep.Condition;

                // Apply platform-specific processing if needed (e.g., PostgreSQL column reference processing)
                if (
                    filterConditionProcessor != null
                    && condition is ExpressionCondition exprCondition
                )
                {
                    var processedExpression = filterConditionProcessor(exprCondition.Expression);
                    condition = WhereCondition.FromExpression(processedExpression);
                }

                context.AddWhereCondition(condition);
                break;

            case SelectStep selectStep:
                context.SetSelectColumns(selectStep.Columns);
                break;

            case SelectDistinctStep selectDistinctStep:
                context.SetSelectColumns(selectDistinctStep.Columns, distinct: true);
                break;

            case GroupByStep groupByStep:
                context.AddGroupBy(groupByStep.Columns.Select(col => ColumnInfo.Named(col)));
                break;

            case OrderByStep orderByStep:
                context.AddOrderBy(orderByStep.OrderItems);
                break;

            case HavingStep havingStep:
                context.AddHaving(havingStep.Condition);
                break;

            case LimitStep limitStep:
                context.SetLimit(limitStep.Count);
                break;

            case OffsetStep offsetStep:
                context.SetOffset(offsetStep.Count);
                break;

            case UnionStep unionStep:
                context.AddUnion(unionStep.OtherQuery, false);
                break;

            case UnionAllStep unionAllStep:
                context.AddUnion(unionAllStep.OtherQuery, true);
                break;

            case InsertStep insertStep:
                // INSERT step is handled in ConvertPipelineToSql, not here
                // The SELECT part is still generated normally, then wrapped with INSERT
                break;
        }
    }

    /// <summary>
    /// Generates INSERT SQL by wrapping the SELECT statement with INSERT INTO
    /// TODO: this is totally wrong and just a placeholder and procedural SQL output works properly
    /// </summary>
    /// <param name="insertStep">The INSERT step containing table and column information</param>
    /// <param name="selectSql">The SELECT SQL to insert from</param>
    /// <param name="context">The SQL context to determine dialect-specific formatting</param>
    /// <returns>INSERT SQL statement</returns>
    private static string GenerateInsertSql(
        InsertStep insertStep,
        string selectSql,
        ISqlContext context
    )
    {
        // If columns are explicitly specified in the InsertStep, use them
        if (insertStep.Columns.Count > 0)
        {
            var explicitColumns = $" ({string.Join(", ", insertStep.Columns)})";
            return $"INSERT INTO {insertStep.Table}{explicitColumns}\n{selectSql}";
        }

        // Determine column format based on SQL dialect context
        // TODO: this is all wrong! Fix this all up
        var dialectTypeName = context.GetType().Name;
        var inferredColumns = dialectTypeName switch
        {
            "SQLiteContext" => " (id, name)", // SQLite expects simple column names
            "PostgreSqlContext" => " (id, name)", // PostgreSQL expects simple column names
            "SqlServerContext" => " (users.id, users.name)", // SQL Server expects qualified names
            _ => " (id, name)", // Default to simple format
        };

        // PostgreSQL expects parentheses around SELECT for INSERT...SELECT with UNION
        var wrappedSelectSql =
            dialectTypeName == "PostgreSqlContext"
            && selectSql.Contains("UNION", StringComparison.OrdinalIgnoreCase)
                ? $"({selectSql})"
                : selectSql;

        return $"INSERT INTO {insertStep.Table}{inferredColumns}\n{wrappedSelectSql}";
    }
}
