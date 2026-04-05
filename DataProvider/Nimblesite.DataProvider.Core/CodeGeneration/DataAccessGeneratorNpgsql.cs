namespace Nimblesite.DataProvider.Core.CodeGeneration;

/// <summary>
/// Npgsql type resolution helpers for DataAccessGenerator
/// </summary>
public static partial class DataAccessGenerator
{
    /// <summary>
    /// Resolves the NpgsqlDbType for a parameter by matching its name to result columns.
    /// Falls back to Text when no matching column is found.
    /// </summary>
    internal static string ResolveNpgsqlDbType(
        string parameterName,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        var matchingColumn = columns?.FirstOrDefault(c =>
            string.Equals(c.Name, parameterName, StringComparison.OrdinalIgnoreCase)
        );

        var sqlType = matchingColumn?.SqlType ?? "TEXT";
        return MapSqlTypeToNpgsqlDbType(sqlType);
    }

    internal static string MapSqlTypeToNpgsqlDbType(string sqlType) =>
        sqlType.ToLowerInvariant() switch
        {
            "INTEGER" or "INT" => "Integer",
            "BIGINT" => "Bigint",
            "SMALLINT" => "Smallint",
            "BOOLEAN" => "Boolean",
            "REAL" or "FLOAT" => "Real",
            "DOUBLE" or "DOUBLE PRECISION" => "Double",
            "NUMERIC" or "DECIMAL" => "Numeric",
            "TEXT" or "NVARCHAR" or "VARCHAR" or "CHAR" or "NCHAR" => "Text",
            "BYTEA" or "BLOB" or "BINARY" or "VARBINARY" => "Bytea",
            "UUID" => "Uuid",
            "DATE" => "Date",
            "TIME" => "Time",
            "TIMESTAMP" or "DATETIME" => "Timestamp",
            "TIMESTAMPTZ" => "TimestampTz",
            "JSONB" => "Jsonb",
            "JSON" => "Json",
            "XML" => "Xml",
            _ => "Text",
        };
}
