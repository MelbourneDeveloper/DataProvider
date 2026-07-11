using CommunityToolkit.Mvvm.ComponentModel;

namespace Nimblesite.Lql.Browser.ViewModels;

/// <summary>
/// ViewModel for the query editor component
/// </summary>
public partial class QueryEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _queryText = string.Empty;
}
