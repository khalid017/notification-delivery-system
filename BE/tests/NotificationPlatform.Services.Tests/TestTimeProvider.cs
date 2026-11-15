namespace NotificationPlatform.Services.Tests;

/// <summary>A clock fixed at a chosen moment.</summary>
internal sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
