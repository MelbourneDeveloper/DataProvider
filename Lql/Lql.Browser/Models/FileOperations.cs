using System.Collections.ObjectModel;
using Outcome;

namespace Lql.Browser.Models;

/// <summary>
/// File operation result types and static methods
/// </summary>
public static class FileOperations
{
    /// <summary>
    /// Creates a new file tab
    /// </summary>
    public static FileTab CreateNewTab(string fileType, int tabNumber) =>
        fileType.Equals("lql", StringComparison.OrdinalIgnoreCase)
            ? new FileTab
            {
                FileName = $"Untitled{tabNumber}.lql",
                FileType = FileType.Lql,
                Content = "Customer |> select(*) |> limit(50)",
            }
            : new FileTab
            {
                FileName = $"Untitled{tabNumber}.sql",
                FileType = FileType.Sql,
                Content = "SELECT * FROM Customer LIMIT 50;",
            };

    /// <summary>
    /// Reads file content
    /// </summary>
    public static async Task<Result<string, string>> ReadFileAsync(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            return new Result<string, string>.Ok<string, string>(content);
        }
        catch (Exception ex)
        {
            return new Result<string, string>.Error<string, string>(
                $"Error reading file: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Writes file content
    /// </summary>
    public static async Task<Result<Unit, string>> WriteFileAsync(string filePath, string content)
    {
        try
        {
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
            await File.WriteAllTextAsync(filePath, content);
#pragma warning restore RS1035 // Do not use APIs banned for analyzers
            return new Result<Unit, string>.Ok<Unit, string>(Unit.Value);
        }
        catch (Exception ex)
        {
            return new Result<Unit, string>.Error<Unit, string>(
                $"Error writing file: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Creates file tab from file path
    /// </summary>
    public static FileTab CreateTabFromFile(string filePath, string content)
    {
        var fileName = Path.GetFileName(filePath);
        var isLql = Path.GetExtension(filePath).Equals(".lql", StringComparison.OrdinalIgnoreCase);

        return new FileTab
        {
            FileName = fileName,
            FilePath = filePath,
            Content = content,
            FileType = isLql ? FileType.Lql : FileType.Sql,
        };
    }

    /// <summary>
    /// Updates file type based on extension
    /// </summary>
    public static FileTab UpdateFileType(FileTab tab, string filePath)
    {
        var isLql = Path.GetExtension(filePath).Equals(".lql", StringComparison.OrdinalIgnoreCase);
        tab.FileType = isLql ? FileType.Lql : FileType.Sql;
        tab.FileName = Path.GetFileName(filePath);
        tab.FilePath = filePath;
        return tab;
    }

    /// <summary>
    /// Switches active tab
    /// </summary>
    public static void SwitchActiveTab(ObservableCollection<FileTab> tabs, FileTab targetTab)
    {
        foreach (var tab in tabs)
        {
            tab.IsActive = tab == targetTab;
        }
    }

    /// <summary>
    /// Removes tab and returns new active tab
    /// </summary>
    public static FileTab? RemoveTab(ObservableCollection<FileTab> tabs, FileTab tabToRemove)
    {
        tabs.Remove(tabToRemove);
        return tabs.FirstOrDefault();
    }
}
