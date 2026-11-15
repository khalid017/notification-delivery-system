using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NotificationPlatform.Services.Auth;

namespace NotificationPlatform.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers business services. Options are bound by the hosts that need them
    /// (e.g. <see cref="JwtOptions"/> in the Api only).
    /// </summary>
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<TokenService>();
        services.AddSingleton<ApiKeyService>();

        return services;
    }
}
