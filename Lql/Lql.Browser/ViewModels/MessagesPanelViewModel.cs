using System.Collections.ObjectModel;
using System.Globalization;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lql.Browser.ViewModels;

/// <summary>
/// ViewModel for the messages panel that displays SQL output, errors, and execution information
/// </summary>
public partial class MessagesPanelViewModel : ViewModelBase
{
    /// <summary>
    /// Collection of messages to display in the panel
    /// </summary>
    public ObservableCollection<MessageItem> Messages { get; } = [];

    /// <summary>
    /// The transpiled SQL query from LQL conversion
    /// </summary>
    [ObservableProperty]
    private string? _transpiledSql;

    /// <summary>
    /// Text document for the AvaloniaEdit editor
    /// </summary>
    [ObservableProperty]
    private TextDocument _transpiledSqlDocument = new();

    /// <summary>
    /// Adds an error message to the messages collection
    /// </summary>
    /// <param name="message">The error message to add</param>
    public void AddError(string message) =>
        Messages.Add(new MessageItem(MessageType.Error, message, DateTime.Now));

    /// <summary>
    /// Adds an information message to the messages collection
    /// </summary>
    /// <param name="message">The information message to add</param>
    public void AddInfo(string message) =>
        Messages.Add(new MessageItem(MessageType.Info, message, DateTime.Now));

    /// <summary>
    /// Adds a warning message to the messages collection
    /// </summary>
    /// <param name="message">The warning message to add</param>
    public void AddWarning(string message) =>
        Messages.Add(new MessageItem(MessageType.Warning, message, DateTime.Now));

    /// <summary>
    /// Adds a success message to the messages collection
    /// </summary>
    /// <param name="message">The success message to add</param>
    public void AddSuccess(string message) =>
        Messages.Add(new MessageItem(MessageType.Success, message, DateTime.Now));

    /// <summary>
    /// Sets the transpiled SQL and adds an info message about the conversion
    /// </summary>
    /// <param name="sql">The transpiled SQL query</param>
    public void SetTranspiledSql(string sql)
    {
        TranspiledSql = sql;
        TranspiledSqlDocument.Text = sql ?? string.Empty;
        AddInfo($"LQL transpiled to SQL successfully");
    }

    /// <summary>
    /// Clears all messages from the panel
    /// </summary>
    public void ClearMessages() => Messages.Clear();

    /// <summary>
    /// Clears the transpiled SQL
    /// </summary>
    public void ClearTranspiledSql()
    {
        TranspiledSql = null;
        TranspiledSqlDocument.Text = string.Empty;
    }

    /// <summary>
    /// Clears both messages and transpiled SQL
    /// </summary>
    public void ClearAll()
    {
        ClearMessages();
        ClearTranspiledSql();
    }
}

/// <summary>
/// Represents a single message item in the messages panel
/// </summary>
/// <param name="Type">The type/severity of the message</param>
/// <param name="Message">The message text</param>
/// <param name="Timestamp">When the message was created</param>
public record MessageItem(MessageType Type, string Message, DateTime Timestamp)
{
    /// <summary>
    /// Formatted timestamp for display
    /// </summary>
    public string FormattedTimestamp =>
        Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the icon character for the message type
    /// </summary>
    public string Icon =>
        Type switch
        {
            MessageType.Error => "❌",
            MessageType.Warning => "⚠️",
            MessageType.Success => "✅",
            MessageType.Info => "ℹ️",
            _ => "•",
        };

    /// <summary>
    /// Gets the CSS class for styling the message type
    /// </summary>
    public string TypeClass => Type.ToString().ToUpperInvariant();
}

/// <summary>
/// Types of messages that can be displayed
/// </summary>
public enum MessageType
{
    Info,
    Success,
    Warning,
    Error,
}
