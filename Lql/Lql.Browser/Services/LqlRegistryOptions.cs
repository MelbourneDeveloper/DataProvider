using System.Reflection;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace Lql.Browser.Services;

/// <summary>
/// Custom registry options that provides LQL grammar support to TextMateSharp
/// </summary>
public sealed class LqlRegistryOptions : IRegistryOptions
{
    private readonly RegistryOptions _defaultOptions = new(ThemeName.DarkPlus);
    private IRawGrammar? _lqlGrammar;

    public IRawGrammar GetGrammar(string scopeName)
    {
        System.Diagnostics.Debug.WriteLine($"GetGrammar called for scope: {scopeName}");

        if (scopeName == "source.lql")
        {
            _lqlGrammar ??= LoadLqlGrammar();

            if (_lqlGrammar != null)
            {
                System.Diagnostics.Debug.WriteLine("✓ Returning custom LQL grammar");
                return _lqlGrammar;
            }
        }

        // Fall back to default grammars
        return _defaultOptions.GetGrammar(scopeName);
    }

    private IRawGrammar? LoadLqlGrammar()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Lql.Browser.TextMate.lql.tmLanguage.json";

            System.Diagnostics.Debug.WriteLine("=== LOADING LQL GRAMMAR ===");
            System.Diagnostics.Debug.WriteLine($"Assembly: {assembly.FullName}");
            System.Diagnostics.Debug.WriteLine($"Looking for resource: {resourceName}");

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"✓ Resource found, length: {stream.Length} bytes"
                );

                using var reader = new StreamReader(stream);
                var grammar = GrammarReader.ReadGrammarSync(reader);

                if (grammar != null)
                {
                    System.Diagnostics.Debug.WriteLine("✓ LQL grammar loaded successfully");
                    return grammar;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("✗ GrammarReader returned null");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("✗ Resource stream is null");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"✗ Error loading LQL grammar: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"✗ Stack trace: {ex.StackTrace}");
        }

        return null;
    }

    public ICollection<string> GetInjections(string scopeName) =>
        _defaultOptions.GetInjections(scopeName);

    public IRawTheme GetTheme(string scopeName) => _defaultOptions.GetTheme(scopeName);

    public IRawTheme GetDefaultTheme() => _defaultOptions.GetDefaultTheme();
}
