using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Lql.Browser.Models;
using Lql.Browser.ViewModels;
using TextMateSharp.Grammars;

namespace Lql.Browser.Views;

public partial class MainWindow : Window
{
    private TextMate.Installation? _textMateInstallation;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Initialize the editor when the window is loaded
        if (DataContext is MainWindowViewModel viewModel && viewModel.ActiveTab != null)
        {
            UpdateSyntaxHighlighting(viewModel.ActiveTab);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Initialize the editor if there's already an active tab
            if (viewModel.ActiveTab != null)
            {
                UpdateSyntaxHighlighting(viewModel.ActiveTab);
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel viewModel)
            return;

        if (e.PropertyName == nameof(MainWindowViewModel.ActiveTab))
        {
            UpdateSyntaxHighlighting(viewModel.ActiveTab);
        }
    }

    private void UpdateSyntaxHighlighting(FileTab? activeTab)
    {
        var editor = this.FindControl<TextEditor>("QueryEditor");
        if (editor == null || activeTab == null)
        {
            return;
        }

        // Initialize TextMate if not already done
        if (_textMateInstallation == null)
        {
            _textMateInstallation = LqlTextMateSetup.SetupLqlTextMate(editor);
        }

        // Temporarily remove text change handler to prevent infinite loops
        editor.TextChanged -= OnEditorTextChanged;

        // Update the text content
        editor.Text = activeTab.Content ?? "";

        // Re-add text change handling
        editor.TextChanged += OnEditorTextChanged;

        try
        {
            // Set grammar based on file type - use SQL grammar for both LQL and SQL for now
            var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            var language = registryOptions.GetLanguageByExtension(".sql");
            if (language != null)
            {
                _textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(language.Id));
            }
        }
        catch (Exception ex)
        {
            // Fallback to no syntax highlighting if there's an error
            Console.WriteLine($"Error setting syntax highlighting: {ex.Message}");
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (
            sender is TextEditor editor
            && DataContext is MainWindowViewModel viewModel
            && viewModel.ActiveTab != null
        )
        {
            viewModel.ActiveTab.Content = editor.Text;
            viewModel.ActiveTab.IsModified = true;
        }
    }
}
