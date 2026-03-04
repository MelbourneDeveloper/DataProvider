using System.Text.Json;

namespace Reporting.Engine;

/// <summary>
/// Serializes report execution results to various output formats.
/// </summary>
public static class FormatAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Serializes a report execution result to JSON.
    /// </summary>
    /// <param name="result">The execution result to serialize.</param>
    /// <returns>JSON string representation of the result.</returns>
    public static string ToJson(ReportExecutionResult result) =>
        JsonSerializer.Serialize(value: result, options: JsonOptions);

    /// <summary>
    /// Serializes a report execution result to CSV for a specific data source.
    /// </summary>
    /// <param name="result">The data source result to serialize.</param>
    /// <returns>CSV string representation.</returns>
    public static string ToCsv(DataSourceResult result)
    {
        var lines = new List<string>(capacity: result.TotalRows + 1)
        {
            string.Join(",", result.ColumnNames),
        };

        foreach (var row in result.Rows)
        {
            lines.Add(string.Join(",", row.Select(EscapeCsvValue)));
        }

        return string.Join("\n", lines);
    }

    private static string EscapeCsvValue(object? value)
    {
        if (value is null)
        {
            return "";
        }

        var str = value.ToString() ?? "";
        if (
            str.Contains(',', StringComparison.Ordinal)
            || str.Contains('"', StringComparison.Ordinal)
            || str.Contains('\n', StringComparison.Ordinal)
        )
        {
            return $"\"{str.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return str;
    }
}
