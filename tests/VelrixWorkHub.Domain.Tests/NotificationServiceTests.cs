using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class NotificationServiceTests
{
    [Fact]
    public void Publish_IsIdempotentByRecipientAndDedupeKey()
    {
        var repository = new InMemoryNotificationRepository();
        var service = new NotificationService(repository);

        var first = service.Publish("admin", WorkNotificationKind.Approval, "待审批", "请处理", "/Workflow/Inbox", "workflow-task:1");
        Assert.Equal(0, repository.FindCount);
        var second = service.Publish("ADMIN", WorkNotificationKind.Approval, "重复", "不应新增", "/Workflow/Inbox", "workflow-task:1");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, repository.FindCount);
        Assert.Single(repository.Items);
        Assert.Equal(1, service.UnreadCount("admin"));
    }

    [Fact]
    public void MarkReadAndMarkAllReadOnlyAffectRecipientNotifications()
    {
        var repository = new InMemoryNotificationRepository();
        var service = new NotificationService(repository);
        var first = service.Publish("admin", WorkNotificationKind.Reminder, "提醒一", "内容一", null, "r1");
        service.Publish("admin", WorkNotificationKind.System, "提醒二", "内容二", null, "r2");
        service.Publish("finance", WorkNotificationKind.System, "其他人", "内容", null, "r3");

        service.MarkRead("admin", first.Id);
        Assert.Equal(1, service.UnreadCount("admin"));
        Assert.Equal(1, service.MarkAllRead("admin"));
        Assert.Equal(0, service.UnreadCount("admin"));
        Assert.Equal(1, service.UnreadCount("finance"));
    }

    [Fact]
    public void MarkReadRejectsAnotherRecipient()
    {
        var repository = new InMemoryNotificationRepository();
        var service = new NotificationService(repository);
        var notification = service.Publish("admin", WorkNotificationKind.System, "系统", "内容", null, "system:1");

        Assert.Throws<InvalidOperationException>(() => service.MarkRead("finance", notification.Id));
        Assert.False(notification.IsRead);
    }

    [Fact]
    public void ListPageAndDeleteKeepRecipientBoundary()
    {
        var repository = new InMemoryNotificationRepository();
        var service = new NotificationService(repository);
        var first = service.Publish("admin", WorkNotificationKind.System, "一", "内容", null, "page:1", new DateTime(2026, 7, 18, 10, 0, 0));
        var second = service.Publish("admin", WorkNotificationKind.System, "二", "内容", null, "page:2", new DateTime(2026, 7, 18, 11, 0, 0));
        var third = service.Publish("admin", WorkNotificationKind.System, "三", "内容", null, "page:3", new DateTime(2026, 7, 18, 12, 0, 0));
        var foreign = service.Publish("finance", WorkNotificationKind.System, "其他人", "内容", null, "page:4");

        var page = service.ListPage("ADMIN", pageIndex: 2, pageSize: 2);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.PageCount);
        Assert.Equal(2, page.PageIndex);
        Assert.Equal([first.Id], page.Items.Select(x => x.Id));

        Assert.Throws<InvalidOperationException>(() => service.Delete("finance", second.Id));
        Assert.Equal(1, service.DeleteMany("admin", [second.Id, foreign.Id]));
        Assert.Equal(2, service.List("admin").Count);
        Assert.Single(service.List("finance"));
        Assert.Equal([third.Id, first.Id], service.List("admin").Select(x => x.Id));
    }

    [Fact]
    public void PublishFailure_IsRecordedAfterWorkflowTransactionCommit()
    {
        var failures = new InMemoryNotificationFailureRecorder();
        var boundary = new DeferredTransactionBoundary();
        var service = new NotificationService(new ThrowingNotificationRepository(), failures, boundary);

        boundary.Execute(() => service.Publish("admin", WorkNotificationKind.System, "通知", "内容", null, "failure:commit"));

        var failure = Assert.Single(failures.List());
        Assert.Equal("failure:commit", failure.DedupeKey);
        Assert.NotNull(failure.Payload);
    }

    [Fact]
    public void PublishFailure_IsDiscardedWhenWorkflowTransactionRollsBack()
    {
        var failures = new InMemoryNotificationFailureRecorder();
        var boundary = new DeferredTransactionBoundary();
        var service = new NotificationService(new ThrowingNotificationRepository(), failures, boundary);

        Assert.Throws<InvalidOperationException>(() => boundary.Execute(() =>
        {
            service.Publish("admin", WorkNotificationKind.System, "通知", "内容", null, "failure:rollback");
            throw new InvalidOperationException("主事务失败");
        }));

        Assert.Empty(failures.List());
    }

    [Fact]
    public void Publish_WhenAtomicInsertLosesRace_ReturnsConcurrentNotificationWithoutFailureRecord()
    {
        var repository = new RacingNotificationRepository();
        var failures = new InMemoryNotificationFailureRecorder();
        var service = new NotificationService(repository, failures);

        var notification = service.Publish("admin", WorkNotificationKind.System, "并发通知", "内容", null, "race:1");

        Assert.Same(repository.Concurrent, notification);
        Assert.Empty(failures.List());
    }

    private sealed class InMemoryNotificationRepository : INotificationRepository
    {
        public List<WorkNotification> Items { get; } = [];
        public int FindCount { get; private set; }

        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false)
            => Items.Where(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase)).Where(x => !unreadOnly || !x.IsRead).ToArray();

        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey)
        {
            FindCount++;
            return Items.FirstOrDefault(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && x.DedupeKey.Equals(dedupeKey, StringComparison.Ordinal));
        }

        public void Add(WorkNotification notification) => Items.Add(notification);
        public bool TryAdd(WorkNotification notification)
        {
            if (Items.Any(x => x.Recipient.Equals(notification.Recipient, StringComparison.OrdinalIgnoreCase) && x.DedupeKey == notification.DedupeKey)) return false;
            Items.Add(notification);
            return true;
        }
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds)
        {
            var selected = Items.Where(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && notificationIds.Contains(x.Id)).ToArray();
            foreach (var item in selected) Items.Remove(item);
            return selected.Length;
        }
    }

    private sealed class RacingNotificationRepository : INotificationRepository
    {
        public WorkNotification? Concurrent { get; private set; }
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => Concurrent is null ? [] : [Concurrent];
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey)
            => Concurrent?.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) == true && Concurrent.DedupeKey == dedupeKey ? Concurrent : null;
        public void Add(WorkNotification notification)
        {
            Concurrent = notification;
            throw new InvalidOperationException("模拟通知唯一键竞态");
        }
        public bool TryAdd(WorkNotification notification)
        {
            Concurrent = notification;
            return false;
        }
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }

    private sealed class ThrowingNotificationRepository : INotificationRepository
    {
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => [];
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => throw new InvalidOperationException("通知存储不可用");
        public void Add(WorkNotification notification) => throw new InvalidOperationException("通知存储不可用");
        public bool TryAdd(WorkNotification notification) => throw new InvalidOperationException("通知存储不可用");
        public void Update(WorkNotification notification) => throw new InvalidOperationException("通知存储不可用");
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }

    private sealed class DeferredTransactionBoundary : IWorkflowTransactionBoundary
    {
        private readonly List<Action<Exception>> rollbackCallbacks = [];
        private readonly List<Action> commitCallbacks = [];
        private int depth;

        public void Execute(Action operation, Action<Exception>? afterRollback = null)
            => Execute(operation, afterRollback, null);

        public void Execute(Action operation, Action<Exception>? afterRollback, Action? afterCommit)
        {
            ArgumentNullException.ThrowIfNull(operation);
            var isRoot = depth++ == 0;
            if (afterRollback is not null) rollbackCallbacks.Add(afterRollback);
            if (afterCommit is not null) commitCallbacks.Add(afterCommit);
            try
            {
                operation();
                if (isRoot)
                {
                    var callbacks = commitCallbacks.ToArray();
                    commitCallbacks.Clear();
                    rollbackCallbacks.Clear();
                    foreach (var callback in callbacks) callback();
                }
            }
            catch (Exception exception)
            {
                if (isRoot)
                {
                    var callbacks = rollbackCallbacks.AsEnumerable().Reverse().ToArray();
                    commitCallbacks.Clear();
                    rollbackCallbacks.Clear();
                    foreach (var callback in callbacks)
                    {
                        try { callback(exception); }
                        catch { }
                    }
                }
                throw;
            }
            finally
            {
                depth--;
            }
        }
    }
}
