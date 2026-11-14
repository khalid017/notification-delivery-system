using NotificationPlatform.Domain.DomainModels;

namespace NotificationPlatform.Services.Abstractions;

public interface IDeliveryLogDataAccess
{
    /// <summary>Inserts the log and sets its <see cref="DeliveryLog.Id"/>.</summary>
    Task InsertAsync(DeliveryLog log, CancellationToken ct = default);

    /// <summary>All attempts for a notification, oldest first.</summary>
    Task<IReadOnlyList<DeliveryLog>> GetByNotificationAsync(string notificationId, CancellationToken ct = default);
}
