using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ExternalNotificationOutboxServiceTests
{
    [Fact]
    public async Task DeliverPending_KeepsUnconfiguredChannelPendingAndRetriesFailedProvider()
    {
        var repository = new OutboxRepository();
        var email = new RecordingProvider(ExternalNotificationChannel.Email);
        var service = new ExternalNotificationOutboxService(
            repository,
            new RecipientResolver(
            [
                new ExternalNotificationRecipient(ExternalNotificationChannel.Email, "admin@example.com"),
                new ExternalNotificationRecipient(ExternalNotificationChannel.Sms, "13800138000"),
                new ExternalNotificationRecipient(ExternalNotificationChannel.WeCom, "wecom-admin")
            ]),
            [email, new ThrowingProvider(ExternalNotificationChannel.Sms)]);
        var notification = new WorkNotification("admin", WorkNotificationKind.Reminder, "逾期工作项", "请处理。", "/Pmp/WorkItem", "work-item:overdue", new DateTime(2026, 7, 22, 10, 0, 0));

        Assert.Equal(3, service.Enqueue(notification));
        Assert.Equal(0, service.Enqueue(notification));
        var result = await service.DeliverPendingAsync(attemptedAt: new DateTime(2026, 7, 22, 10, 1, 0));

        Assert.Equal(3, result.CandidateCount);
        Assert.Equal(1, result.DeliveredCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Single(email.Messages);
        Assert.Contains(repository.Items, item => item.Message.Channel == ExternalNotificationChannel.Email && item.Status == ExternalNotificationDeliveryStatus.Delivered);
        Assert.Contains(repository.Items, item => item.Message.Channel == ExternalNotificationChannel.Sms && item.Status == ExternalNotificationDeliveryStatus.Pending && item.RetryCount == 1 && item.LastError == "短信网关不可用" && item.NextAttemptAt == new DateTime(2026, 7, 22, 10, 6, 0));
        Assert.Contains(repository.Items, item => item.Message.Channel == ExternalNotificationChannel.WeCom && item.Status == ExternalNotificationDeliveryStatus.Pending && item.RetryCount == 0);
    }

    [Fact]
    public void Publish_EnqueuesExternalMessageOnlyAfterTransactionCommit()
    {
        var outbox = new OutboxRepository();
        var outboxService = new ExternalNotificationOutboxService(
            outbox,
            new RecipientResolver([new ExternalNotificationRecipient(ExternalNotificationChannel.Email, "admin@example.com")]),
            []);
        var boundary = new DeferredTransactionBoundary();
        var notifications = new NotificationRepository();
        var service = new NotificationService(notifications, transactions: boundary, externalOutbox: outboxService);

        boundary.Execute(() => service.Publish("admin", WorkNotificationKind.System, "系统消息", "内容", null, "system:outbox"));
        Assert.Single(outbox.Items);

        Assert.Throws<InvalidOperationException>(() => boundary.Execute(() =>
        {
            service.Publish("admin", WorkNotificationKind.System, "不会入队", "内容", null, "system:rollback");
            throw new InvalidOperationException("主交易回滚");
        }));
        Assert.Single(outbox.Items);
    }

    [Fact]
    public void InspectPending_ReturnsOnlyOperationalMetadata()
    {
        var repository = new OutboxRepository();
        var service = new ExternalNotificationOutboxService(repository, new RecipientResolver([]), []);
        repository.TryAdd(new ExternalNotificationMessage(Guid.CreateVersion7(), ExternalNotificationChannel.Email, "admin@example.com", WorkNotificationKind.System, "不应展示的标题", "不应展示的正文", null, "external:email:1", DateTime.Now));
        var item = Assert.Single(repository.Items);
        repository.TryClaim(item.Id, DateTime.Now, TimeSpan.FromMinutes(5));
        repository.MarkFailed(item.Id, "渠道失败", DateTime.Now);

        var summary = service.InspectPending(asOf: DateTime.Now.AddMinutes(1));

        Assert.Equal(1, summary.PendingCount);
        Assert.Equal(0, summary.DeferredCount);
        Assert.Equal(1, summary.FailedAttemptCount);
        Assert.Equal(1, summary.MaxRetryCount);
    }

    [Fact]
    public void InspectPending_SeparatesDeferredRetriesFromImmediatelyEligiblePending()
    {
        var repository = new OutboxRepository();
        var service = new ExternalNotificationOutboxService(repository, new RecipientResolver([]), []);
        var now = new DateTime(2026, 7, 22, 10, 0, 0);
        repository.TryAdd(new ExternalNotificationMessage(Guid.CreateVersion7(), ExternalNotificationChannel.Email, "admin@example.com", WorkNotificationKind.System, "延迟", "内容", null, "external:deferred", now));
        repository.TryAdd(new ExternalNotificationMessage(Guid.CreateVersion7(), ExternalNotificationChannel.WeCom, "admin", WorkNotificationKind.System, "可立即投递", "内容", null, "external:ready", now));
        var delayed = repository.Items.Single(item => item.Message.DedupeKey == "external:deferred");
        repository.TryClaim(delayed.Id, now, TimeSpan.FromMinutes(5));
        repository.MarkFailed(delayed.Id, "渠道失败", now, now.AddMinutes(15));

        var summary = service.InspectPending(asOf: now);

        Assert.Equal(2, summary.PendingCount);
        Assert.Equal(1, summary.DeferredCount);
        Assert.Equal(1, summary.FailedAttemptCount);
    }

    [Fact]
    public void InspectChannels_SeparatesPendingDeferredAndFailedCountsWithoutPayload()
    {
        var repository = new OutboxRepository();
        var service = new ExternalNotificationOutboxService(repository, new RecipientResolver([]), []);
        var now = new DateTime(2026, 7, 22, 10, 0, 0);
        repository.TryAdd(new ExternalNotificationMessage(Guid.CreateVersion7(), ExternalNotificationChannel.Email, "admin@example.com", WorkNotificationKind.System, "邮件标题", "邮件正文", null, "external:email-channel", now));
        repository.TryAdd(new ExternalNotificationMessage(Guid.CreateVersion7(), ExternalNotificationChannel.Sms, "13800138000", WorkNotificationKind.System, "短信标题", "短信正文", null, "external:sms-channel", now));
        var email = repository.Items.Single(item => item.Message.Channel == ExternalNotificationChannel.Email);
        repository.TryClaim(email.Id, now, TimeSpan.FromMinutes(5));
        repository.MarkFailed(email.Id, "邮件渠道失败", now, now.AddMinutes(15));

        var summaries = service.InspectChannels(asOf: now);

        Assert.Equal(4, summaries.Count);
        Assert.Equal(new ExternalNotificationOutboxChannelSummary(ExternalNotificationChannel.Email, 1, 1, 1, 1), summaries.Single(item => item.Channel == ExternalNotificationChannel.Email));
        Assert.Equal(new ExternalNotificationOutboxChannelSummary(ExternalNotificationChannel.Sms, 1, 0, 0, 0), summaries.Single(item => item.Channel == ExternalNotificationChannel.Sms));
        Assert.All(summaries, item => Assert.DoesNotContain("标题", item.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeliverPending_DefersFailedProviderUntilItsRetryWindow()
    {
        var repository = new OutboxRepository();
        var service = new ExternalNotificationOutboxService(
            repository,
            new RecipientResolver([new ExternalNotificationRecipient(ExternalNotificationChannel.Email, "admin@example.com")]),
            [new ThrowingProvider(ExternalNotificationChannel.Email)]);
        service.Enqueue(new WorkNotification("admin", WorkNotificationKind.System, "系统消息", "内容", null, "outbox:backoff", new DateTime(2026, 7, 22, 10, 0, 0)));

        var first = await service.DeliverPendingAsync(attemptedAt: new DateTime(2026, 7, 22, 10, 0, 0));
        var deferred = await service.DeliverPendingAsync(attemptedAt: new DateTime(2026, 7, 22, 10, 4, 59));
        var second = await service.DeliverPendingAsync(attemptedAt: new DateTime(2026, 7, 22, 10, 5, 0));

        Assert.Equal(1, first.FailedCount);
        Assert.Equal(0, deferred.CandidateCount);
        Assert.Equal(1, second.FailedCount);
        var item = Assert.Single(repository.Items);
        Assert.Equal(2, item.RetryCount);
        Assert.Equal(new DateTime(2026, 7, 22, 10, 20, 0), item.NextAttemptAt);
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
        public Task SendAsync(ExternalNotificationMessage message, CancellationToken cancellationToken = default) => Task.FromException(new InvalidOperationException("短信网关不可用"));
    }

    private sealed class OutboxRepository : IExternalNotificationOutboxRepository
    {
        public List<PersistedExternalNotificationDelivery> Items { get; } = [];
        public bool TryAdd(ExternalNotificationMessage message)
        {
            if (Items.Any(item => item.Message.Channel == message.Channel && item.Message.Address == message.Address && item.Message.DedupeKey == message.DedupeKey)) return false;
            Items.Add(new PersistedExternalNotificationDelivery(Guid.CreateVersion7(), message, ExternalNotificationDeliveryStatus.Pending, 0, null, null, null));
            return true;
        }
        public IReadOnlyList<PersistedExternalNotificationDelivery> ListPending(int take, DateTime? dueAt = null) => Items.Where(item => item.Status == ExternalNotificationDeliveryStatus.Pending && (dueAt == null || item.NextAttemptAt == null || item.NextAttemptAt <= dueAt)).Take(take).ToArray();
        public bool TryClaim(Guid id, DateTime attemptedAt, TimeSpan lease)
        {
            var item = Items.SingleOrDefault(item => item.Id == id);
            if (item is null || item.Status != ExternalNotificationDeliveryStatus.Pending || (item.LastAttemptAt is not null && attemptedAt - item.LastAttemptAt.Value < lease)) return false;
            Replace(item with { RetryCount = item.RetryCount + 1, LastAttemptAt = attemptedAt });
            return true;
        }
        public void MarkDelivered(Guid id, DateTime deliveredAt)
        {
            var item = Items.Single(item => item.Id == id && item.Status == ExternalNotificationDeliveryStatus.Pending);
            Replace(item with { Status = ExternalNotificationDeliveryStatus.Delivered, DeliveredAt = deliveredAt, LastAttemptAt = deliveredAt, LastError = null });
        }
        public void MarkFailed(Guid id, string error, DateTime attemptedAt)
            => MarkFailed(id, error, attemptedAt, attemptedAt);
        public void MarkFailed(Guid id, string error, DateTime attemptedAt, DateTime nextAttemptAt)
        {
            var item = Items.Single(item => item.Id == id && item.Status == ExternalNotificationDeliveryStatus.Pending);
            Replace(item with { LastError = error, LastAttemptAt = attemptedAt, NextAttemptAt = nextAttemptAt });
        }
        private void Replace(PersistedExternalNotificationDelivery item) => Items[Items.FindIndex(existing => existing.Id == item.Id)] = item;
    }

    private sealed class NotificationRepository : INotificationRepository
    {
        private readonly List<WorkNotification> items = [];
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => items.Where(item => item.Recipient == recipient).ToArray();
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => items.SingleOrDefault(item => item.Recipient == recipient && item.DedupeKey == dedupeKey);
        public void Add(WorkNotification notification) => items.Add(notification);
        public bool TryAdd(WorkNotification notification)
        {
            if (FindByDedupeKey(notification.Recipient, notification.DedupeKey) is not null) return false;
            items.Add(notification);
            return true;
        }
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }

    private sealed class DeferredTransactionBoundary : IWorkflowTransactionBoundary
    {
        private readonly List<Action> commitCallbacks = [];
        private int depth;
        public void Execute(Action operation, Action<Exception>? afterRollback = null) => Execute(operation, afterRollback, null);
        public void Execute(Action operation, Action<Exception>? afterRollback, Action? afterCommit)
        {
            var isRoot = depth++ == 0;
            if (afterCommit is not null) commitCallbacks.Add(afterCommit);
            try
            {
                operation();
                if (isRoot)
                {
                    var callbacks = commitCallbacks.ToArray();
                    commitCallbacks.Clear();
                    foreach (var callback in callbacks) callback();
                }
            }
            catch
            {
                if (isRoot) commitCallbacks.Clear();
                throw;
            }
            finally { depth--; }
        }
    }
}
