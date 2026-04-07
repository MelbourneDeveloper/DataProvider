// Shared across all *.Tests projects via Compile Include from /DataProvider/Tests.Shared/.
// Each test assembly gets its OWN [CollectionDefinition] (because the attribute is compiled
// into each assembly), so each assembly's tests share exactly one container.

namespace Nimblesite.TestSupport;

/// <summary>
/// xUnit collection definition that wires <see cref="PostgresContainerFixture"/> as a
/// per-assembly shared fixture. Test classes opt in by adding
/// <c>[Collection(PostgresTestSuite.Name)]</c>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresTestSuite : ICollectionFixture<PostgresContainerFixture>
{
    /// <summary>
    /// Name used by <c>[Collection(...)]</c> attributes on test classes.
    /// </summary>
    public const string Name = "Postgres";
}
