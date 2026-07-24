using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class NotificationFailureRetryServiceTests
{
    [Fact]
    public void RetryPending_PublishesPayloadAndMarksFailureResolved()
    {
        var payload = new NotificationDeliveryPayload("Finance", WorkNotificationKind.System, "待重试", "请处理", "/Workflow/Inbox", "retry:1", new DateTime(2026, 7, 17, 10, 0, 0));
        var failures = new InMemoryFailureRepository(new PersistedNotificationFailure(Guid.CreateVersion7(), "publish", "finance", "retry:1", "写入失败", new DateTime(2026, 7, 17, 10, 0, 1), payload, NotificationFailureStatus.Pending, 0, null, null));
        var notifications = new InMemoryNotificationRepository();
        var service = new NotificationFailureRetryService(notifications, failures);

        var count = service.RetryPending(attemptedAt: new DateTime(2026, 7, 17, 10, 1, 0));

        Assert.Equal(1, count);
        Assert.Single(notifications.Items);
        Assert.Equal("finance", notifications.Items[0].Recipient);
        Assert.Equal(WorkNotificationKind.System, notifications.Items[0].Kind);
        Assert.Equal(NotificationFailureStatus.Resolved, failures.Item.Status);
        Assert.Equal(1, failures.Item.RetryCount);
        Assert.Equal(new DateTime(2026, 7, 17, 10, 1, 0), failures.Item.ResolvedAt);
    }

    [Fact]
    public void InspectPending_ReportsHighRetryFailures()
    {
        var payload = new NotificationDeliveryPayload("finance", WorkNotificationKind.System, "持续失败", "请人工处理", null, "retry:alert", new DateTime(2026, 7, 17, 10, 0, 0));
        var failures = new InMemoryFailureRepository(new PersistedNotificationFailure(Guid.CreateVersion7(), "publish", "finance", "retry:alert", "持续失败", DateTime.Now, payload, NotificationFailureStatus.Pending, 3, DateTime.Now, null));
        var service = new NotificationFailureRetryService(new InMemoryNotificationRepository(), failures);

        var summary = service.InspectPending(alertRetryThreshold: 3);

        Assert.Equal(1, summary.PendingCount);
        Assert.Equal(1, summary.HighRetryCount);
        Assert.Equal(3, summary.MaxRetryCount);
    }

    [Fact]
    public void ManualRetry_RecordsIndependentAuditWithoutChangingRetryResult()
    {
        var payload = new NotificationDeliveryPayload("finance", WorkNotificationKind.System, "手动重试", "请处理", null, "retry:audit", DateTime.Now);
        var failures = new InMemoryFailureRepository(new PersistedNotificationFailure(Guid.CreateVersion7(), "publish", "finance", "retry:audit", "初始失败", DateTime.Now, payload, NotificationFailureStatus.Pending, 0, null, null));
        var audits = new InMemoryFailureAuditRepository();
        var service = new NotificationFailureRetryService(new InMemoryNotificationRepository(), failures, audits);

        Assert.True(service.Retry(failures.Item.Id, new DateTime(2026, 7, 18, 12, 0, 0), "admin"));

        var audit = Assert.Single(audits.Items);
        Assert.Equal(failures.Item.Id, audit.FailureId);
        Assert.Equal("ManualRetrySucceeded", audit.Action);
        Assert.Equal("admin", audit.Actor);
    }

    [Fact]
    public void RetryMany_DeduplicatesIdsAndReturnsBatchResult()
    {
        var payload = new NotificationDeliveryPayload("finance", WorkNotificationKind.System, "批量重试", "请处理", null, "retry:batch", DateTime.Now);
        var failures = new InMemoryFailureRepository(new PersistedNotificationFailure(Guid.CreateVersion7(), "publish", "finance", "retry:batch", "初始失败", DateTime.Now, payload, NotificationFailureStatus.Pending, 0, null, null));
        var service = new NotificationFailureRetryService(new InMemoryNotificationRepository(), failures);

        var result = service.RetryMany([failures.Item.Id, failures.Item.Id], "admin", new DateTime(2026, 7, 18, 12, 0, 0));

        Assert.Equal(1, result.RequestedCount);
        Assert.Equal(1, result.ResolvedCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public void RetryMany_RejectsMoreThanFiftyIds()
    {
        var service = new NotificationFailureRetryService(new InMemoryNotificationRepository(), new InMemoryFailureRepository());

        Assert.Throws<ArgumentOutOfRangeException>(() => service.RetryMany(Enumerable.Range(0, 51).Select(_ => Guid.CreateVersion7()), "admin"));
    }

    [Fact]
    public void RetryPending_WhenNotificationWriteFails_KeepsPendingAndRecordsAttempt()
    {
        var payload = new NotificationDeliveryPayload("finance", WorkNotificationKind.System, "待重试", "请处理", null, "retry:2", new DateTime(2026, 7, 17, 10, 0, 0));
        var failures = new InMemoryFailureRepository(new PersistedNotificationFailure(Guid.CreateVersion7(), "publish", "finance", "retry:2", "初始失败", new DateTime(2026, 7, 17, 10, 0, 1), payload, NotificationFailureStatus.Pending, 0, null, null));
        var service = new NotificationFailureRetryService(new ThrowingNotificationRepository(), failures);

        var count = service.RetryPending(attemptedAt: new DateTime(2026, 7, 17, 10, 1, 0));

        Assert.Equal(0, count);
        Assert.Equal(NotificationFailureStatus.Pending, failures.Item.Status);
        Assert.Equal(1, failures.Item.RetryCount);
        Assert.Equal(new DateTime(2026, 7, 17, 10, 1, 0), failures.Item.LastRetryAt);
        Assert.Equal("重试写入失败", failures.Item.Error);
    }

    [Fact]
    public void Retry_WhenAlreadyResolved_DoesNotRepublishOrIncrementAgain()
    {
        var payload = new NotificationDeliveryPayload("finance", WorkNotificationKind.System, "待重试", "请处理", null, "retry:3", new DateTime(2026, 7, 17, 10, 0, 0));
        var failures = new InMemoryFailureRepository(new PersistedNotificationFailure(Guid.CreateVersion7(), "publish", "finance", "retry:3", "初始失败", new DateTime(2026, 7, 17, 10, 0, 1), payload, NotificationFailureStatus.Pending, 0, null, null));
        var notifications = new InMemoryNotificationRepository();
        var service = new NotificationFailureRetryService(notifications, failures);

        Assert.True(service.Retry(failures.Item.Id, new DateTime(2026, 7, 17, 10, 1, 0)));
        Assert.False(service.Retry(failures.Item.Id, new DateTime(2026, 7, 17, 10, 2, 0)));

        Assert.Single(notifications.Items);
        Assert.Equal(1, failures.Item.RetryCount);
        Assert.Equal(new DateTime(2026, 7, 17, 10, 1, 0), failures.Item.ResolvedAt);
    }

    [Fact]
    public void Retry_WhenAnotherWorkerHoldsLease_SkipsDelivery()
    {
        var attemptedAt = new DateTime(2026, 7, 18, 12, 0, 0);
        var payload = new NotificationDeliveryPayload("finance", WorkNotificationKind.System, "待重试", "请处理", null, "retry:lease", attemptedAt);
        var failures = new InMemoryFailureRepository(new PersistedNotificationFailure(Guid.CreateVersion7(), "publish", "finance", "retry:lease", "初始失败", attemptedAt, payload, NotificationFailureStatus.Pending, 1, attemptedAt, null));
        var notifications = new InMemoryNotificationRepository();
        var service = new NotificationFailureRetryService(notifications, failures);

        Assert.False(service.Retry(failures.Item.Id, attemptedAt.AddMinutes(1)));

        Assert.Empty(notifications.Items);
        Assert.Equal(1, failures.Item.RetryCount);
    }

    [Fact]
    public void Retry_WhenAnotherWorkerChangesFailureState_DoesNotThrow()
    {
        var payload = new NotificationDeliveryPayload("finance", WorkNotificationKind.System, "待重试", "请处理", null, "retry:4", new DateTime(2026, 7, 17, 10, 0, 0));
        var failures = new InMemoryFailureRepository(new PersistedNotificationFailure(Guid.CreateVersion7(), "publish", "finance", "retry:4", "初始失败", new DateTime(2026, 7, 17, 10, 0, 1), payload, NotificationFailureStatus.Pending, 0, null, null), throwOnFailureMark: true);
        var service = new NotificationFailureRetryService(new ThrowingNotificationRepository(), failures);

        Assert.False(service.Retry(failures.Item.Id));
    }

    [Fact]
    public void Retry_WhenNotificationAlreadyExists_UsesAtomicInsertAndResolvesFailure()
    {
        var payload = new NotificationDeliveryPayload("finance", WorkNotificationKind.System, "已存在", "无需重复投递", null, "retry:atomic", DateTime.Now);
        var failures = new InMemoryFailureRepository(new PersistedNotificationFailure(Guid.CreateVersion7(), "publish", "finance", "retry:atomic", "初始失败", DateTime.Now, payload, NotificationFailureStatus.Pending, 0, null, null));
        var notifications = new AlreadyExistingNotificationRepository();
        var service = new NotificationFailureRetryService(notifications, failures);

        Assert.True(service.Retry(failures.Item.Id, new DateTime(2026, 7, 18, 12, 0, 0)));

        Assert.Equal(1, notifications.TryAddCount);
        Assert.Equal(NotificationFailureStatus.Resolved, failures.Item.Status);
        Assert.Equal(1, failures.Item.RetryCount);
    }

    [Fact]
    public void Retry_WhenResolveFails_RemovesOnlyNotificationCreatedByThisAttempt()
    {
        var payload = new NotificationDeliveryPayload("finance", WorkNotificationKind.System, "需要补偿", "不会残留", null, "retry:compensation", DateTime.Now);
        var failures = new InMemoryFailureRepository(
            new PersistedNotificationFailure(Guid.CreateVersion7(), "publish", "finance", "retry:compensation", "初始失败", DateTime.Now, payload, NotificationFailureStatus.Pending, 0, null, null),
            throwOnResolve: true);
        var notifications = new InMemoryNotificationRepository();
        var service = new NotificationFailureRetryService(notifications, failures);

        Assert.False(service.Retry(failures.Item.Id, new DateTime(2026, 7, 18, 12, 1, 0)));

        Assert.Empty(notifications.Items);
        Assert.Equal(NotificationFailureStatus.Pending, failures.Item.Status);
        Assert.Equal(1, failures.Item.RetryCount);
    }

    private sealed class InMemoryNotificationRepository : INotificationRepository
    {
        public List<WorkNotification> Items { get; } = [];
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => Items.Where(x => x.Recipient == recipient).ToArray();
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => Items.SingleOrDefault(x => x.Recipient == recipient && x.DedupeKey == dedupeKey);
        public void Add(WorkNotification notification) => Items.Add(notification);
        public bool TryAdd(WorkNotification notification)
        {
            if (Items.Any(x => x.Recipient == notification.Recipient && x.DedupeKey == notification.DedupeKey)) return false;
            Items.Add(notification);
            return true;
        }
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds)
        {
            var ids = notificationIds.ToHashSet();
            return Items.RemoveAll(x => x.Recipient == recipient && ids.Contains(x.Id));
        }
    }

    private sealed class ThrowingNotificationRepository : INotificationRepository
    {
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => [];
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => null;
        public void Add(WorkNotification notification) => throw new InvalidOperationException("重试写入失败");
        public bool TryAdd(WorkNotification notification) => throw new InvalidOperationException("重试写入失败");
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }

    private sealed class AlreadyExistingNotificationRepository : INotificationRepository
    {
        public int TryAddCount { get; private set; }
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => [];
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => throw new InvalidOperationException("补投不得先查通知");
        public void Add(WorkNotification notification) => throw new InvalidOperationException("补投不得走非原子写入");
        public bool TryAdd(WorkNotification notification) { TryAddCount++; return false; }
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }

    private sealed class InMemoryFailureRepository(PersistedNotificationFailure? seed = null, bool throwOnFailureMark = false, bool throwOnResolve = false) : INotificationFailureRepository
    {
        public PersistedNotificationFailure Item { get; private set; } = seed ?? new PersistedNotificationFailure(Guid.CreateVersion7(), "publish", "finance", "seed", "初始失败", DateTime.Now, new NotificationDeliveryPayload("finance", WorkNotificationKind.System, "seed", "seed", null, "seed", DateTime.Now), NotificationFailureStatus.Pending, 0, null, null);
        public IReadOnlyList<PersistedNotificationFailure> ListPending(int take) => Item.Status == NotificationFailureStatus.Pending ? [Item] : [];
        public PersistedNotificationFailure? FindPending(Guid id) => Item.Id == id && Item.Status == NotificationFailureStatus.Pending ? Item : null;
        public bool TryClaim(Guid id, DateTime attemptedAt, TimeSpan lease)
        {
            if (Item.Id != id || Item.Status != NotificationFailureStatus.Pending || (Item.LastRetryAt is not null && attemptedAt - Item.LastRetryAt.Value < lease)) return false;
            Item = Item with { RetryCount = Item.RetryCount + 1, LastRetryAt = attemptedAt };
            return true;
        }
        public void MarkRetryFailed(Guid id, string error, DateTime attemptedAt)
        {
            if (throwOnFailureMark) throw new InvalidOperationException("记录已被其他执行者更新。");
            Item = Item with { Error = error, LastRetryAt = attemptedAt };
        }
        public void MarkResolved(Guid id, DateTime resolvedAt)
        {
            if (throwOnResolve) throw new InvalidOperationException("失败记录状态写入失败");
            Item = Item with { Status = NotificationFailureStatus.Resolved, LastRetryAt = resolvedAt, ResolvedAt = resolvedAt };
        }
    }

    private sealed class InMemoryFailureAuditRepository : INotificationFailureAuditRepository
    {
        public List<NotificationFailureAuditEntry> Items { get; } = [];
        public IReadOnlyList<NotificationFailureAuditEntry> List(Guid? failureId = null, int take = 100)
            => Items.Where(x => failureId is null || x.FailureId == failureId).Take(take).ToArray();
        public void Add(NotificationFailureAuditEntry entry) => Items.Add(entry);
    }
}
