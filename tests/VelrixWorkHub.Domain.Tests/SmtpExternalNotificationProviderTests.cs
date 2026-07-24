using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.Notifications;

namespace VelrixWorkHub.Domain.Tests;

public sealed class SmtpExternalNotificationProviderTests
{
    [Fact]
    public async Task Send_BuildsPlainTextMailWithStableMessageId()
    {
        var sender = new CapturingSender();
        var provider = new SmtpExternalNotificationProvider(new ExternalNotificationEmailOptions
        {
            Enabled = true,
            Host = "smtp.example.test",
            Port = 465,
            UseSsl = true,
            FromAddress = "workflow@example.test",
            FromDisplayName = "流程中心",
            Username = "workflow",
            Password = "test-secret"
        }, sender);
        var message = new ExternalNotificationMessage(Guid.NewGuid(), ExternalNotificationChannel.Email, "employee@example.test", WorkNotificationKind.Approval, "审批已完成", "您的申请已批准。", "/Workflow/Inbox", "external:Email:employee:approval:42", DateTime.Now);

        await provider.SendAsync(message);
        await provider.SendAsync(message);

        Assert.Equal(2, sender.Messages.Count);
        var sent = sender.Messages[0];
        Assert.Equal("employee@example.test", sent.To);
        Assert.Equal("workflow@example.test", sent.FromAddress);
        Assert.Equal("流程中心", sent.FromDisplayName);
        Assert.Equal("审批已完成", sent.Subject);
        Assert.Contains("您的申请已批准。", sent.Body);
        Assert.Contains("办理入口：/Workflow/Inbox", sent.Body);
        Assert.StartsWith("<", sent.MessageId);
        Assert.EndsWith("@example.test>", sent.MessageId);
        Assert.Equal(sent.MessageId, sender.Messages[1].MessageId);
        Assert.DoesNotContain("test-secret", sent.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_UsesDifferentMessageIdForDifferentRecipientAddress()
    {
        var sender = new CapturingSender();
        var provider = new SmtpExternalNotificationProvider(new ExternalNotificationEmailOptions
        {
            Enabled = true,
            Host = "smtp.example.test",
            FromAddress = "workflow@example.test"
        }, sender);
        var first = new ExternalNotificationMessage(Guid.NewGuid(), ExternalNotificationChannel.Email, "old@example.test", WorkNotificationKind.System, "通知", "内容", null, "external:Email:employee:42", DateTime.Now);
        var second = first with { Address = "new@example.test" };

        await provider.SendAsync(first);
        await provider.SendAsync(second);

        Assert.NotEqual(sender.Messages[0].MessageId, sender.Messages[1].MessageId);
    }

    [Fact]
    public void Validate_EnabledMailChannelRequiresCompleteSafeConfiguration()
    {
        var missingHost = new ExternalNotificationEmailOptions { Enabled = true, FromAddress = "workflow@example.test" };
        var partialCredentials = new ExternalNotificationEmailOptions { Enabled = true, Host = "smtp.example.test", FromAddress = "workflow@example.test", Username = "workflow" };

        Assert.Throws<InvalidOperationException>(missingHost.Validate);
        Assert.Throws<InvalidOperationException>(partialCredentials.Validate);
        new ExternalNotificationEmailOptions { Enabled = false }.Validate();
    }

    [Fact]
    public async Task Send_RejectsNonEmailChannelBeforeCallingSender()
    {
        var sender = new CapturingSender();
        var provider = new SmtpExternalNotificationProvider(new ExternalNotificationEmailOptions
        {
            Enabled = true,
            Host = "smtp.example.test",
            FromAddress = "workflow@example.test"
        }, sender);
        var message = new ExternalNotificationMessage(Guid.NewGuid(), ExternalNotificationChannel.Sms, "13800000000", WorkNotificationKind.System, "系统通知", "内容", null, "key", DateTime.Now);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SendAsync(message));
        Assert.Empty(sender.Messages);
    }

    private sealed class CapturingSender : IExternalSmtpSender
    {
        public List<ExternalSmtpMessage> Messages { get; } = [];
        public Task SendAsync(ExternalSmtpMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
