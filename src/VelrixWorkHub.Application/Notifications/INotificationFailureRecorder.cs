using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Notifications;

public sealed record NotificationDeliveryFailure(
    string Operation,
    string Recipient,
    string DedupeKey,
    string Error,
    DateTime OccurredAt,
    NotificationDeliveryPayload? Payload = null);

/// <summary>通知写入失败后保留的可重放负载；不携带通知已读状态等运行态。</summary>
public sealed record NotificationDeliveryPayload(
    string Recipient,
    WorkNotificationKind Kind,
    string Title,
    string Content,
    string? Href,
    string DedupeKey,
    DateTime CreatedAt)
{
    public static NotificationDeliveryPayload From(WorkNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return new(notification.Recipient, notification.Kind, notification.Title, notification.Content, notification.Href, notification.DedupeKey, notification.CreatedAt);
    }
}

/// <summary>
/// 为通知失败保留记录、重试和审计的替换边界；失败记录本身不能反向阻断主交易。
/// </summary>
public interface INotificationFailureRecorder
{
    void Record(NotificationDeliveryFailure failure);
}

public sealed class InMemoryNotificationFailureRecorder : INotificationFailureRecorder
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<NotificationDeliveryFailure> failures = new();

    public IReadOnlyList<NotificationDeliveryFailure> List() => failures.ToArray();

    public void Record(NotificationDeliveryFailure failure) => failures.Enqueue(failure);
}

public enum NotificationFailureStatus
{
    Pending,
    Resolved,
    InvalidPayload
}

public sealed record PersistedNotificationFailure(
    Guid Id,
    string Operation,
    string Recipient,
    string DedupeKey,
    string Error,
    DateTime OccurredAt,
    NotificationDeliveryPayload Payload,
    NotificationFailureStatus Status,
    int RetryCount,
    DateTime? LastRetryAt,
    DateTime? ResolvedAt);

public interface INotificationFailureRepository
{
    IReadOnlyList<PersistedNotificationFailure> ListPending(int take);
    PersistedNotificationFailure? FindPending(Guid id);
    /// <summary>必须以 Pending 状态和最近重试时间执行数据库原子更新，抢占一次补投租约。</summary>
    bool TryClaim(Guid id, DateTime attemptedAt, TimeSpan lease);
    void MarkRetryFailed(Guid id, string error, DateTime attemptedAt);
    void MarkResolved(Guid id, DateTime resolvedAt);
}
