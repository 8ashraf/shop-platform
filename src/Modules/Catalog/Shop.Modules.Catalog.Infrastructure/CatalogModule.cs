using Shop.Modules.Catalog.Application;
using Shop.Modules.Catalog.Infrastructure.Database;
using Shop.Modules.Catalog.Presentation;
using Shop.Shared.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace Shop.Modules.Catalog.Infrastructure;

public sealed class CatalogModule : IModule
{
    public string Name => "Catalog";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", CatalogDbContext.Schema)));
        
        services.AddScoped<ICatalogStore, CatalogStore>();
        services.AddCatalogApplication();
        services.AddIntegrationEventHandlers(Application.AssemblyReference.Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapCatalogEndpoints();
}
