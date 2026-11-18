using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NotificationPlatform.Domain.DomainModels;

namespace NotificationPlatform.DataAccess;

/// <summary>
/// Creates the indexes from BE-02 section 4. Safe to run on every startup: creating an index
/// that already exists with the same definition is a no-op. Failures are logged, not thrown,
/// so the API still starts if MongoDB is briefly unreachable.
/// </summary>
public class MongoIndexInitializer(MongoContext context, ILogger<MongoIndexInitializer> logger)
{
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.GetCollection<Tenant>().Indexes.CreateOneAsync(
                new CreateIndexModel<Tenant>(
                    Builders<Tenant>.IndexKeys.Ascending(tenant => tenant.Email),
                    new CreateIndexOptions { Unique = true }),
                cancellationToken: cancellationToken);

            await context.GetCollection<Tenant>().Indexes.CreateOneAsync(
                new CreateIndexModel<Tenant>(
                    Builders<Tenant>.IndexKeys.Ascending(tenant => tenant.ApiKeyHash),
                    new CreateIndexOptions { Unique = true }),
                cancellationToken: cancellationToken);

            await context.GetCollection<Notification>().Indexes.CreateOneAsync(
                new CreateIndexModel<Notification>(
                    Builders<Notification>.IndexKeys
                        .Ascending(notification => notification.TenantId)
                        .Descending(notification => notification.CreatedAt)),
                cancellationToken: cancellationToken);

            await context.GetCollection<Notification>().Indexes.CreateOneAsync(
                new CreateIndexModel<Notification>(
                    Builders<Notification>.IndexKeys
                        .Ascending(notification => notification.TenantId)
                        .Ascending(notification => notification.Status)
                        .Descending(notification => notification.CreatedAt)), // ESR: filtered list sorted by the index
                cancellationToken: cancellationToken);

            await context.GetCollection<DeliveryLog>().Indexes.CreateOneAsync(
                new CreateIndexModel<DeliveryLog>(
                    Builders<DeliveryLog>.IndexKeys.Ascending(deliveryLog => deliveryLog.NotificationId)),
                cancellationToken: cancellationToken);

            logger.LogInformation("MongoDB indexes ensured");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create MongoDB indexes");
        }
    }
}
