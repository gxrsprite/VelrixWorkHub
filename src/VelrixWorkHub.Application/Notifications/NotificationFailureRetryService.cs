using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Workflow;

namespace VelrixWorkHub.Application.Notifications;

public sealed record NotificationFailureRetrySummary(int PendingCount, int HighRetryCount, int MaxRetryCount);
public sealed record NotificationFailureBatchRetryResult(int RequestedCount, int ResolvedCount, int FailedCount);

/// <summary>消费持久化的发布失败记录；调度频率和人工处置由宿主决定。</summary>
public sealed class NotificationFailureRetryService(
    INotificationRepository notifications,
    INotificationFailureRepository failures,
    INotificationFailureAuditRepository? audits = null,
    IWorkflowTransactionBoundary? transactions = null)
{
    private static readonly TimeSpan RetryLease = TimeSpan.FromMinutes(5);

    public NotificationFailureRetrySummary InspectPending(int take = 500, int alertRetryThreshold = 3)
    {
        if (take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(take));
        if (alertRetryThreshold < 1) throw new ArgumentOutOfRangeException(nameof(alertRetryThreshold));
        var pending = failures.ListPending(take);
        return new(
            pending.Count,
            pending.Count(x => x.RetryCount >= alertRetryThreshold),
            pending.Select(x => x.RetryCount).DefaultIfEmpty(0).Max());
    }

    public int RetryPending(int take = 50, DateTime? attemptedAt = null)
    {
        if (take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(take));
        var now = attemptedAt ?? DateTime.Now;
        var resolved = 0;
        foreach (var failure in failures.ListPending(take))
            if (Retry(failure, now)) resolved++;
        return resolved;
    }

    public bool Retry(Guid failureId, DateTime? attemptedAt = null, string? actor = null)
    {
        if (failureId == Guid.Empty) throw new ArgumentException("通知失败记录标识不能为空。", nameof(failureId));
        var attempted = attemptedAt ?? DateTime.Now;
        var failure = failures.FindPending(failureId);
        var resolved = failure is not null && Retry(failure, attempted);
        if (!string.IsNullOrWhiteSpace(actor) && failure is not null) RecordManualAudit(failure.Id, resolved, actor, attempted);
        return resolved;
    }

    public NotificationFailureBatchRetryResult RetryMany(IEnumerable<Guid> failureIds, string actor, DateTime? attemptedAt = null)
    {
        ArgumentNullException.ThrowIfNull(failureIds);
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("批量重试必须提供操作者。", nameof(actor));
        if (actor.Trim().Length > 200) throw new ArgumentException("操作者不能超过 200 个字符。", nameof(actor));
        var ids = failureIds.Distinct().ToArray();
        if (ids.Length is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(failureIds), "批量重试一次只能选择 1 到 50 条失败记录。");
        if (ids.Any(id => id == Guid.Empty)) throw new ArgumentException("批量重试标识不能包含空值。", nameof(failureIds));

        var now = attemptedAt ?? DateTime.Now;
        var resolved = ids.Count(id => Retry(id, now, actor));
        return new(ids.Length, resolved, ids.Length - resolved);
    }

    private void RecordManualAudit(Guid failureId, bool resolved, string actor, DateTime occurredAt)
    {
        if (audits is null) return;
        try
        {
            audits.Add(new NotificationFailureAuditEntry(
                failureId,
                resolved ? "ManualRetrySucceeded" : "ManualRetryFailed",
                actor.Trim(),
                resolved ? "管理员手动重试成功" : "管理员手动重试失败或记录已处理",
                occurredAt));
        }
        catch
        {
            // 处置审计失败不能反向阻断通知补投；后台日志由宿主负责记录。
        }
    }

    private bool Retry(PersistedNotificationFailure failure, DateTime attemptedAt)
    {
        if (!failures.TryClaim(failure.Id, attemptedAt, RetryLease)) return false;
        var createdNotificationId = Guid.Empty;
        var createdNotification = false;
        try
        {
            ExecuteTransaction(() =>
            {
                var payload = failure.Payload;
                var notification = new WorkNotification(payload.Recipient, payload.Kind, payload.Title, payload.Content, payload.Href, payload.DedupeKey, payload.CreatedAt);
                createdNotificationId = notification.Id;
                createdNotification = notifications.TryAdd(notification);
                failures.MarkResolved(failure.Id, attemptedAt);
            });
            return true;
        }
        catch (Exception exception)
        {
            if (createdNotification)
            {
                try { notifications.Delete(failure.Payload.Recipient, [createdNotificationId]); }
                catch { /* 主交易异常优先；真实数据库通常已回滚，内存宿主补偿失败也不能覆盖原异常。 */ }
            }
            try { failures.MarkRetryFailed(failure.Id, TrimError(exception.Message), attemptedAt); }
            catch
            {
                // 另一执行者可能已经解决或更新了同一条失败记录；不覆盖其状态，也不中断剩余批次。
            }
            return false;
        }
    }

    private static string TrimError(string error) => error.Length <= 2000 ? error : error[..2000];

    private void ExecuteTransaction(Action operation)
    {
        if (transactions is null) operation();
        else transactions.Execute(operation);
    }
}
