using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using Lql.Browser.Models;
using Lql.Browser.ViewModels;
using Lql.SQLite;
using Microsoft.Data.Sqlite;
using Outcome;
using Selecta;

namespace Lql.Browser.Services;

/// <summary>
/// Handles query execution and result processing
/// </summary>
public static class QueryExecutor
{
    /// <summary>
    /// Executes a query and updates the results grid
    /// </summary>
    public static async Task<DataTable?> ExecuteQueryAsync(
        SqliteConnection connection,
        FileTab activeTab,
        ResultsGridViewModel resultsGridViewModel,
        StatusBarViewModel statusBarViewModel
    )
    {
        Console.WriteLine("=== ExecuteQueryAsync Started ===");

        if (string.IsNullOrWhiteSpace(activeTab.Content))
        {
            Console.WriteLine("ERROR: No query to execute");
            statusBarViewModel.StatusMessage = "No query to execute";
            return null;
        }

        Console.WriteLine($"Query Text: {activeTab.Content}");
        Console.WriteLine($"Is LQL Mode: {activeTab.FileType == FileType.Lql}");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            statusBarViewModel.StatusMessage = "Executing query...";

            string sqlToExecute;

            if (activeTab.FileType == FileType.Lql)
            {
                Console.WriteLine("Converting LQL to SQL...");
                var lqlStatement = LqlStatementConverter.ToStatement(activeTab.Content);
                if (
                    lqlStatement
                    is Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError> lqlSuccess
                )
                {
                    Console.WriteLine("LQL parsed successfully");
                    var sqlResult = lqlSuccess.Value.ToSQLite();
                    if (sqlResult is Result<string, SqlError>.Ok<string, SqlError> sqlSuccess)
                    {
                        sqlToExecute = sqlSuccess.Value;
                        Console.WriteLine($"LQL converted to SQL: {sqlToExecute}");
                        statusBarViewModel.StatusMessage = $"LQL converted to SQL: {sqlToExecute}";
                    }
                    else if (
                        sqlResult is Result<string, SqlError>.Error<string, SqlError> sqlFailure
                    )
                    {
                        Console.WriteLine($"LQL conversion error: {sqlFailure.Value.Message}");
                        statusBarViewModel.StatusMessage =
                            $"LQL conversion error: {sqlFailure.Value.Message}";
                        return null;
                    }
                    else
                    {
                        Console.WriteLine("Unknown LQL conversion result");
                        statusBarViewModel.StatusMessage = "Unknown LQL conversion result";
                        return null;
                    }
                }
                else if (
                    lqlStatement
                    is Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError> lqlFailure
                )
                {
                    Console.WriteLine($"LQL parse error: {lqlFailure.Value.Message}");
                    statusBarViewModel.StatusMessage =
                        $"LQL parse error: {lqlFailure.Value.Message}";
                    return null;
                }
                else
                {
                    Console.WriteLine("Unknown LQL parse result");
                    statusBarViewModel.StatusMessage = "Unknown LQL parse result";
                    return null;
                }
            }
            else
            {
                sqlToExecute = activeTab.Content;
                Console.WriteLine($"Using SQL directly: {sqlToExecute}");
            }

            Console.WriteLine($"Executing SQL: {sqlToExecute}");

            using var command = connection.CreateCommand();
            command.CommandText = sqlToExecute;

            Console.WriteLine("Created command, executing reader...");
            using var reader = await command.ExecuteReaderAsync();
            Console.WriteLine("Reader created, loading data...");

            var currentDataTable = new DataTable();
            currentDataTable.Load(reader);
            Console.WriteLine(
                $"Data loaded: {currentDataTable.Rows.Count} rows, {currentDataTable.Columns.Count} columns"
            );

            // Convert DataTable to QueryResultRow collection
            var results = new ObservableCollection<QueryResultRow>();

            foreach (DataRow row in currentDataTable.Rows)
            {
                var resultRow = new QueryResultRow();
                foreach (DataColumn column in currentDataTable.Columns)
                {
                    var value = row[column] == DBNull.Value ? null : row[column];
                    resultRow[column.ColumnName] = value;
                    Console.WriteLine($"  {column.ColumnName}: {value}");
                }
                results.Add(resultRow);
            }

            stopwatch.Stop();

            resultsGridViewModel.QueryResults = results;
            Console.WriteLine($"QueryResults set with {results.Count} QueryResultRow items");

            resultsGridViewModel.ExecutionTime = $"{stopwatch.ElapsedMilliseconds} ms";
            resultsGridViewModel.RowCount = $"{currentDataTable.Rows.Count} rows";
            resultsGridViewModel.ResultsHeader =
                currentDataTable.Columns.Count > 0
                    ? currentDataTable.Columns[0].ColumnName
                    : "Results";
            statusBarViewModel.StatusMessage =
                $"Query executed successfully in {stopwatch.ElapsedMilliseconds} ms";

            Console.WriteLine("=== ExecuteQueryAsync Completed Successfully ===");
            return currentDataTable;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"=== ERROR in ExecuteQueryAsync ===");
            Console.WriteLine($"Exception: {ex}");
            statusBarViewModel.StatusMessage = $"Query execution error: {ex.Message}";
            resultsGridViewModel.QueryResults = null;
            resultsGridViewModel.ExecutionTime = "";
            resultsGridViewModel.RowCount = "";
            resultsGridViewModel.ResultsHeader = "Error";
            return null;
        }
    }
}
