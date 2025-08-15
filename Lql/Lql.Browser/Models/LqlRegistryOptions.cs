using TextMateSharp.Grammars;

namespace Lql.Browser.Models;

/// <summary>
/// Helper class for creating registry options with LQL support
/// </summary>
public static class LqlRegistryOptions
{
    /// <summary>
    /// Creates registry options with dark theme
    /// </summary>
    public static RegistryOptions Create() => new(ThemeName.DarkPlus);

    /// <summary>
    /// Gets the scope name for LQL language
    /// </summary>
#pragma warning disable CA1024 // Use properties where appropriate
    public static string GetLqlScope() => "source.lql";
#pragma warning restore CA1024 // Use properties where appropriate
}
