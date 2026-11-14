namespace NotificationPlatform.Services.Abstractions;

public interface IEmailSender
{
    /// <summary>Sends a plain-text email. Throws on failure.</summary>
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
