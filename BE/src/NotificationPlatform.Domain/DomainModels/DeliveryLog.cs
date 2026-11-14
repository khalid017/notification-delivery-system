namespace NotificationPlatform.Domain.DomainModels;

/// <summary>One document per delivery attempt.</summary>
public class DeliveryLog
{
    public string Id { get; set; } = string.Empty;
    public string NotificationId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public int Attempt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime AttemptedAt { get; set; }
}
