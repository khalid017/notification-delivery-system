using NotificationPlatform.Domain.DomainModels;

namespace NotificationPlatform.Services.Abstractions;

public interface INotificationDataAccess
{
    /// <summary>Inserts the notification and sets its <see cref="Notification.Id"/>.</summary>
    Task InsertAsync(Notification notification, CancellationToken ct = default);

    /// <summary>Tenant-scoped lookup. Returns null for an unknown id or another tenant's id.</summary>
    Task<Notification?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default);

    /// <summary>Lookup by id only, for the delivery worker.</summary>
    Task<Notification?> GetByIdForProcessingAsync(string id, CancellationToken ct = default);

    /// <summary>Sets status <c>processing</c>, <c>attempts</c> and <c>updatedAt</c>.</summary>
    Task MarkProcessingAsync(string id, int attempt, CancellationToken ct = default);

    /// <summary>Sets <c>status</c>, <c>lastError</c> (null clears it), <c>deliveredAt</c> and <c>updatedAt</c>.</summary>
    Task UpdateStatusAsync(string id, string status, string? lastError, DateTime? deliveredAt, CancellationToken ct = default);

    /// <summary>One page of the tenant's notifications, newest first, plus the total matching count.</summary>
    Task<(IReadOnlyList<Notification> Items, long Total)> ListAsync(
        string tenantId, string? status, int page, int pageSize, CancellationToken ct = default);

    Task<long> CountByStatusAsync(string tenantId, string status, CancellationToken ct = default);
}
