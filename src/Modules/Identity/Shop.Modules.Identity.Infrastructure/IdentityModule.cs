using Shop.Modules.Identity.Application;
using Shop.Modules.Identity.Infrastructure.Database;
using Shop.Modules.Identity.Presentation;
using Shop.Shared.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shop.Modules.Identity.Infrastructure.Services;

namespace Shop.Modules.Identity.Infrastructure;

public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

    services.AddDbContext<IdentityDbContext>(options =>
        options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema)));

    // تنفيذ الواجهات اللي الـ Application محتاجها
    services.AddScoped<IIdentityStore, IdentityStore>();
    services.AddScoped<IOtpSender, LogOtpSender>();
    services.AddScoped<ITokenIssuer, JwtTokenIssuer>();

    services.AddIdentityApplication();
    services.AddIntegrationEventHandlers(Application.AssemblyReference.Assembly);
}

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapIdentityEndpoints();
}
