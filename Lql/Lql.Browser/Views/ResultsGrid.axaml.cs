using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Lql.Browser.ViewModels;

namespace Lql.Browser.Views;

public partial class ResultsGrid : UserControl
{
    public ResultsGrid()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ResultsGridViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ResultsGridViewModel viewModel)
            return;

        if (e.PropertyName == nameof(ResultsGridViewModel.QueryResults))
        {
            UpdateDataGridColumns(viewModel);
        }
    }

    private void UpdateDataGridColumns(ResultsGridViewModel viewModel)
    {
        var dataGrid = this.FindControl<DataGrid>("QueryResultsDataGrid");
        if (dataGrid == null || viewModel.QueryResults == null || viewModel.QueryResults.Count == 0)
            return;

        dataGrid.Columns.Clear();

        // Get column names from the first result row
        var firstRow = viewModel.QueryResults.FirstOrDefault();
        if (firstRow != null)
        {
            var allValues = firstRow.GetAllValues();
            foreach (var columnName in allValues.Keys)
            {
                var column = new DataGridTextColumn
                {
                    Header = columnName,
                    Binding = new Binding($"[{columnName}]"),
                    Width = new DataGridLength(100, DataGridLengthUnitType.Pixel),
                };
                dataGrid.Columns.Add(column);
            }
        }
    }
}
