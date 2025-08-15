using Lql.Browser.ViewModels;
using Microsoft.Data.Sqlite;

namespace Lql.Browser.Services;

/// <summary>
/// Handles database connection and schema loading operations
/// </summary>
public static class DatabaseConnectionManager
{
    /// <summary>
    /// Connects to database and updates UI state
    /// </summary>
    public static async Task<SqliteConnection?> ConnectToDatabaseAsync(
        string databasePath,
        SchemaPanelViewModel schemaPanelViewModel,
        StatusBarViewModel statusBarViewModel
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
#pragma warning disable CA2000 // Connection is returned to caller for disposal
            var connection = new SqliteConnection(connectionString);
#pragma warning restore CA2000
            await connection.OpenAsync();
            Console.WriteLine("Database connection opened successfully");

            statusBarViewModel.DatabasePath = databasePath;
            statusBarViewModel.ConnectionStatusText = "Connected";
            statusBarViewModel.ConnectionStatus = ConnectionStatus.Connected;
            statusBarViewModel.StatusMessage = "Database connected successfully";

            await LoadDatabaseSchemaAsync(connection, schemaPanelViewModel, statusBarViewModel);
            return connection;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"=== Database connection failed ===");
            Console.WriteLine($"Exception: {ex}");
            statusBarViewModel.ConnectionStatusText = "Error";
            statusBarViewModel.ConnectionStatus = ConnectionStatus.Error;
            statusBarViewModel.StatusMessage = $"Connection failed: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Loads database schema into the schema panel
    /// </summary>
    public static async Task LoadDatabaseSchemaAsync(
        SqliteConnection connection,
        SchemaPanelViewModel schemaPanelViewModel,
        StatusBarViewModel statusBarViewModel
    )
    {
        try
        {
            schemaPanelViewModel.DatabaseTables.Clear();
            schemaPanelViewModel.DatabaseViews.Clear();

            var command = connection.CreateCommand();
            command.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                schemaPanelViewModel.DatabaseTables.Add(reader.GetString(0));
            }

            command.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'view' ORDER BY name";
            using var viewReader = await command.ExecuteReaderAsync();
            while (await viewReader.ReadAsync())
            {
                schemaPanelViewModel.DatabaseViews.Add(viewReader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            statusBarViewModel.StatusMessage = $"Error loading schema: {ex.Message}";
        }
    }
}
