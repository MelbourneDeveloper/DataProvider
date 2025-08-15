using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lql.Browser.Models;

public class QueryResultRow : INotifyPropertyChanged
{
    private readonly Dictionary<string, object?> _data = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public object? this[string columnName]
    {
        get => _data.TryGetValue(columnName, out var value) ? value : null;
        set
        {
            _data[columnName] = value;
            OnPropertyChanged(columnName);
        }
    }

    public void SetValue(string columnName, object? value)
    {
        _data[columnName] = value;
        OnPropertyChanged(columnName);
    }

#pragma warning disable CA1024 // Use properties where appropriate
    public Dictionary<string, object?> GetAllValues() => new(_data);
#pragma warning restore CA1024 // Use properties where appropriate

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
