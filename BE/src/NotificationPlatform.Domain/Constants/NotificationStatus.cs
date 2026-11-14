namespace NotificationPlatform.Domain.Constants;

public static class NotificationStatus
{
    public const string Queued = "queued";
    public const string Processing = "processing";
    public const string Delivered = "delivered";
    public const string Failed = "failed";

    public static readonly IReadOnlyList<string> All = [Queued, Processing, Delivered, Failed];
}
