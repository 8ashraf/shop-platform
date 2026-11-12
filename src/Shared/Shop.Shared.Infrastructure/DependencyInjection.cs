using Shop.Shared.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Shop.Shared.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // مين اللي بيتكلم؟ (بتقرا الـ claims من الطلب الحالي)
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentPrincipal, CurrentPrincipal>();

        return services;
    }
}