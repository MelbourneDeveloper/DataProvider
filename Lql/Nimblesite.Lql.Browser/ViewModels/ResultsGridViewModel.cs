using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Nimblesite.Lql.Browser.Models;

namespace Nimblesite.Lql.Browser.ViewModels;

/// <summary>
/// ViewModel for the results grid component
/// </summary>
public partial class ResultsGridViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _resultsHeader = "Results";

    [ObservableProperty]
    private string _executionTime = string.Empty;

    [ObservableProperty]
    private string _rowCount = string.Empty;

    [ObservableProperty]
    private ObservableCollection<QueryResultRow>? _queryResults;
}
