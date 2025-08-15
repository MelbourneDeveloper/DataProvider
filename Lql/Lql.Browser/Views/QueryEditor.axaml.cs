using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Lql.Browser.Services;
using Lql.Browser.ViewModels;
using TextMateSharp.Registry;

namespace Lql.Browser.Views;

public partial class QueryEditor : UserControl
{
    public QueryEditor()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SetupSyntaxHighlighting();
    }

    private void SetupSyntaxHighlighting()
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null)
            return;

        try
        {
            System.Diagnostics.Debug.WriteLine("=== TEXTMATE INSTALLATION DIAGNOSTIC ===");

            // Create custom registry options that includes our LQL grammar
            var registryOptions = CreateRegistryOptionsWithLqlGrammar();
            System.Diagnostics.Debug.WriteLine($"✓ Registry options created with LQL grammar");

            var registry = new Registry(registryOptions);
            System.Diagnostics.Debug.WriteLine("✓ Registry created with custom options");

            // The custom registry options should handle grammar loading automatically

            // Test if our grammar is available
            try
            {
                var testGrammar = registry.LoadGrammar("source.lql");
                System.Diagnostics.Debug.WriteLine(
                    $"✓ LQL grammar test load: {testGrammar != null}"
                );
                if (testGrammar != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"✓ Grammar scope: {testGrammar.GetScopeName()}"
                    );
                }
            }
            catch (Exception testEx)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Grammar test failed: {testEx.Message}");
            }

            // Install TextMate on the editor
            var textMateInstallation = editor.InstallTextMate(registryOptions);
            System.Diagnostics.Debug.WriteLine(
                $"✓ TextMate installed on editor: {textMateInstallation != null}"
            );

            // Default to LQL syntax highlighting
            System.Diagnostics.Debug.WriteLine("Attempting to set grammar to: source.lql");
            if (textMateInstallation != null)
            {
                try
                {
                    textMateInstallation.SetGrammar("source.lql");
                    System.Diagnostics.Debug.WriteLine(
                        "✓ Grammar set to source.lql - syntax highlighting should now be active"
                    );
                }
                catch (Exception grammarEx)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"✗ Error setting grammar: {grammarEx.Message}"
                    );
                    System.Diagnostics.Debug.WriteLine($"✗ Stack trace: {grammarEx.StackTrace}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("✗ TextMate installation is null");
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash the application
            System.Diagnostics.Debug.WriteLine(
                $"Failed to setup syntax highlighting: {ex.Message}"
            );
        }
    }

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static IRegistryOptions CreateRegistryOptionsWithLqlGrammar() =>
        new LqlRegistryOptions();
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateEditorContent(viewModel.ActiveTab);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel viewModel)
            return;

        if (e.PropertyName == nameof(MainWindowViewModel.ActiveTab))
        {
            UpdateEditorContent(viewModel.ActiveTab);
        }
    }

    private void UpdateEditorContent(Models.FileTab? activeTab)
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null)
            return;

        // Remove event handler temporarily
        editor.TextChanged -= OnEditorTextChanged;

        // Update content
        editor.Text = activeTab?.Content ?? "";

        // Debug content and highlighting
        System.Diagnostics.Debug.WriteLine($"=== EDITOR CONTENT UPDATE ===");
        System.Diagnostics.Debug.WriteLine($"Content length: {editor.Text.Length}");
        System.Diagnostics.Debug.WriteLine(
            $"File extension: {activeTab?.FileName?.Split('.').LastOrDefault()}"
        );

        if (!string.IsNullOrEmpty(editor.Text))
        {
            var preview = editor.Text.Length > 50 ? editor.Text[..50] + "..." : editor.Text;
            System.Diagnostics.Debug.WriteLine($"Content preview: {preview}");
        }

        // Re-add event handler
        editor.TextChanged += OnEditorTextChanged;
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
