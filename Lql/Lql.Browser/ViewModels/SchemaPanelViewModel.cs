using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Lql.Browser.ViewModels;

/// <summary>
/// ViewModel for the schema panel displaying database tables, views, and other objects
/// </summary>
public partial class SchemaPanelViewModel : ViewModelBase
{
    /// <summary>
    /// Collection of table names in the database
    /// </summary>
    public ObservableCollection<string> DatabaseTables { get; } = [];

    /// <summary>
    /// Collection of view names in the database
    /// </summary>
    public ObservableCollection<string> DatabaseViews { get; } = [];

    /// <summary>
    /// Command to handle table selection
    /// </summary>
    public ICommand SelectTableCommand { get; }

    /// <summary>
    /// Action to execute when a table is selected
    /// </summary>
    public Action<string>? OnTableSelected { get; set; }

    public SchemaPanelViewModel()
    {
        SelectTableCommand = new RelayCommand<string>(ExecuteSelectTable);
    }

    private void ExecuteSelectTable(string? tableName)
    {
        if (!string.IsNullOrEmpty(tableName))
        {
            OnTableSelected?.Invoke(tableName);
        }
    }
}
