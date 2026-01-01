namespace Conduit;

/// <summary>
/// Unit type for handlers that don't return a value (void-equivalent in FP).
/// Singleton pattern ensures only one instance exists.
/// </summary>
public sealed record Unit
{
    /// <summary>
    /// The singleton Unit value.
    /// </summary>
    public static readonly Unit Value = new();

    private Unit() { }
}
