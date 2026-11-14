using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NotificationPlatform.DataAccess;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the MongoDB context, index initializer and (from B2) the data access classes.</summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoOptions>()
            .Bind(configuration.GetSection(MongoOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<MongoContext>();
        services.AddSingleton<MongoIndexInitializer>();

        return services;
    }
}
