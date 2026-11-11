using Microsoft.Extensions.DependencyInjection;

namespace Shop.Modules.Catalog.Application;

public static class DependencyInjection
{
    /// <summary>Use cases, validators and handlers of the Catalog module.</summary>
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        return services;
    }
}
