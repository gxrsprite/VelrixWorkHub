using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ExternalNotificationDispatcherTests
{
    [Fact]
    public async Task Dispatch_SendsConfiguredChannelsOnceAndIsolatesProviderFailures()
    {
        var notification = new WorkNotification("admin", WorkNotificationKind.Reminder, "待处理事项", "请及时处理。", "/Pmp/WorkItem", "work-item:1", new DateTime(2026, 7, 22, 9, 0, 0));
        var email = new RecordingProvider(ExternalNotificationChannel.Email);
        var dispatcher = new ExternalNotificationDispatcher(
            new RecipientResolver(
            [
                new ExternalNotificationRecipient(ExternalNotificationChannel.Email, "admin@example.com"),
                new ExternalNotificationRecipient(ExternalNotificationChannel.Email, "ADMIN@example.com"),
                new ExternalNotificationRecipient(ExternalNotificationChannel.Sms, "13800138000"),
                new ExternalNotificationRecipient(ExternalNotificationChannel.WeCom, "wecom-admin")
            ]),
            [email, new ThrowingProvider(ExternalNotificationChannel.Sms)]);

        var result = await dispatcher.DispatchAsync(notification);

        Assert.Equal(3, result.ResolvedRecipientCount);
        Assert.Equal(1, result.SentCount);
        Assert.Equal(1, result.SkippedCount);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(ExternalNotificationChannel.Sms, failure.Message.Channel);
        Assert.Equal("短信网关不可用", failure.Error);
        var sent = Assert.Single(email.Messages);
        Assert.Equal(ExternalNotificationChannel.Email, sent.Channel);
        Assert.Equal("admin@example.com", sent.Address);
        Assert.Equal("external:Email:admin:work-item:1", sent.DedupeKey);
        Assert.Equal(notification.Id, sent.NotificationId);
    }

    [Fact]
    public async Task Dispatch_WithNoResolvedAddress_DoesNotCallProvider()
    {
        var provider = new RecordingProvider(ExternalNotificationChannel.DingTalk);
        var dispatcher = new ExternalNotificationDispatcher(new EmptyExternalNotificationRecipientResolver(), [provider]);

        var result = await dispatcher.DispatchAsync(new WorkNotification("admin", WorkNotificationKind.System, "系统消息", "内容", null, "system:1"));

        Assert.Equal(0, result.ResolvedRecipientCount);
        Assert.Equal(0, result.SentCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Empty(result.Failures);
        Assert.Empty(provider.Messages);
    }

    private sealed class RecipientResolver(IReadOnlyList<ExternalNotificationRecipient> recipients) : IExternalNotificationRecipientResolver
    {
        public IReadOnlyList<ExternalNotificationRecipient> Resolve(WorkNotification notification) => recipients;
    }

    private sealed class RecordingProvider(ExternalNotificationChannel channel) : IExternalNotificationChannelProvider
    {
        public ExternalNotificationChannel Channel { get; } = channel;
        public List<ExternalNotificationMessage> Messages { get; } = [];
        public Task SendAsync(ExternalNotificationMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingProvider(ExternalNotificationChannel channel) : IExternalNotificationChannelProvider
    {
        public ExternalNotificationChannel Channel { get; } = channel;
        public Task SendAsync(ExternalNotificationMessage message, CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("短信网关不可用"));
    }
}
