namespace Nimblesite.Lql.Core.FunctionMapping;

/// <summary>
/// Represents a function mapping from LQL to SQL dialect
/// </summary>
public record FunctionMap(
    string LqlFunction,
    string SqlFunction,
    bool RequiresSpecialHandling = false,
    Func<string[], string>? SpecialHandler = null
);
