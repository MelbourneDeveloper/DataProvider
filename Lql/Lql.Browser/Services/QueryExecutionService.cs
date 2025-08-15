using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using Lql.Browser.Models;
using Lql.SQLite;
using Microsoft.Data.Sqlite;
using Results;

namespace Lql.Browser.Services;

/// <summary>
/// Service for executing queries and processing results
/// </summary>
public static class QueryExecutionService
{
    /// <summary>
    /// Executes a query (LQL or SQL) and returns formatted results
    /// </summary>
    /// <param name="connection">Active database connection</param>
    /// <param name="queryText">Query text to execute</param>
    /// <param name="isLqlMode">Whether the query is in LQL format</param>
    /// <returns>Result containing query execution results or error message</returns>
    public static async Task<Result<QueryExecutionResult, string>> ExecuteQueryAsync(
        SqliteConnection connection,
        string queryText,
        bool isLqlMode
    )
    {
        try
        {
            Console.WriteLine("=== ExecuteQueryAsync Started ===");
            Console.WriteLine($"Query Text: {queryText}");
            Console.WriteLine($"Is LQL Mode: {isLqlMode}");

            var stopwatch = Stopwatch.StartNew();

            // Convert LQL to SQL if needed
            var sqlToExecute = isLqlMode
                ? ConvertLqlToSql(queryText)
                : new Result<string, string>.Success(queryText);

            if (sqlToExecute is Result<string, string>.Failure sqlFailure)
            {
                return new Result<QueryExecutionResult, string>.Failure(sqlFailure.ErrorValue);
            }

            var sql = ((Result<string, string>.Success)sqlToExecute).Value;
            Console.WriteLine($"Executing SQL: {sql}");

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            Console.WriteLine("Created command, executing reader...");
            using var reader = await command.ExecuteReaderAsync();
            Console.WriteLine("Reader created, loading data...");

            var dataTable = new DataTable();
            dataTable.Load(reader);
            Console.WriteLine(
                $"Data loaded: {dataTable.Rows.Count} rows, {dataTable.Columns.Count} columns"
            );

            // Convert DataTable to QueryResultRow collection
            var results = new ObservableCollection<QueryResultRow>();

            foreach (DataRow row in dataTable.Rows)
            {
                var resultRow = new QueryResultRow();
                foreach (DataColumn column in dataTable.Columns)
                {
                    var value = row[column] == DBNull.Value ? null : row[column];
                    resultRow[column.ColumnName] = value;
                    Console.WriteLine($"  {column.ColumnName}: {value}");
                }
                results.Add(resultRow);
            }

            stopwatch.Stop();

            var executionResult = new QueryExecutionResult
            {
                QueryResults = results,
                ExecutionTime = $"{stopwatch.ElapsedMilliseconds} ms",
                RowCount = $"{dataTable.Rows.Count} rows",
                ResultsHeader =
                    dataTable.Columns.Count > 0 ? dataTable.Columns[0].ColumnName : "Results",
                DataTable = dataTable,
            };

            Console.WriteLine("=== ExecuteQueryAsync Completed Successfully ===");
            return new Result<QueryExecutionResult, string>.Success(executionResult);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"=== ERROR in ExecuteQueryAsync ===");
            Console.WriteLine($"Exception: {ex}");
            return new Result<QueryExecutionResult, string>.Failure(
                $"Query execution error: {ex.Message}"
            );
        }
    }

    private static Result<string, string> ConvertLqlToSql(string lqlQuery)
    {
        try
        {
            Console.WriteLine("Converting LQL to SQL...");
            var lqlStatement = LqlStatementConverter.ToStatement(lqlQuery);

            if (lqlStatement is Result<LqlStatement, SqlError>.Success lqlSuccess)
            {
                Console.WriteLine("LQL parsed successfully");
                var sqlResult = lqlSuccess.Value.ToSQLite();

                if (sqlResult is Result<string, SqlError>.Success sqlSuccess)
                {
                    Console.WriteLine($"LQL converted to SQL: {sqlSuccess.Value}");
                    return new Result<string, string>.Success(sqlSuccess.Value);
                }
                else if (sqlResult is Result<string, SqlError>.Failure sqlFailure)
                {
                    Console.WriteLine($"LQL conversion error: {sqlFailure.ErrorValue.Message}");
                    return new Result<string, string>.Failure(
                        $"LQL conversion error: {sqlFailure.ErrorValue.Message}"
                    );
                }
                else
                {
                    Console.WriteLine("Unknown LQL conversion result");
                    return new Result<string, string>.Failure("Unknown LQL conversion result");
                }
            }
            else if (lqlStatement is Result<LqlStatement, SqlError>.Failure lqlFailure)
            {
                Console.WriteLine($"LQL parse error: {lqlFailure.ErrorValue.Message}");
                return new Result<string, string>.Failure(
                    $"LQL parse error: {lqlFailure.ErrorValue.Message}"
                );
            }
            else
            {
                Console.WriteLine("Unknown LQL parse result");
                return new Result<string, string>.Failure("Unknown LQL parse result");
            }
        }
        catch (Exception ex)
        {
            return new Result<string, string>.Failure($"LQL processing error: {ex.Message}");
        }
    }
}
