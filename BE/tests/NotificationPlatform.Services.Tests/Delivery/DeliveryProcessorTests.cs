using NotificationPlatform.Domain.Constants;
using NotificationPlatform.Domain.DomainModels;
using NotificationPlatform.Services.Abstractions;
using NotificationPlatform.Services.Delivery;
using NSubstitute;

namespace NotificationPlatform.Services.Tests.Delivery;

public class DeliveryProcessorTests
{
    private const string NotificationId = "6af8daacb866548119b05e40";
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private readonly INotificationDataAccess _notifications = Substitute.For<INotificationDataAccess>();
    private readonly IDeliveryLogDataAccess _deliveryLogs = Substitute.For<IDeliveryLogDataAccess>();
    private readonly FakeEmailSender _emailSender = new();
    private readonly DeliveryProcessor _processor;

    public DeliveryProcessorTests()
    {
        _processor = new DeliveryProcessor(_notifications, _deliveryLogs, _emailSender, new TestTimeProvider(Now));

        // By default the claim succeeds, like a queued notification in the database.
        _notifications.TryClaimAsync(NotificationId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new Notification
            {
                Id = NotificationId,
                TenantId = "tenant-1",
                To = "user@example.com",
                Subject = "Hello",
                Body = "Body",
                Status = NotificationStatus.Processing,
                Attempts = call.ArgAt<int>(1),
            });
    }

    [Fact]
    public async Task Success_OnFirstAttempt_LogsSuccessAndMarksDelivered()
    {
        var result = await _processor.ProcessAsync(NotificationId, attempt: 1, DeliveryProcessor.MaxAttempts);

        Assert.Equal(new DeliveryResult(DeliveryOutcome.Delivered), result);
        Assert.Equal(["user@example.com"], _emailSender.SentTo);
        await _deliveryLogs.Received(1).InsertAsync(
            Arg.Is<DeliveryLog>(log =>
                log.NotificationId == NotificationId && log.TenantId == "tenant-1" && log.Attempt == 1 &&
                log.Status == DeliveryStatus.Success && log.ErrorMessage == null && log.AttemptedAt == Now.UtcDateTime),
            Arg.Any<CancellationToken>());
        await _notifications.Received(1).UpdateStatusAsync(
            NotificationId, NotificationStatus.Delivered, null, Now.UtcDateTime, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailureThenSuccess_FirstAttemptRetriesSecondDelivers()
    {
        _emailSender.FailuresRemaining = 1;

        var first = await _processor.ProcessAsync(NotificationId, attempt: 1, DeliveryProcessor.MaxAttempts);

        Assert.Equal(new DeliveryResult(DeliveryOutcome.Retry, FakeEmailSender.FailureMessage), first);
        await _deliveryLogs.Received(1).InsertAsync(
            Arg.Is<DeliveryLog>(log =>
                log.Attempt == 1 && log.Status == DeliveryStatus.Failed && log.ErrorMessage == FakeEmailSender.FailureMessage),
            Arg.Any<CancellationToken>());
        await _notifications.Received(1).UpdateStatusAsync(
            NotificationId, NotificationStatus.Queued, FakeEmailSender.FailureMessage, null, Arg.Any<CancellationToken>());

        var second = await _processor.ProcessAsync(NotificationId, attempt: 2, DeliveryProcessor.MaxAttempts);

        Assert.Equal(DeliveryOutcome.Delivered, second.Outcome);
        await _deliveryLogs.Received(1).InsertAsync(
            Arg.Is<DeliveryLog>(log => log.Attempt == 2 && log.Status == DeliveryStatus.Success),
            Arg.Any<CancellationToken>());
        await _notifications.Received(1).UpdateStatusAsync(
            NotificationId, NotificationStatus.Delivered, null, Now.UtcDateTime, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailureOnLastAttempt_MarksFailed()
    {
        _emailSender.FailuresRemaining = 1;

        var result = await _processor.ProcessAsync(NotificationId, attempt: 3, DeliveryProcessor.MaxAttempts);

        Assert.Equal(new DeliveryResult(DeliveryOutcome.FailedFinal, FakeEmailSender.FailureMessage), result);
        await _deliveryLogs.Received(1).InsertAsync(
            Arg.Is<DeliveryLog>(log => log.Attempt == 3 && log.Status == DeliveryStatus.Failed),
            Arg.Any<CancellationToken>());
        await _notifications.Received(1).UpdateStatusAsync(
            NotificationId, NotificationStatus.Failed, FakeEmailSender.FailureMessage, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NothingToClaim_SkipsWithoutSendingOrWriting()
    {
        // Already delivered (or failed, unknown, or a duplicate of an attempt already claimed).
        _notifications.TryClaimAsync(NotificationId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Notification?)null);

        var result = await _processor.ProcessAsync(NotificationId, attempt: 1, DeliveryProcessor.MaxAttempts);

        Assert.Equal(new DeliveryResult(DeliveryOutcome.Delivered), result);
        Assert.Empty(_emailSender.SentTo);
        await _deliveryLogs.DidNotReceiveWithAnyArgs().InsertAsync(default!, default);
        await _notifications.DidNotReceiveWithAnyArgs().UpdateStatusAsync(default!, default!, default, default, default);
    }

    [Fact]
    public async Task CancelledAfterSuccessfulSend_StillRecordsDelivery()
    {
        using var cancellation = new CancellationTokenSource();
        _emailSender.AfterSend = cancellation.Cancel;

        var result = await _processor.ProcessAsync(NotificationId, attempt: 1, DeliveryProcessor.MaxAttempts, cancellation.Token);

        Assert.Equal(DeliveryOutcome.Delivered, result.Outcome);
        await _deliveryLogs.Received(1).InsertAsync(
            Arg.Is<DeliveryLog>(log => log.Status == DeliveryStatus.Success),
            Arg.Is<CancellationToken>(token => !token.IsCancellationRequested));
        await _notifications.Received(1).UpdateStatusAsync(
            NotificationId, NotificationStatus.Delivered, null, Now.UtcDateTime,
            Arg.Is<CancellationToken>(token => !token.IsCancellationRequested));
    }
}
