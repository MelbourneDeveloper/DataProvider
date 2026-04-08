using Nimblesite.Sql.Model;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

// Implements [CON-SHARED-CORE]. Shared across SQLite and Postgres (and any
// future platform) so neither dialect duplicates the dummy-value resolution
// used when probing schemas with a throwaway SELECT.
/// <summary>
/// Resolves placeholder values for SQL parameters when probing a query's
/// column metadata. The dialect-specific <c>IDatabaseEffects</c>
/// implementations call into this to bind parameters before running a
/// schema-discovery query.
/// </summary>
public static class DummyParameterValues
{
    /// <summary>
    /// Gets a best-effort placeholder value for a parameter based on its name.
    /// </summary>
    /// <param name="parameter">The parameter to resolve a dummy for.</param>
    /// <returns>A value appropriate for <see cref="ParameterInfo.Name"/>.</returns>
    public static object GetDummyValueForParameter(ParameterInfo parameter)
    {
        var lowerName = parameter.Name.ToLowerInvariant();

        return lowerName switch
        {
            var name when name.Contains("id", StringComparison.Ordinal) => 1,
            var name when name.Contains("limit", StringComparison.Ordinal) => 100,
            var name when name.Contains("offset", StringComparison.Ordinal) => 0,
            var name when name.Contains("count", StringComparison.Ordinal) => 1,
            var name when name.Contains("quantity", StringComparison.Ordinal) => 1,
            var name when name.Contains("amount", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("price", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("total", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("percentage", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("date", StringComparison.Ordinal) => DateTime.UtcNow,
            var name when name.Contains("time", StringComparison.Ordinal) => DateTime.UtcNow,
            var name when name.Contains("created", StringComparison.Ordinal) => DateTime.UtcNow,
            var name when name.Contains("updated", StringComparison.Ordinal) => DateTime.UtcNow,
            _ => "dummy_value",
        };
    }
}
