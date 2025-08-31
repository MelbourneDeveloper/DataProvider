using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lql.Browser.Models;
using Lql.SQLite;
using Microsoft.Data.Sqlite;
using Results;

namespace Lql.Browser.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private FileTab? _activeTab;

    /// <summary>
    /// ViewModel for the schema panel component
    /// </summary>
    public SchemaPanelViewModel SchemaPanelViewModel { get; }

    /// <summary>
    /// ViewModel for the query editor component
    /// </summary>
    public QueryEditorViewModel QueryEditorViewModel { get; }

    /// <summary>
    /// ViewModel for the results grid component
    /// </summary>
    public ResultsGridViewModel ResultsGridViewModel { get; }

    /// <summary>
    /// ViewModel for the toolbar component
    /// </summary>
    public ToolbarViewModel ToolbarViewModel { get; }

    /// <summary>
    /// ViewModel for the status bar component
    /// </summary>
    public StatusBarViewModel StatusBarViewModel { get; }

    /// <summary>
    /// ViewModel for the file tabs component
    /// </summary>
    public FileTabsViewModel FileTabsViewModel { get; }

    /// <summary>
    /// ViewModel for the messages panel component
    /// </summary>
    public MessagesPanelViewModel MessagesPanelViewModel { get; }

    public ObservableCollection<FileTab> FileTabs { get; } = [];

    /// <summary>
    /// Collection of column names for the current query results
    /// </summary>
    public ObservableCollection<string> ColumnNames { get; } = [];

    private SqliteConnection? _connection;
    private DataTable? _currentDataTable;
    private int _nextTabNumber = 1;

    public ICommand ConnectDatabaseCommand { get; }
    public ICommand NewFileCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand SaveFileCommand { get; }
    public ICommand SaveAsFileCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand SwitchTabCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand SelectTableCommand { get; }
    public ICommand ExecuteQueryCommand { get; }

    public MainWindowViewModel()
    {
        SchemaPanelViewModel = new SchemaPanelViewModel();
        QueryEditorViewModel = new QueryEditorViewModel();
        ResultsGridViewModel = new ResultsGridViewModel();
        ToolbarViewModel = new ToolbarViewModel();
        StatusBarViewModel = new StatusBarViewModel();
        FileTabsViewModel = new FileTabsViewModel();
        MessagesPanelViewModel = new MessagesPanelViewModel();

        ConnectDatabaseCommand = new AsyncRelayCommand(ConnectDatabaseAsync);
        NewFileCommand = new RelayCommand<string>(NewFile);
        OpenFileCommand = new AsyncRelayCommand(OpenFileAsync);
        SaveFileCommand = new AsyncRelayCommand(SaveFileAsync);
        SaveAsFileCommand = new AsyncRelayCommand(SaveAsFileAsync);
        CloseTabCommand = new RelayCommand<FileTab>(CloseTab);
        SwitchTabCommand = new RelayCommand<FileTab>(SwitchTab);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync);
        ExportJsonCommand = new AsyncRelayCommand(ExportJsonAsync);
        SelectTableCommand = new RelayCommand<string>(SelectTable);
        ExecuteQueryCommand = new AsyncRelayCommand(ExecuteQueryAsync);

        SetupComponentCommands();
        SetupComponentEvents();

        StatusBarViewModel.ConnectionStatusText = "Disconnected";
        StatusBarViewModel.ConnectionStatus = ConnectionStatus.Disconnected;
        StatusBarViewModel.DatabasePath = "No database selected";

        // Create initial LQL tab
        NewFile("lql");
    }

    /// <summary>
    /// Sets up command bindings for component view models
    /// </summary>
    private void SetupComponentCommands()
    {
        ToolbarViewModel.ConnectDatabaseCommand = ConnectDatabaseCommand;
        ToolbarViewModel.ExecuteQueryCommand = ExecuteQueryCommand;
        ToolbarViewModel.OpenFileCommand = OpenFileCommand;
        ToolbarViewModel.SaveFileCommand = SaveFileCommand;
        ToolbarViewModel.ExportCsvCommand = ExportCsvCommand;
        ToolbarViewModel.ExportJsonCommand = ExportJsonCommand;

        FileTabsViewModel.SwitchTabCommand = SwitchTabCommand;
        FileTabsViewModel.CloseTabCommand = CloseTabCommand;
        FileTabsViewModel.NewFileCommand = NewFileCommand;
    }

    /// <summary>
    /// Sets up event handlers for component communication
    /// </summary>
    private void SetupComponentEvents() => SchemaPanelViewModel.OnTableSelected = SelectTable;

    private async Task ConnectDatabaseAsync()
    {
        try
        {
            var dialog = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select SQLite Database",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("SQLite Database")
                    {
                        Patterns = ["*.db", "*.sqlite", "*.sqlite3"],
                    },
                ],
            };

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(dialog);
                if (files.Count > 0)
                {
                    _connection?.Close();
                    _connection = await Services.DatabaseConnectionManager.ConnectToDatabaseAsync(
                        files[0].Path.LocalPath,
                        SchemaPanelViewModel,
                        StatusBarViewModel
                    );
                }
            }
        }
        catch (Exception ex)
        {
            StatusBarViewModel.StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void NewFile(string? fileType)
    {
        var tab = FileOperations.CreateNewTab(fileType ?? "lql", _nextTabNumber);
        FileTabs.Add(tab);
        FileOperations.SwitchActiveTab(FileTabs, tab);
        ActiveTab = tab;
        _nextTabNumber++;
    }

    private async Task OpenFileAsync()
    {
        try
        {
            var dialog = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Open File",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("LQL Files")
                    {
                        Patterns = ["*.lql"],
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("SQL Files")
                    {
                        Patterns = ["*.sql"],
                    },
                ],
            };

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(dialog);
                if (files.Count > 0)
                {
                    var filePath = files[0].Path.LocalPath;
                    var contentResult = await FileOperations.ReadFileAsync(filePath);

                    if (contentResult is Result<string, string>.Success success)
                    {
                        var tab = FileOperations.CreateTabFromFile(filePath, success.Value);
                        FileTabs.Add(tab);
                        FileOperations.SwitchActiveTab(FileTabs, tab);
                        ActiveTab = tab;
                    }
                    else if (contentResult is Result<string, string>.Failure failure)
                    {
                        StatusBarViewModel.StatusMessage = failure.ErrorValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusBarViewModel.StatusMessage = $"Error opening file: {ex.Message}";
        }
    }

    private async Task SaveFileAsync()
    {
        if (ActiveTab == null)
            return;

        if (string.IsNullOrEmpty(ActiveTab.FilePath))
        {
            await SaveAsFileAsync();
            return;
        }

        var result = await FileOperations.WriteFileAsync(ActiveTab.FilePath, ActiveTab.Content);
        if (result is Result<Unit, string>.Success)
        {
            ActiveTab.IsModified = false;
            StatusBarViewModel.StatusMessage = $"Saved {ActiveTab.FileName}";
        }
        else if (result is Result<Unit, string>.Failure failure)
        {
            StatusBarViewModel.StatusMessage = failure.ErrorValue;
        }
    }

    private async Task SaveAsFileAsync()
    {
        if (ActiveTab == null)
            return;

        try
        {
            var dialog = new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save File As",
                SuggestedFileName = ActiveTab.FileName,
                FileTypeChoices =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("LQL Files")
                    {
                        Patterns = ["*.lql"],
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("SQL Files")
                    {
                        Patterns = ["*.sql"],
                    },
                ],
            };

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(dialog);
                if (file != null)
                {
                    var filePath = file.Path.LocalPath;
                    var result = await FileOperations.WriteFileAsync(filePath, ActiveTab.Content);

                    if (result is Result<Unit, string>.Success)
                    {
                        FileOperations.UpdateFileType(ActiveTab, filePath);
                        ActiveTab.IsModified = false;
                        StatusBarViewModel.StatusMessage = $"Saved {ActiveTab.FileName}";
                    }
                    else if (result is Result<Unit, string>.Failure failure)
                    {
                        StatusBarViewModel.StatusMessage = failure.ErrorValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusBarViewModel.StatusMessage = $"Error saving file: {ex.Message}";
        }
    }

    private void CloseTab(FileTab? tab)
    {
        if (tab == null)
            return;

        var newActiveTab = FileOperations.RemoveTab(FileTabs, tab);

        if (ActiveTab == tab)
        {
            ActiveTab = newActiveTab;
            if (newActiveTab != null)
            {
                FileOperations.SwitchActiveTab(FileTabs, newActiveTab);
            }
        }

        if (FileTabs.Count == 0)
        {
            NewFile("lql");
        }
    }

    private void SwitchTab(FileTab? tab)
    {
        if (tab == null)
            return;

        FileOperations.SwitchActiveTab(FileTabs, tab);
        ActiveTab = tab;
    }

    private async Task ExportCsvAsync()
    {
        if (_currentDataTable == null || _currentDataTable.Rows.Count == 0)
        {
            StatusBarViewModel.StatusMessage = "No data to export";
            return;
        }

        try
        {
            var dialog = new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Export CSV",
                SuggestedFileName = "export.csv",
                FileTypeChoices =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("CSV Files")
                    {
                        Patterns = ["*.csv"],
                    },
                ],
            };

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(dialog);
                if (file != null)
                {
                    var result = await DataExport.ExportToCsvAsync(
                        _currentDataTable,
                        file.Path.LocalPath
                    );
                    if (result is Result<Unit, string>.Success)
                    {
                        StatusBarViewModel.StatusMessage =
                            $"Exported {_currentDataTable.Rows.Count} rows to CSV";
                    }
                    else if (result is Result<Unit, string>.Failure failure)
                    {
                        StatusBarViewModel.StatusMessage = failure.ErrorValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusBarViewModel.StatusMessage = $"Error exporting CSV: {ex.Message}";
        }
    }

    private async Task ExportJsonAsync()
    {
        if (_currentDataTable == null || _currentDataTable.Rows.Count == 0)
        {
            StatusBarViewModel.StatusMessage = "No data to export";
            return;
        }

        try
        {
            var dialog = new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Export JSON",
                SuggestedFileName = "export.json",
                FileTypeChoices =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("JSON Files")
                    {
                        Patterns = ["*.json"],
                    },
                ],
            };

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(dialog);
                if (file != null)
                {
                    var result = await DataExport.ExportToJsonAsync(
                        _currentDataTable,
                        file.Path.LocalPath
                    );
                    if (result is Result<Unit, string>.Success)
                    {
                        StatusBarViewModel.StatusMessage =
                            $"Exported {_currentDataTable.Rows.Count} rows to JSON";
                    }
                    else if (result is Result<Unit, string>.Failure failure)
                    {
                        StatusBarViewModel.StatusMessage = failure.ErrorValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusBarViewModel.StatusMessage = $"Error exporting JSON: {ex.Message}";
        }
    }

    private void SelectTable(string? tableName)
    {
        if (string.IsNullOrEmpty(tableName))
            return;

        // Create a new LQL tab with the table query
        var newTab = FileOperations.CreateNewTab("lql", _nextTabNumber);
        newTab.Content = $"{tableName} |> select(*) |> limit(50)";
        newTab.IsModified = true;

        FileTabs.Add(newTab);
        FileOperations.SwitchActiveTab(FileTabs, newTab);
        ActiveTab = newTab;
        _nextTabNumber++;
    }

    private async Task ExecuteQueryAsync()
    {
        Console.WriteLine("=== ExecuteQueryAsync Started ===");

        // Clear previous messages
        MessagesPanelViewModel.ClearAll();

        if (_connection == null)
        {
            Console.WriteLine("ERROR: No database connection");
            MessagesPanelViewModel.AddError("No database connection");
            StatusBarViewModel.StatusMessage = "No database connection";
            return;
        }

        if (ActiveTab == null || string.IsNullOrWhiteSpace(ActiveTab.Content))
        {
            Console.WriteLine("ERROR: No query to execute");
            MessagesPanelViewModel.AddError("No query to execute");
            StatusBarViewModel.StatusMessage = "No query to execute";
            return;
        }

        Console.WriteLine($"Query Text: {ActiveTab.Content}");
        Console.WriteLine($"Is LQL Mode: {ActiveTab.FileType == FileType.Lql}");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            StatusBarViewModel.StatusMessage = "Executing query...";
            MessagesPanelViewModel.AddInfo("Starting query execution...");

            string sqlToExecute;

            if (ActiveTab.FileType == FileType.Lql)
            {
                Console.WriteLine("Converting LQL to SQL...");
                MessagesPanelViewModel.AddInfo("Converting LQL to SQL...");
                var lqlStatement = LqlStatementConverter.ToStatement(ActiveTab.Content);
                if (lqlStatement is Result<LqlStatement, SqlError>.Success lqlSuccess)
                {
                    Console.WriteLine("LQL parsed successfully");
                    var sqlResult = lqlSuccess.Value.ToSQLite();
                    if (sqlResult is Result<string, SqlError>.Success sqlSuccess)
                    {
                        sqlToExecute = sqlSuccess.Value;
                        Console.WriteLine($"LQL converted to SQL: {sqlToExecute}");
                        MessagesPanelViewModel.SetTranspiledSql(sqlToExecute);
                        StatusBarViewModel.StatusMessage = $"LQL converted to SQL: {sqlToExecute}";
                    }
                    else if (sqlResult is Result<string, SqlError>.Failure sqlFailure)
                    {
                        Console.WriteLine($"LQL conversion error: {sqlFailure.ErrorValue.Message}");
                        MessagesPanelViewModel.AddError(
                            $"LQL conversion error: {sqlFailure.ErrorValue.Message}"
                        );
                        StatusBarViewModel.StatusMessage =
                            $"LQL conversion error: {sqlFailure.ErrorValue.Message}";
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Unknown LQL conversion result");
                        MessagesPanelViewModel.AddError("Unknown LQL conversion result");
                        StatusBarViewModel.StatusMessage = "Unknown LQL conversion result";
                        return;
                    }
                }
                else if (lqlStatement is Result<LqlStatement, SqlError>.Failure lqlFailure)
                {
                    Console.WriteLine($"LQL parse error: {lqlFailure.ErrorValue.Message}");
                    MessagesPanelViewModel.AddError(
                        $"LQL parse error: {lqlFailure.ErrorValue.Message}"
                    );
                    StatusBarViewModel.StatusMessage =
                        $"LQL parse error: {lqlFailure.ErrorValue.Message}";
                    return;
                }
                else
                {
                    Console.WriteLine("Unknown LQL parse result");
                    MessagesPanelViewModel.AddError("Unknown LQL parse result");
                    StatusBarViewModel.StatusMessage = "Unknown LQL parse result";
                    return;
                }
            }
            else
            {
                sqlToExecute = ActiveTab.Content;
                Console.WriteLine($"Using SQL directly: {sqlToExecute}");
                MessagesPanelViewModel.SetTranspiledSql(sqlToExecute);
                MessagesPanelViewModel.AddInfo("Using SQL query directly");
            }

            Console.WriteLine($"Executing SQL: {sqlToExecute}");
            MessagesPanelViewModel.AddInfo($"Executing SQL query...");

            using var command = _connection.CreateCommand();
            command.CommandText = sqlToExecute;

            Console.WriteLine("Created command, executing reader...");
            using var reader = await command.ExecuteReaderAsync();
            Console.WriteLine("Reader created, loading data...");

            _currentDataTable?.Dispose();
            _currentDataTable = new DataTable();
            _currentDataTable.Load(reader);
            Console.WriteLine(
                $"Data loaded: {_currentDataTable.Rows.Count} rows, {_currentDataTable.Columns.Count} columns"
            );

            // Convert DataTable to QueryResultRow collection
            var results = new ObservableCollection<QueryResultRow>();

            // Get column names
            ColumnNames.Clear();
            foreach (DataColumn column in _currentDataTable.Columns)
            {
                ColumnNames.Add(column.ColumnName);
            }

            // Convert rows
            foreach (DataRow row in _currentDataTable.Rows)
            {
                var resultRow = new QueryResultRow();
                foreach (DataColumn column in _currentDataTable.Columns)
                {
                    var value = row[column] == DBNull.Value ? null : row[column];
                    resultRow[column.ColumnName] = value;
                    Console.WriteLine($"  {column.ColumnName}: {value}");
                }
                results.Add(resultRow);
            }

            stopwatch.Stop();

            ResultsGridViewModel.QueryResults = results;
            Console.WriteLine($"QueryResults set with {results.Count} QueryResultRow items");

            ResultsGridViewModel.ExecutionTime = $"{stopwatch.ElapsedMilliseconds} ms";
            ResultsGridViewModel.RowCount = $"{_currentDataTable.Rows.Count} rows";
            ResultsGridViewModel.ResultsHeader =
                _currentDataTable.Columns.Count > 0
                    ? _currentDataTable.Columns[0].ColumnName
                    : "Results";

            var successMessage =
                $"Query executed successfully in {stopwatch.ElapsedMilliseconds} ms - {_currentDataTable.Rows.Count} rows returned";
            StatusBarViewModel.StatusMessage = successMessage;
            MessagesPanelViewModel.AddSuccess(successMessage);

            Console.WriteLine("=== ExecuteQueryAsync Completed Successfully ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"=== ERROR in ExecuteQueryAsync ===");
            Console.WriteLine($"Exception: {ex}");

            var errorMessage = $"Query execution error: {ex.Message}";
            StatusBarViewModel.StatusMessage = errorMessage;
            MessagesPanelViewModel.AddError(errorMessage);

            // Add the SQL that failed to the messages panel for debugging
            if (!string.IsNullOrEmpty(MessagesPanelViewModel.TranspiledSql))
            {
                MessagesPanelViewModel.AddError(
                    $"Failed SQL: {MessagesPanelViewModel.TranspiledSql}"
                );
            }

            ResultsGridViewModel.QueryResults = null;
            ResultsGridViewModel.ExecutionTime = "";
            ResultsGridViewModel.RowCount = "";
            ResultsGridViewModel.ResultsHeader = "Error";
            ColumnNames.Clear();
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _currentDataTable?.Dispose();
    }
}
