using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;
using Results;

namespace Lql.Browser.Services;

/// <summary>
/// Service for managing database connections and schema operations
/// </summary>
public static class DatabaseService
{
    /// <summary>
    /// Connects to a SQLite database
    /// </summary>
    /// <param name="databasePath">Path to the database file</param>
    /// <returns>Result containing the connection or error message</returns>
    public static async Task<Result<SqliteConnection, string>> ConnectToDatabaseAsync(
        string databasePath
    )
    {
        try
        {
            Console.WriteLine($"=== Connecting to database: {databasePath} ===");

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();

            Console.WriteLine($"Connection string: {connectionString}");
#pragma warning disable CA2000 // Connection is returned in Result for disposal by caller
            var connection = new SqliteConnection(connectionString);
#pragma warning restore CA2000
            await connection.OpenAsync();
            Console.WriteLine("Database connection opened successfully");

            return new Result<SqliteConnection, string>.Success(connection);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"=== Database connection failed ===");
            Console.WriteLine($"Exception: {ex}");
            return new Result<SqliteConnection, string>.Failure($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads database schema (tables and views)
    /// </summary>
    /// <param name="connection">Active database connection</param>
    /// <returns>Result containing schema information or error message</returns>
    public static async Task<
        Result<(ObservableCollection<string> Tables, ObservableCollection<string> Views), string>
    > LoadDatabaseSchemaAsync(SqliteConnection connection)
    {
        try
        {
            var tables = new ObservableCollection<string>();
            var views = new ObservableCollection<string>();

            var command = connection.CreateCommand();
            command.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }

            command.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'view' ORDER BY name";
            using var viewReader = await command.ExecuteReaderAsync();
            while (await viewReader.ReadAsync())
            {
                views.Add(viewReader.GetString(0));
            }

            return new Result<
                (ObservableCollection<string>, ObservableCollection<string>),
                string
            >.Success((tables, views));
        }
        catch (Exception ex)
        {
            return new Result<
                (ObservableCollection<string>, ObservableCollection<string>),
                string
            >.Failure($"Error loading schema: {ex.Message}");
        }
    }
}
