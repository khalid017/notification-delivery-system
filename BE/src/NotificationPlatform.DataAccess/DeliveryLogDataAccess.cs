using MongoDB.Driver;
using NotificationPlatform.Domain.DomainModels;
using NotificationPlatform.Services.Abstractions;

namespace NotificationPlatform.DataAccess;

internal sealed class DeliveryLogDataAccess(MongoContext context)
    : MongoDataAccessBase<DeliveryLog>(context), IDeliveryLogDataAccess
{
    public async Task InsertAsync(DeliveryLog log, CancellationToken cancellationToken = default)
    {
        log.Id = NewId();
        await Collection.InsertOneAsync(log, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<DeliveryLog>> GetByNotificationAsync(
        string notificationId, CancellationToken cancellationToken = default) =>
        await Collection.Find(log => log.NotificationId == notificationId)
            .SortBy(log => log.Attempt)
            .ToListAsync(cancellationToken);
}
