namespace VelrixWorkHub.Domain;

public enum PmpProjectWorkItemActivityType { Created, StatusChanged, Commented }

public sealed class PmpProjectWorkItemActivity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid WorkItemId { get; private set; }
    public PmpProjectWorkItemActivityType Type { get; private set; }
    public string? Content { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public PmpProjectWorkItemStatus? PreviousStatus { get; private set; }
    public PmpProjectWorkItemStatus? CurrentStatus { get; private set; }
    public DateTime OccurredAt { get; private set; }

    public PmpProjectWorkItemActivity(Guid workItemId, PmpProjectWorkItemActivityType type, string? content, string actorName, PmpProjectWorkItemStatus? previousStatus, PmpProjectWorkItemStatus? currentStatus, DateTime occurredAt)
    {
        if (workItemId == Guid.Empty) throw new ArgumentException("必须关联工作项。", nameof(workItemId));
        if (string.IsNullOrWhiteSpace(actorName)) throw new ArgumentException("操作者不能为空。", nameof(actorName));
        if (type == PmpProjectWorkItemActivityType.Commented && string.IsNullOrWhiteSpace(content)) throw new ArgumentException("批注内容不能为空。", nameof(content));
        if (type == PmpProjectWorkItemActivityType.StatusChanged && (previousStatus is null || currentStatus is null)) throw new ArgumentException("状态变更必须记录变更前后状态。", nameof(currentStatus));
        WorkItemId = workItemId; Type = type; Content = Clean(content); ActorName = actorName.Trim(); PreviousStatus = previousStatus; CurrentStatus = currentStatus; OccurredAt = occurredAt;
    }

    public static PmpProjectWorkItemActivity Restore(Guid id, Guid workItemId, PmpProjectWorkItemActivityType type, string? content, string actorName, PmpProjectWorkItemStatus? previousStatus, PmpProjectWorkItemStatus? currentStatus, DateTime occurredAt)
        => new(workItemId, type, content, actorName, previousStatus, currentStatus, occurredAt) { Id = id };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
