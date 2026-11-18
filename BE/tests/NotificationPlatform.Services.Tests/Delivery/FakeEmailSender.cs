using NotificationPlatform.Services.Abstractions;

namespace NotificationPlatform.Services.Tests.Delivery;

/// <summary>Fails the first <see cref="FailuresRemaining"/> sends, then succeeds. Records successful sends.</summary>
internal sealed class FakeEmailSender : IEmailSender
{
    public const string FailureMessage = "Email address is not verified.";

    public int FailuresRemaining { get; set; }

    public List<string> SentTo { get; } = [];

    /// <summary>Runs after a successful send, e.g. to cancel the caller's token.</summary>
    public Action? AfterSend { get; set; }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (FailuresRemaining > 0)
        {
            FailuresRemaining--;
            throw new InvalidOperationException(FailureMessage);
        }

        SentTo.Add(to);
        AfterSend?.Invoke();
        return Task.CompletedTask;
    }
}
