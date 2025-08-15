using System.Data;
using System.Globalization;
using CsvHelper;
using Newtonsoft.Json;
using Results;

namespace Lql.Browser.Models;

/// <summary>
/// Static methods for data export operations
/// </summary>
public static class DataExport
{
    /// <summary>
    /// Exports DataTable to CSV format
    /// </summary>
    public static async Task<Result<Unit, string>> ExportToCsvAsync(
        DataTable dataTable,
        string filePath
    )
    {
        try
        {
            if (dataTable.Rows.Count == 0)
                return new Result<Unit, string>.Failure("No data to export");

            await using var writer = new StreamWriter(filePath);
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            foreach (DataColumn column in dataTable.Columns)
            {
                csv.WriteField(column.ColumnName);
            }
            await csv.NextRecordAsync();

            foreach (DataRow row in dataTable.Rows)
            {
                foreach (var item in row.ItemArray)
                {
                    csv.WriteField(item);
                }
                await csv.NextRecordAsync();
            }

            return new Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return new Result<Unit, string>.Failure($"Error exporting CSV: {ex.Message}");
        }
    }

    /// <summary>
    /// Exports DataTable to JSON format
    /// </summary>
    public static async Task<Result<Unit, string>> ExportToJsonAsync(
        DataTable dataTable,
        string filePath
    )
    {
        try
        {
            if (dataTable.Rows.Count == 0)
                return new Result<Unit, string>.Failure("No data to export");

            var jsonData = new List<Dictionary<string, object?>>();

            foreach (DataRow row in dataTable.Rows)
            {
                var rowData = new Dictionary<string, object?>();
                foreach (DataColumn column in dataTable.Columns)
                {
                    rowData[column.ColumnName] = row[column] == DBNull.Value ? null : row[column];
                }
                jsonData.Add(rowData);
            }

            var json = JsonConvert.SerializeObject(jsonData, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, json);

            return new Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return new Result<Unit, string>.Failure($"Error exporting JSON: {ex.Message}");
        }
    }
}
