namespace NotificationPlatform.Services.Delivery;

public enum DeliveryOutcome
{
    /// <summary>Sent, or nothing to do (unknown id, already finished, or a duplicate message).</summary>
    Delivered,

    /// <summary>This attempt failed and another one is allowed. The notification is back to <c>queued</c>.</summary>
    Retry,

    /// <summary>The last allowed attempt failed. The notification is <c>failed</c>.</summary>
    FailedFinal,
}

/// <summary>What happened to one delivery attempt. <see cref="Error"/> is set for a failed send.</summary>
public record DeliveryResult(DeliveryOutcome Outcome, string? Error = null);
