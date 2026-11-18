using NotificationPlatform.Domain.Constants;
using NotificationPlatform.Domain.DomainModels;
using NotificationPlatform.Services.Abstractions;

namespace NotificationPlatform.Services.Delivery;

/// <summary>
/// Delivers one notification for one attempt (BE-02 section 6.4). Returns a result instead of throwing,
/// so each host decides what to do: the worker Lambda throws on <c>Retry</c>/<c>FailedFinal</c> so SQS
/// retries, the local worker loops.
/// </summary>
public class DeliveryProcessor(
    INotificationDataAccess notifications,
    IDeliveryLogDataAccess deliveryLogs,
    IEmailSender emailSender,
    TimeProvider time)
{
    /// <summary>Attempts per notification. Must match the SQS queue's maximum receives (BE-02 section 1.4).</summary>
    public const int MaxAttempts = 3;

    public async Task<DeliveryResult> ProcessAsync(
        string notificationId, int attempt, int maxAttempts, CancellationToken cancellationToken = default)
    {
        var notification = await notifications.TryClaimAsync(notificationId, attempt, cancellationToken);
        if (notification is null)
        {
            // Unknown id, already delivered or failed, or a duplicate message for an attempt already claimed.
            return new DeliveryResult(DeliveryOutcome.Delivered);
        }

        try
        {
            await emailSender.SendAsync(notification.To, notification.Subject, notification.Body, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A cancellation (host shutting down) is not a failed attempt: it propagates and the message is redelivered.
            return await RecordFailureAsync(notification, attempt, maxAttempts, exception.Message);
        }

        // The email is sent: record it even if cancellation is requested now (CancellationToken.None).
        await deliveryLogs.InsertAsync(NewLog(notification, attempt, DeliveryStatus.Success, errorMessage: null), CancellationToken.None);
        await notifications.UpdateStatusAsync(
            notification.Id, NotificationStatus.Delivered, lastError: null, deliveredAt: time.GetUtcNow().UtcDateTime, CancellationToken.None);

        return new DeliveryResult(DeliveryOutcome.Delivered);
    }

    private async Task<DeliveryResult> RecordFailureAsync(
        Notification notification, int attempt, int maxAttempts, string error)
    {
        var isFinal = attempt >= maxAttempts;

        await deliveryLogs.InsertAsync(NewLog(notification, attempt, DeliveryStatus.Failed, error), CancellationToken.None);
        await notifications.UpdateStatusAsync(
            notification.Id,
            isFinal ? NotificationStatus.Failed : NotificationStatus.Queued,
            lastError: error,
            deliveredAt: null,
            CancellationToken.None);

        return new DeliveryResult(isFinal ? DeliveryOutcome.FailedFinal : DeliveryOutcome.Retry, error);
    }

    private DeliveryLog NewLog(Notification notification, int attempt, string status, string? errorMessage) => new()
    {
        NotificationId = notification.Id,
        TenantId = notification.TenantId,
        Attempt = attempt,
        Status = status,
        ErrorMessage = errorMessage,
        AttemptedAt = time.GetUtcNow().UtcDateTime,
    };
}
