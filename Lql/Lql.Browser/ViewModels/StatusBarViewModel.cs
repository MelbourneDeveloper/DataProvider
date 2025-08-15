using CommunityToolkit.Mvvm.ComponentModel;

namespace Lql.Browser.ViewModels;

/// <summary>
/// Connection status enumeration
/// </summary>
public enum ConnectionStatus
{
    Disconnected,
    Connected,
    Error,
}

/// <summary>
/// ViewModel for the status bar component
/// </summary>
public partial class StatusBarViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _connectionStatusText = "Disconnected";

    [ObservableProperty]
    private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;

    [ObservableProperty]
    private string _databasePath = "No database selected";

    [ObservableProperty]
    private string _statusMessage = string.Empty;
}
