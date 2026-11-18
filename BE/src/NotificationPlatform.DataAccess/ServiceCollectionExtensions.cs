using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NotificationPlatform.Services.Abstractions;

namespace NotificationPlatform.DataAccess;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the MongoDB context, index initializer and the data access classes. The data access
    /// classes are singletons: they hold no state and Mongo collections are thread-safe.
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoOptions>()
            .Bind(configuration.GetSection(MongoOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<MongoContext>();
        services.AddSingleton<MongoIndexInitializer>();

        services.TryAddSingleton(TimeProvider.System); // for updatedAt; also registered by AddServices()

        services.AddSingleton<ITenantDataAccess, TenantDataAccess>();
        services.AddSingleton<INotificationDataAccess, NotificationDataAccess>();
        services.AddSingleton<IDeliveryLogDataAccess, DeliveryLogDataAccess>();

        return services;
    }
}
