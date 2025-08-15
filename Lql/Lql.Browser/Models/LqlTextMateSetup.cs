using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace Lql.Browser.Models;

/// <summary>
/// Static methods for setting up LQL TextMate integration
/// </summary>
public static class LqlTextMateSetup
{
    /// <summary>
    /// Sets up TextMate with dark theme for LQL syntax highlighting
    /// </summary>
    public static TextMate.Installation SetupLqlTextMate(TextEditor editor)
    {
        // Use DarkPlus theme which has good colors for syntax highlighting
        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);

        // Install TextMate
        var installation = editor.InstallTextMate(registryOptions);

        return installation;
    }
}
