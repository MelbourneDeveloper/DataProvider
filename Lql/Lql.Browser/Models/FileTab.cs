using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lql.Browser.Models;

/// <summary>
/// Represents a file tab in the editor
/// </summary>
public class FileTab : INotifyPropertyChanged
{
    private string _fileName = "Untitled";
    private string _filePath = "";
    private string _content = "";
    private bool _isModified;
    private bool _isActive;
    private FileType _fileType = FileType.Lql;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Display name of the file
    /// </summary>
    public string FileName
    {
        get => _fileName;
        set
        {
            _fileName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Full path to the file
    /// </summary>
    public string FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Content of the file
    /// </summary>
    public string Content
    {
        get => _content;
        set
        {
            _content = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Whether the file has unsaved changes
    /// </summary>
    public bool IsModified
    {
        get => _isModified;
        set
        {
            _isModified = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Whether this tab is currently active
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Type of file (LQL or SQL)
    /// </summary>
    public FileType FileType
    {
        get => _fileType;
        set
        {
            _fileType = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Display name with modification indicator
    /// </summary>
    public string DisplayName => $"{FileName}{(IsModified ? "*" : "")}";

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// File type enumeration
/// </summary>
public enum FileType
{
    Lql,
    Sql,
}
