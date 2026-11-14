namespace NotificationPlatform.Services.Abstractions;

public interface INotificationQueue
{
    /// <summary>Queues a notification id for delivery. Throws on failure.</summary>
    Task EnqueueAsync(string notificationId, CancellationToken ct = default);
}
