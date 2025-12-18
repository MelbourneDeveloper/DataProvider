using System.Globalization;
using System.Text;
using Outcome;
using Selecta;

namespace DataProvider.CodeGeneration;

/// <summary>
/// Static methods for generating data access extension methods
/// </summary>
public static class DataAccessGenerator
{
    /// <summary>
    /// Generates parameter list string for method signatures
    /// </summary>
    /// <param name="parameters">SQL parameters</param>
    /// <returns>Formatted parameter list</returns>
    public static string GenerateParameterList(IReadOnlyList<ParameterInfo> parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return "";

        return string.Join(", ", parameters.Select(p => $"object {p.Name}"));
    }

    /// <summary>
    /// Generates a data access extension method for querying
    /// </summary>
    /// <param name="className">Extension class name</param>
    /// <param name="methodName">Method name</param>
    /// <param name="returnTypeName">Return type name</param>
    /// <param name="sql">SQL query</param>
    /// <param name="parameters">SQL parameters</param>
    /// <param name="columns">Database columns</param>
    /// <param name="connectionType">Database connection type (e.g., SqliteConnection)</param>
    /// <returns>Generated extension method code</returns>
    public static Result<string, SqlError> GenerateQueryMethod(
        string className,
        string methodName,
        string returnTypeName,
        string sql,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<DatabaseColumn> columns,
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

        if (string.IsNullOrWhiteSpace(returnTypeName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("returnTypeName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("sql cannot be null or empty")
            );

        if (columns == null || columns.Count == 0)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("columns cannot be null or empty")
            );

        var parameterList = GenerateParameterList(parameters);
        var sb = new StringBuilder();

        // Generate extension class
        sb.AppendLine("/// <summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"/// Extension methods for '{methodName}'.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"public static partial class {className}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Executes '{methodName}.sql' and maps results."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <param name=\"connection\">Open {connectionType} connection.</param>"
        );

        if (parameters != null)
        {
            foreach (var p in parameters)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    /// <param name=\"{p.Name}\">Query parameter.</param>"
                );
            }
        }

        sb.AppendLine("    /// <returns>Result of records or SQL error.</returns>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<ImmutableList<{returnTypeName}>, SqlError>> {methodName}Async(this {connectionType} connection{(string.IsNullOrEmpty(parameterList) ? "" : ", " + parameterList)})"
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
            $"            var results = ImmutableList.CreateBuilder<{returnTypeName}>();"
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

        // Generate record constructor using column metadata
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                        var item = new {returnTypeName}("
        );

        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var isLast = i == columns.Count - 1;
            var comma = isLast ? "" : ",";

            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                            reader.IsDBNull({i}) ? {(column.IsNullable ? "null" : $"default({column.CSharpType})")} : ({column.CSharpType})reader.GetValue({i}){comma}"
            );
        }

        sb.AppendLine("                        );");
        sb.AppendLine("                        results.Add(item);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new Result<ImmutableList<{returnTypeName}>, SqlError>.Ok<ImmutableList<{returnTypeName}>, SqlError>(results.ToImmutable());"
        );
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new Result<ImmutableList<{returnTypeName}>, SqlError>.Error<ImmutableList<{returnTypeName}>, SqlError>(new SqlError(\"Database error\", ex));"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates an INSERT method for a database table
    /// </summary>
    /// <param name="table">Database table metadata</param>
    /// <param name="connectionType">Database connection type</param>
    /// <returns>Generated INSERT method code</returns>
    public static Result<string, SqlError> GenerateInsertMethod(
        DatabaseTable table,
        string connectionType = "SqliteConnection"
    )
    {
        if (table == null)
            return new Result<string, SqlError>.Error<string, SqlError>(new SqlError("table cannot be null"));

        var insertableColumns = table.InsertableColumns;
        if (insertableColumns.Count == 0)
            return new Result<string, SqlError>.Ok<string, SqlError>("");

        var sb = new StringBuilder();
        var parameterList = string.Join(
            ", ",
            insertableColumns.Select(c =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{c.CSharpType} {c.Name.ToLowerInvariant()}"
                )
            )
        );

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Inserts a new row into the {table.Name} table."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<long, SqlError>> Insert{table.Name}Async(this IDbTransaction transaction, {parameterList})"
        );
        sb.AppendLine("    {");

        // Generate INSERT SQL
        var columnNames = string.Join(", ", insertableColumns.Select(c => c.Name));
        var parameterNames = string.Join(", ", insertableColumns.Select(c => $"@{c.Name}"));

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = \"INSERT INTO {table.Name} ({columnNames}) VALUES ({parameterNames}); SELECT last_insert_rowid()\";"
        );
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");

        var commandType = connectionType.Replace("Connection", "Command", StringComparison.Ordinal);
        var transactionType = connectionType.Replace(
            "Connection",
            "Transaction",
            StringComparison.Ordinal
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            using (var command = new {commandType}(sql, ({connectionType})transaction.Connection, ({transactionType})transaction))"
        );
        sb.AppendLine("            {");

        // Add parameters
        foreach (var column in insertableColumns)
        {
            var paramName = column.Name.ToLowerInvariant();
            if (column.IsNullable)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                command.Parameters.AddWithValue(\"@{column.Name}\", {paramName} ?? (object)DBNull.Value);"
                );
            }
            else
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                command.Parameters.AddWithValue(\"@{column.Name}\", {paramName});"
                );
            }
        }

        sb.AppendLine();
        sb.AppendLine(
            "                var result = await command.ExecuteScalarAsync().ConfigureAwait(false);"
        );
        sb.AppendLine("                if (result == null || result == DBNull.Value)");
        sb.AppendLine(
            "                    return new Result<long, SqlError>.Error<long, SqlError>(new SqlError(\"Insert failed: no ID returned\"));"
        );
        sb.AppendLine(
            "                var newId = Convert.ToInt64(result, CultureInfo.InvariantCulture);"
        );
        sb.AppendLine("                return new Result<long, SqlError>.Ok<long, SqlError>(newId);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            return new Result<long, SqlError>.Error<long, SqlError>(new SqlError(\"Insert failed\", ex));"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates an UPDATE method for a database table
    /// </summary>
    /// <param name="table">Database table metadata</param>
    /// <param name="connectionType">Database connection type</param>
    /// <returns>Generated UPDATE method code</returns>
    public static Result<string, SqlError> GenerateUpdateMethod(
        DatabaseTable table,
        string connectionType = "SqliteConnection"
    )
    {
        if (table == null)
            return new Result<string, SqlError>.Error<string, SqlError>(new SqlError("table cannot be null"));

        var updateableColumns = table.UpdateableColumns;
        var primaryKeyColumns = table.PrimaryKeyColumns;

        if (updateableColumns.Count == 0 || primaryKeyColumns.Count == 0)
            return new Result<string, SqlError>.Ok<string, SqlError>("");

        var sb = new StringBuilder();
        var allColumns = primaryKeyColumns.Concat(updateableColumns).ToList();
        var parameterList = string.Join(
            ", ",
            allColumns.Select(c =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{c.CSharpType} {c.Name.ToLowerInvariant()}"
                )
            )
        );

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Updates a row in the {table.Name} table."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<int, SqlError>> Update{table.Name}Async(this IDbTransaction transaction, {parameterList})"
        );
        sb.AppendLine("    {");

        // Generate UPDATE SQL
        var setClause = string.Join(", ", updateableColumns.Select(c => $"{c.Name} = @{c.Name}"));
        var whereClause = string.Join(
            " AND ",
            primaryKeyColumns.Select(c => $"{c.Name} = @{c.Name}")
        );

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = \"UPDATE {table.Name} SET {setClause} WHERE {whereClause}\";"
        );
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");

        var commandType = connectionType.Replace("Connection", "Command", StringComparison.Ordinal);
        var transactionType = connectionType.Replace(
            "Connection",
            "Transaction",
            StringComparison.Ordinal
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            using (var command = new {commandType}(sql, ({connectionType})transaction.Connection, ({transactionType})transaction))"
        );
        sb.AppendLine("            {");

        // Add parameters
        foreach (var column in allColumns)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                command.Parameters.AddWithValue(\"@{column.Name}\", {column.Name.ToLowerInvariant()});"
            );
        }

        sb.AppendLine();
        sb.AppendLine(
            "                var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);"
        );
        sb.AppendLine("                return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Update failed\", ex));"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }
}
