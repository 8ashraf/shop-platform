using Microsoft.Extensions.DependencyInjection;

namespace Shop.Modules.Identity.Application;

public static class DependencyInjection
{
    /// <summary>Use cases, validators and handlers of the Identity module.</summary>
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<MerchantService>();
        return services;
    }
}