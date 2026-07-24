using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Workflow;

namespace VelrixWorkHub.Application.Notifications;

public sealed class NotificationService(INotificationRepository repository, INotificationFailureRecorder? failureRecorder = null, IWorkflowTransactionBoundary? transactions = null, ExternalNotificationOutboxService? externalOutbox = null)
{
    public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false)
    {
        EnsureRecipient(recipient);
        return repository.List(NormalizeRecipient(recipient), unreadOnly)
            .OrderByDescending(x => x.CreatedAt)
            .ToArray();
    }

    public int UnreadCount(string recipient)
    {
        EnsureRecipient(recipient);
        return repository.Count(NormalizeRecipient(recipient), unreadOnly: true);
    }

    public WorkNotification Publish(string recipient, WorkNotificationKind kind, string title, string content, string? href, string dedupeKey, DateTime? createdAt = null)
    {
        EnsureRecipient(recipient);
        var normalizedRecipient = NormalizeRecipient(recipient);
        var normalizedKey = dedupeKey.Trim();
        var notification = new WorkNotification(normalizedRecipient, kind, title, content, href, normalizedKey, createdAt);
        try
        {
            if (!repository.TryAdd(notification))
                return repository.FindByDedupeKey(normalizedRecipient, normalizedKey)
                    ?? throw new InvalidOperationException("通知原子写入未返回胜出记录。");
            EnqueueExternalAfterCommit(notification);
        }
        catch (Exception ex)
        {
            try
            {
                var concurrent = repository.FindByDedupeKey(normalizedRecipient, normalizedKey);
                if (concurrent is not null) return concurrent;
            }
            catch
            {
                // 继续交给失败记录边界处理。
            }
            RecordFailure("publish", normalizedRecipient, normalizedKey, ex, NotificationDeliveryPayload.From(notification));
        }
        return notification;
    }

    public void MarkRead(string recipient, Guid notificationId, DateTime? readAt = null)
    {
        EnsureRecipient(recipient);
        var notification = List(recipient).FirstOrDefault(x => x.Id == notificationId) ?? throw new InvalidOperationException("通知不存在或无权访问。");
        notification.MarkRead(readAt);
        repository.Update(notification);
    }

    public void MarkReadByDedupeKey(string recipient, string dedupeKey, DateTime? readAt = null)
    {
        EnsureRecipient(recipient);
        try
        {
            var notification = repository.FindByDedupeKey(NormalizeRecipient(recipient), dedupeKey.Trim());
            if (notification is null) return;
            notification.MarkRead(readAt);
            repository.Update(notification);
        }
        catch (Exception ex)
        {
            RecordFailure("mark-read", NormalizeRecipient(recipient), dedupeKey.Trim(), ex);
        }
    }

    private void RecordFailure(string operation, string recipient, string dedupeKey, Exception exception, NotificationDeliveryPayload? payload = null)
    {
        if (failureRecorder is null) return;
        var failure = new NotificationDeliveryFailure(operation, recipient, dedupeKey, exception.Message, DateTime.Now, payload);
        void PersistFailure()
        {
            try { failureRecorder.Record(failure); }
            catch { /* 失败记录不能覆盖通知主流程，也不能反向阻断已提交事务。 */ }
        }
        try
        {
            if (transactions is null) PersistFailure();
            else transactions.Execute(static () => { }, afterRollback: null, afterCommit: PersistFailure);
        }
        catch
        {
            // 外部未由 Workflow 管理的事务不能安全登记提交回调；此时宁可丢弃辅助记录，
            // 也不能直接写入可能已标记回滚的主连接。
        }
    }

    private void EnqueueExternalAfterCommit(WorkNotification notification)
    {
        if (externalOutbox is null) return;
        void Enqueue()
        {
            try { externalOutbox.Enqueue(notification); }
            catch { /* 站外 Outbox 失败不能阻断站内通知和业务提交。 */ }
        }
        try
        {
            if (transactions is null) Enqueue();
            else transactions.Execute(static () => { }, afterRollback: null, afterCommit: Enqueue);
        }
        catch
        {
            // 外部事务不支持安全登记提交回调时，宁可不入队，也不能污染业务提交边界。
        }
    }

    public int MarkAllRead(string recipient, DateTime? readAt = null)
    {
        EnsureRecipient(recipient);
        var unread = List(recipient, unreadOnly: true);
        foreach (var notification in unread)
        {
            notification.MarkRead(readAt);
            repository.Update(notification);
        }
        return unread.Count;
    }

    public NotificationPage ListPage(string recipient, int pageIndex = 1, int pageSize = 20, bool unreadOnly = false)
    {
        EnsureRecipient(recipient);
        if (pageIndex < 1) throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));

        return repository.ListPage(NormalizeRecipient(recipient), pageIndex, pageSize, unreadOnly);
    }

    public void Delete(string recipient, Guid notificationId)
    {
        EnsureRecipient(recipient);
        var normalizedRecipient = NormalizeRecipient(recipient);
        if (List(normalizedRecipient).All(x => x.Id != notificationId))
            throw new InvalidOperationException("通知不存在或无权访问。");

        if (repository.Delete(normalizedRecipient, [notificationId]) != 1)
            throw new InvalidOperationException("通知不存在或已被删除。");
    }

    public int DeleteMany(string recipient, IEnumerable<Guid> notificationIds)
    {
        EnsureRecipient(recipient);
        var requestedIds = notificationIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (requestedIds.Length == 0) return 0;

        var normalizedRecipient = NormalizeRecipient(recipient);
        var visibleIds = List(normalizedRecipient)
            .Where(x => requestedIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToArray();
        return repository.Delete(normalizedRecipient, visibleIds);
    }

    private static void EnsureRecipient(string recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient)) throw new ArgumentException("通知接收人不能为空。", nameof(recipient));
    }

    private static string NormalizeRecipient(string recipient) => recipient.Trim().ToLowerInvariant();
}

public sealed record NotificationPage(
    IReadOnlyList<WorkNotification> Items,
    int TotalCount,
    int PageIndex,
    int PageSize,
    int PageCount);
