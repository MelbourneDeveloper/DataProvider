using System.Collections.ObjectModel;
using System.Windows.Input;
using Lql.Browser.Models;

namespace Lql.Browser.ViewModels;

/// <summary>
/// ViewModel for the file tabs component
/// </summary>
public partial class FileTabsViewModel : ViewModelBase
{
    /// <summary>
    /// Collection of file tabs
    /// </summary>
    public ObservableCollection<FileTab> Tabs { get; } = [];

    /// <summary>
    /// Command to switch to a tab
    /// </summary>
    public ICommand? SwitchTabCommand { get; set; }

    /// <summary>
    /// Command to close a tab
    /// </summary>
    public ICommand? CloseTabCommand { get; set; }

    /// <summary>
    /// Command to create a new file
    /// </summary>
    public ICommand? NewFileCommand { get; set; }
}
