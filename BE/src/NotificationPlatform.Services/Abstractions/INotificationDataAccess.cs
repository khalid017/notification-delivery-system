using NotificationPlatform.Domain.DomainModels;

namespace NotificationPlatform.Services.Abstractions;

public interface INotificationDataAccess
{
    /// <summary>Inserts the notification and sets its <see cref="Notification.Id"/>.</summary>
    Task InsertAsync(Notification notification, CancellationToken cancellationToken = default);

    /// <summary>Tenant-scoped lookup. Returns null for an unknown id or another tenant's id.</summary>
    Task<Notification?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims the notification for one delivery attempt, atomically. Matches only when the status is
    /// <c>queued</c> or <c>processing</c> and <c>attempts</c> is lower than <paramref name="attempt"/>;
    /// then sets status <c>processing</c>, <c>attempts = attempt</c> and <c>updatedAt</c>.
    /// Returns the updated notification, or null when there is nothing to do (unknown id, already
    /// delivered or failed, or this attempt was already claimed by a duplicate message).
    /// </summary>
    Task<Notification?> TryClaimAsync(string id, int attempt, CancellationToken cancellationToken = default);

    /// <summary>Sets <c>status</c>, <c>lastError</c> (null clears it), <c>deliveredAt</c> and <c>updatedAt</c>.</summary>
    Task UpdateStatusAsync(string id, string status, string? lastError, DateTime? deliveredAt, CancellationToken cancellationToken = default);

    /// <summary>One page of the tenant's notifications, newest first, plus the total matching count.</summary>
    Task<(IReadOnlyList<Notification> Items, long Total)> ListAsync(
        string tenantId, string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<long> CountByStatusAsync(string tenantId, string status, CancellationToken cancellationToken = default);
}
