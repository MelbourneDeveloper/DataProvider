using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace Lql.Browser.Views;

/// <summary>
/// Transpiled SQL panel view for displaying the generated SQL from LQL queries
/// </summary>
public partial class TranspiledSqlPanel : UserControl
{
    private TextMate.Installation? _textMateInstallation;

    public TranspiledSqlPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        SetupSqlSyntaxHighlighting();

    private void SetupSqlSyntaxHighlighting()
    {
        var editor = this.FindControl<TextEditor>("SqlEditor");
        if (editor == null)
            return;

        try
        {
            // Use DarkPlus theme for syntax highlighting
            var registryOptions = new RegistryOptions(ThemeName.DarkPlus);

            // Install TextMate on the editor
            _textMateInstallation = editor.InstallTextMate(registryOptions);

            // Set SQL grammar
            var language = registryOptions.GetLanguageByExtension(".sql");
            if (language != null && _textMateInstallation != null)
            {
                _textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(language.Id));
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash the application
            System.Diagnostics.Debug.WriteLine(
                $"Failed to setup SQL syntax highlighting: {ex.Message}"
            );
        }
    }
}
