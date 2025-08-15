using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lql.Browser.ViewModels;

/// <summary>
/// ViewModel for the toolbar component
/// </summary>
public partial class ToolbarViewModel : ViewModelBase
{
    [ObservableProperty]
    private object? _fileTabs;

    /// <summary>
    /// Command to connect to a database
    /// </summary>
    public ICommand? ConnectDatabaseCommand { get; set; }

    /// <summary>
    /// Command to execute the query
    /// </summary>
    public ICommand? ExecuteQueryCommand { get; set; }

    /// <summary>
    /// Command to open a file
    /// </summary>
    public ICommand? OpenFileCommand { get; set; }

    /// <summary>
    /// Command to save a file
    /// </summary>
    public ICommand? SaveFileCommand { get; set; }

    /// <summary>
    /// Command to export data as CSV
    /// </summary>
    public ICommand? ExportCsvCommand { get; set; }

    /// <summary>
    /// Command to export data as JSON
    /// </summary>
    public ICommand? ExportJsonCommand { get; set; }
}
