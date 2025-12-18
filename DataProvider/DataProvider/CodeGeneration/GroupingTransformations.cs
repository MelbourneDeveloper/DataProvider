using System.Globalization;
using System.Text;
using Outcome;
using Selecta;

namespace DataProvider.CodeGeneration;

/// <summary>
/// Pure transformation functions for generating grouped query code
/// </summary>
public static class GroupingTransformations
{
    /// <summary>
    /// Generates the grouping method that transforms flat results into parent-child structure
    /// </summary>
    /// <param name="fileName">Base file name</param>
    /// <param name="groupingConfig">Grouping configuration</param>
    /// <returns>Generated grouping method code</returns>
    public static Result<string, SqlError> GenerateGroupingMethod(
        string fileName,
        GroupingConfig groupingConfig
    )
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("fileName cannot be null or empty")
            );

        if (groupingConfig == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("groupingConfig cannot be null")
            );

        var sb = new StringBuilder();

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    private static ImmutableList<{groupingConfig.ParentEntity.Name}> GroupResults(ImmutableList<{fileName}Raw> rawResults)"
        );
        sb.AppendLine("    {");
        sb.AppendLine("        // Group raw results by parent key columns");
        sb.AppendLine("        var parentGroups = rawResults");
        sb.AppendLine("            .GroupBy(r => new { ");

        // Generate grouping key based on parent key columns
        for (int i = 0; i < groupingConfig.ParentEntity.KeyColumns.Count; i++)
        {
            var keyColumn = groupingConfig.ParentEntity.KeyColumns[i];
            var isLast = i == groupingConfig.ParentEntity.KeyColumns.Count - 1;
            var comma = isLast ? "" : ",";
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                {keyColumn} = r.{keyColumn}{comma}"
            );
        }

        sb.AppendLine("            })");
        sb.AppendLine("            .ToList();");
        sb.AppendLine();
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        var result = new List<{groupingConfig.ParentEntity.Name}>();"
        );
        sb.AppendLine("        foreach (var group in parentGroups)");
        sb.AppendLine("        {");
        sb.AppendLine("            var firstItem = group.First();");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            var parent = new {groupingConfig.ParentEntity.Name}("
        );

        // Generate parent properties
        for (int i = 0; i < groupingConfig.ParentEntity.Columns.Count; i++)
        {
            var column = groupingConfig.ParentEntity.Columns[i];
            var isLast = i == groupingConfig.ParentEntity.Columns.Count - 1;
            var comma = isLast ? "" : ",";
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                firstItem.{column}{comma}"
            );
        }
        // Ensure comma between last parent arg and child collection argument
        sb.AppendLine("                ,");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                group.Select(item => new {groupingConfig.ChildEntity.Name}("
        );

        // Generate child properties
        for (int i = 0; i < groupingConfig.ChildEntity.Columns.Count; i++)
        {
            var column = groupingConfig.ChildEntity.Columns[i];
            var isLast = i == groupingConfig.ChildEntity.Columns.Count - 1;
            var comma = isLast ? "" : ",";
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                    item.{column}{comma}"
            );
        }
        sb.AppendLine("                )).ToList()");
        sb.AppendLine("            );");
        sb.AppendLine("            result.Add(parent);");
        sb.AppendLine("        }");
        sb.AppendLine("        return result.ToImmutableList();");
        sb.AppendLine("    }");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates a grouped query method that returns parent-child results
    /// </summary>
    /// <param name="className">Extension class name</param>
    /// <param name="methodName">Method name</param>
    /// <param name="sql">SQL query</param>
    /// <param name="parameters">SQL parameters</param>
    /// <param name="columns">Database columns</param>
    /// <param name="groupingConfig">Grouping configuration</param>
    /// <param name="connectionType">Database connection type</param>
    /// <returns>Generated grouped query method code</returns>
    public static Result<string, SqlError> GenerateGroupedQueryMethod(
        string className,
        string methodName,
        string sql,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<DatabaseColumn> columns,
        GroupingConfig groupingConfig,
        string connectionType = "SqliteConnection"
    )
    {
        if (string.IsNullOrWhiteSpace(className))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("className cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(methodName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("methodName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("sql cannot be null or empty")
            );

        if (columns == null || columns.Count == 0)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("columns cannot be null or empty")
            );

        if (groupingConfig == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("groupingConfig cannot be null")
            );

        var parameterList = DataAccessGenerator.GenerateParameterList(parameters);
        var sb = new StringBuilder();

        // Generate extension class with XML docs
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"/// Extension methods for '{methodName}' grouped query."
        );
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"public static partial class {className}");
        sb.AppendLine("{");

        // Generate method that returns grouped results
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Executes the '{methodName}' query and groups rows into '{groupingConfig.ParentEntity.Name}' with '{groupingConfig.ChildEntity.Name}' children."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <param name=\"connection\">The open {connectionType} connection.</param>"
        );

        if (parameters != null && parameters.Count > 0)
        {
            foreach (var p in parameters)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    /// <param name=\"{p.Name}\">Query parameter.</param>"
                );
            }
        }

        sb.AppendLine("    /// <returns>Result of grouped records or SQL error.</returns>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>> {methodName}Async(this {connectionType} connection{(string.IsNullOrEmpty(parameterList) ? "" : ", " + parameterList)})"
        );
        sb.AppendLine("    {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = @\"{sql.Replace("\"", "\"\"", StringComparison.Ordinal)}\";"
        );
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            var rawResults = ImmutableList.CreateBuilder<{methodName}Raw>();"
        );
        sb.AppendLine();

        var commandType = connectionType.Replace("Connection", "Command", StringComparison.Ordinal);
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            using (var command = new {commandType}(sql, connection))"
        );
        sb.AppendLine("            {");

        // Add parameters
        if (parameters != null)
        {
            foreach (var parameter in parameters)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                command.Parameters.AddWithValue(\"@{parameter.Name}\", {parameter.Name} ?? (object)DBNull.Value);"
                );
            }
        }

        sb.AppendLine();
        sb.AppendLine(
            "                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))"
        );
        sb.AppendLine("                {");
        sb.AppendLine("                    while (await reader.ReadAsync().ConfigureAwait(false))");
        sb.AppendLine("                    {");

        // Generate raw record constructor using real column metadata
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                        var item = new {methodName}Raw("
        );

        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var isLast = i == columns.Count - 1;
            var comma = isLast ? "" : ",";

            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                            reader.IsDBNull({i}) ? ({column.CSharpType}){(column.IsNullable ? "null" : "default")} : ({column.CSharpType})reader.GetValue({i}){comma}"
            );
        }

        sb.AppendLine("                        );");
        sb.AppendLine("                        rawResults.Add(item);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // Group the raw results into parent-child structure");
        sb.AppendLine("            var grouped = GroupResults(rawResults.ToImmutable());");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new Result<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>.Ok<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>(grouped);"
        );
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new Result<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>.Error<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>(new SqlError(\"Database error\", ex));"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        // Add the grouping method
        sb.AppendLine();
        var groupingMethodResult = GenerateGroupingMethod(methodName, groupingConfig);
        if (groupingMethodResult is Result<string, SqlError>.Ok<string, SqlError> groupingSuccess)
        {
            sb.Append(groupingSuccess.Value);
        }
        else if (groupingMethodResult is Result<string, SqlError>.Error<string, SqlError> groupingFailure)
        {
            return groupingFailure;
        }

        sb.AppendLine("}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }
}
