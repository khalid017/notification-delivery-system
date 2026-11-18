using MongoDB.Driver;
using NotificationPlatform.Domain.Constants;
using NotificationPlatform.Domain.DomainModels;
using NotificationPlatform.Services.Abstractions;

namespace NotificationPlatform.DataAccess;

internal sealed class NotificationDataAccess(MongoContext context, TimeProvider time)
    : MongoDataAccessBase<Notification>(context), INotificationDataAccess
{
    public async Task InsertAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        notification.Id = NewId();
        await Collection.InsertOneAsync(notification, cancellationToken: cancellationToken);
    }

    public async Task<Notification?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default) =>
        await Collection
            .Find(notification => notification.Id == id && notification.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Notification?> TryClaimAsync(string id, int attempt, CancellationToken cancellationToken = default)
    {
        // One atomic find-and-modify: of two copies of the same message, only one can claim a given attempt.
        // A crashed attempt leaves status "processing", which the next attempt number can still claim.
        var filter = Builders<Notification>.Filter.Where(notification =>
            notification.Id == id &&
            (notification.Status == NotificationStatus.Queued || notification.Status == NotificationStatus.Processing) &&
            notification.Attempts < attempt);

        var update = Builders<Notification>.Update
            .Set(notification => notification.Status, NotificationStatus.Processing)
            .Set(notification => notification.Attempts, attempt)
            .Set(notification => notification.UpdatedAt, time.GetUtcNow().UtcDateTime);

        return await Collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Notification> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task UpdateStatusAsync(
        string id, string status, string? lastError, DateTime? deliveredAt, CancellationToken cancellationToken = default)
    {
        var update = Builders<Notification>.Update
            .Set(notification => notification.Status, status)
            .Set(notification => notification.LastError, lastError)
            .Set(notification => notification.DeliveredAt, deliveredAt)
            .Set(notification => notification.UpdatedAt, time.GetUtcNow().UtcDateTime);

        await Collection.UpdateOneAsync(notification => notification.Id == id, update, cancellationToken: cancellationToken);
    }

    public async Task<(IReadOnlyList<Notification> Items, long Total)> ListAsync(
        string tenantId, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Notification>.Filter.Eq(notification => notification.TenantId, tenantId);
        if (status is not null)
        {
            filter &= Builders<Notification>.Filter.Eq(notification => notification.Status, status);
        }

        var items = await Collection.Find(filter)
            .SortByDescending(notification => notification.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var total = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        return (items, total);
    }

    public async Task<long> CountByStatusAsync(string tenantId, string status, CancellationToken cancellationToken = default) =>
        await Collection.CountDocumentsAsync(
            notification => notification.TenantId == tenantId && notification.Status == status,
            cancellationToken: cancellationToken);
}
