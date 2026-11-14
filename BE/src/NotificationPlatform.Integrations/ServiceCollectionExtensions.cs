using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NotificationPlatform.Integrations;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers AWS adapters (SES, SQS). Filled in from B3.2.</summary>
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
