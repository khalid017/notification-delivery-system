using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NotificationPlatform.DataAccess;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers MongoDB implementations of the Services abstractions. Filled in from B1.2.</summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
