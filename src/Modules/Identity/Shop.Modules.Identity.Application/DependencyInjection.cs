using Microsoft.Extensions.DependencyInjection;

namespace Shop.Modules.Identity.Application;

public static class DependencyInjection
{
    /// <summary>Use cases, validators and handlers of the Identity module.</summary>
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        // TODO: نسجّل AuthService في برانش feature/identity-infrastructure،
        // بعد ما نكتب تنفيذ IIdentityStore و IOtpSender و ITokenIssuer.
        return services;
    }
}