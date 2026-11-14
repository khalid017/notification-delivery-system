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
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        try
        {
            await context.GetCollection<Tenant>().Indexes.CreateOneAsync(
                new CreateIndexModel<Tenant>(
                    Builders<Tenant>.IndexKeys.Ascending(t => t.Email),
                    new CreateIndexOptions { Unique = true }),
                cancellationToken: ct);

            await context.GetCollection<Tenant>().Indexes.CreateOneAsync(
                new CreateIndexModel<Tenant>(
                    Builders<Tenant>.IndexKeys.Ascending(t => t.ApiKeyHash),
                    new CreateIndexOptions { Unique = true }),
                cancellationToken: ct);

            await context.GetCollection<Notification>().Indexes.CreateOneAsync(
                new CreateIndexModel<Notification>(
                    Builders<Notification>.IndexKeys
                        .Ascending(n => n.TenantId)
                        .Descending(n => n.CreatedAt)),
                cancellationToken: ct);

            await context.GetCollection<Notification>().Indexes.CreateOneAsync(
                new CreateIndexModel<Notification>(
                    Builders<Notification>.IndexKeys
                        .Ascending(n => n.TenantId)
                        .Ascending(n => n.Status)),
                cancellationToken: ct);

            await context.GetCollection<DeliveryLog>().Indexes.CreateOneAsync(
                new CreateIndexModel<DeliveryLog>(
                    Builders<DeliveryLog>.IndexKeys.Ascending(d => d.NotificationId)),
                cancellationToken: ct);

            logger.LogInformation("MongoDB indexes ensured");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create MongoDB indexes");
        }
    }
}
