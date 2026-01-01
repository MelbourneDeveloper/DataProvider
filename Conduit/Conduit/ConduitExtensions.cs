using Microsoft.Extensions.DependencyInjection;

namespace Conduit;

/// <summary>
/// Extension methods for configuring Conduit with dependency injection.
/// </summary>
public static class ConduitExtensions
{
    /// <summary>
    /// Adds Conduit pipeline services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration function that builds the registry.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConduit(
        this IServiceCollection services,
        Func<ConduitRegistry, ConduitRegistry> configure
    )
    {
        var registry = configure(ConduitRegistry.Empty);
        services.AddSingleton(registry);
        return services;
    }
}
