using DataProvider.CodeGeneration;
using Microsoft.Data.Sqlite;
using Results;
using Selecta;
using System.Diagnostics.CodeAnalysis;

namespace DataProvider.SQLite.CodeGeneration;

/// <summary>
/// SQLite-specific database effects implementation
/// </summary>
[ExcludeFromCodeCoverage]
public class SqliteDatabaseEffects : IDatabaseEffects
{
    /// <summary>
    /// Gets column metadata by executing the SQL query against the SQLite database.
    /// </summary>
    public async Task<
        Result<IReadOnlyList<DatabaseColumn>, SqlError>
    > GetColumnMetadataFromSqlAsync(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    )
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure(
                new SqlError("connectionString cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure(
                new SqlError("sql cannot be null or empty")
            );

        if (parameters == null)
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure(
                new SqlError("parameters cannot be null")
            );

        var columns = new List<DatabaseColumn>();

        try
        {
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            // The SQL is provided by the developer at compile time, not user input
#pragma warning disable CA2100
            using var command = new SqliteCommand(sql, connection);
#pragma warning restore CA2100

            // Add parameters with dummy values to get schema
            foreach (var param in parameters)
            {
                var dummyValue = GetDummyValueForParameter(param);
                command.Parameters.AddWithValue($"@{param.Name}", dummyValue);
            }

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            // Get column schema from the reader
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var fieldType = reader.GetFieldType(i);
                var dataTypeName = reader.GetDataTypeName(i);

                var csharpType = MapSqliteTypeToCSharpType(fieldType);

                columns.Add(
                    new DatabaseColumn
                    {
                        Name = columnName,
                        SqlType = dataTypeName,
                        CSharpType = csharpType,
                        IsNullable =
                            !fieldType.IsValueType || Nullable.GetUnderlyingType(fieldType) != null,
                        IsPrimaryKey = false, // Cannot determine from query result
                        IsIdentity = false, // Cannot determine from query result
                        IsComputed = false, // Cannot determine from query result
                    }
                );
            }
        }
        catch (SqliteException ex)
        {
            // If we can't execute the query, return error
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure(
                new SqlError("Failed to get column metadata", ex)
            );
        }
        catch (Exception ex)
        {
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure(
                new SqlError("Unexpected error getting column metadata", ex)
            );
        }

        return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Success(columns.AsReadOnly());
    }

    /// <summary>
    /// Maps SQLite data types to C# types
    /// </summary>
    private static string MapSqliteTypeToCSharpType(Type fieldType)
    {
        // Use the .NET type first as it's more accurate
        if (fieldType == typeof(int) || fieldType == typeof(int?))
            return fieldType == typeof(int?) ? "int?" : "int";

        if (fieldType == typeof(long) || fieldType == typeof(long?))
            return fieldType == typeof(long?) ? "long?" : "long";

        if (fieldType == typeof(double) || fieldType == typeof(double?))
            return fieldType == typeof(double?) ? "double?" : "double";

        if (fieldType == typeof(decimal) || fieldType == typeof(decimal?))
            return fieldType == typeof(decimal?) ? "decimal?" : "decimal";

        if (fieldType == typeof(bool) || fieldType == typeof(bool?))
            return fieldType == typeof(bool?) ? "bool?" : "bool";

        if (fieldType == typeof(DateTime) || fieldType == typeof(DateTime?))
            return fieldType == typeof(DateTime?) ? "DateTime?" : "DateTime";

        if (fieldType == typeof(string))
            return "string";

        if (fieldType == typeof(byte[]))
            return "byte[]";

        if (fieldType == typeof(Guid) || fieldType == typeof(Guid?))
            return fieldType == typeof(Guid?) ? "Guid?" : "Guid";

        // Fall back to string for unknown types
        return "string";
    }

    /// <summary>
    /// Gets a dummy value for a parameter based on its name for schema discovery
    /// </summary>
    private static object GetDummyValueForParameter(ParameterInfo parameter)
    {
        var lowerName = parameter.Name.ToLowerInvariant();

        return lowerName switch
        {
            var name when name.Contains("id", StringComparison.Ordinal) => 1,
            var name when name.Contains("count", StringComparison.Ordinal) => 1,
            var name when name.Contains("quantity", StringComparison.Ordinal) => 1,
            var name when name.Contains("amount", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("price", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("total", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("percentage", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("date", StringComparison.Ordinal) => DateTime.UtcNow,
            var name when name.Contains("time", StringComparison.Ordinal) => DateTime.UtcNow,
            var name when name.Contains("created", StringComparison.Ordinal) => DateTime.UtcNow,
            var name when name.Contains("updated", StringComparison.Ordinal) => DateTime.UtcNow,
            _ => "dummy_value",
        };
    }
}
