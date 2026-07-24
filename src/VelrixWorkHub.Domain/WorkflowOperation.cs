using System.Security.Cryptography;
using System.Text;

namespace VelrixWorkHub.Domain;

public enum WorkflowOperationKind
{
    Started,
    Assigned,
    Approved,
    Rejected,
    Cancelled,
    Returned,
    Transferred,
    Withdrawn,
    Resubmitted,
    NodeCompleted,
    NodeEntered,
    NodeExecuted,
    NodeFailed,
    Retried
}

/// <summary>
/// 流程实例的不可变操作历史。任务只保存当前结果，完整时间线由该记录承载。
/// </summary>
public sealed class WorkflowOperation
{
    /// <summary>
    /// 操作去重键是不可变时间线的业务幂等键，使用其稳定派生主键可避免并发唯一键冲突污染主事务。
    /// </summary>
    public Guid Id { get; init; }
    public Guid InstanceId { get; }
    public Guid? TaskId { get; }
    public Guid? NodeId { get; }
    public string BusinessType { get; }
    public Guid BusinessId { get; }
    public WorkflowOperationKind Kind { get; }
    public string Actor { get; }
    public string? TargetAssignee { get; }
    public string? Comment { get; }
    public string DedupeKey { get; }
    public DateTime OccurredAt { get; }

    public WorkflowOperation(Guid instanceId, Guid? taskId, Guid? nodeId, string businessType, Guid businessId, WorkflowOperationKind kind, string actor, string? targetAssignee, string? comment, string dedupeKey, DateTime? occurredAt = null)
    {
        Validate(instanceId, taskId, nodeId, businessType, businessId, kind, actor, targetAssignee, comment, dedupeKey);
        Id = CreateId(dedupeKey);
        InstanceId = instanceId;
        TaskId = taskId is Guid task && task != Guid.Empty ? task : null;
        NodeId = nodeId is Guid node && node != Guid.Empty ? node : null;
        BusinessType = businessType.Trim();
        BusinessId = businessId;
        Kind = kind;
        Actor = actor.Trim();
        TargetAssignee = string.IsNullOrWhiteSpace(targetAssignee) ? null : targetAssignee.Trim();
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        DedupeKey = dedupeKey.Trim();
        OccurredAt = occurredAt ?? DateTime.Now;
    }

    private static Guid CreateId(string dedupeKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(dedupeKey.Trim()));
        return new Guid(hash.AsSpan(0, 16));
    }

    private WorkflowOperation(Guid id, Guid instanceId, Guid? taskId, Guid? nodeId, string businessType, Guid businessId, WorkflowOperationKind kind, string actor, string? targetAssignee, string? comment, string dedupeKey, DateTime occurredAt)
    {
        Id = id;
        InstanceId = instanceId;
        TaskId = taskId;
        NodeId = nodeId;
        BusinessType = businessType;
        BusinessId = businessId;
        Kind = kind;
        Actor = actor;
        TargetAssignee = targetAssignee;
        Comment = comment;
        DedupeKey = dedupeKey;
        OccurredAt = occurredAt;
    }

    public static WorkflowOperation Rehydrate(Guid id, Guid instanceId, Guid? taskId, Guid? nodeId, string businessType, Guid businessId, WorkflowOperationKind kind, string actor, string? targetAssignee, string? comment, string dedupeKey, DateTime occurredAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("流程操作记录标识不能为空。", nameof(id));
        Validate(instanceId, taskId, nodeId, businessType, businessId, kind, actor, targetAssignee, comment, dedupeKey);
        return new WorkflowOperation(id, instanceId, taskId is Guid task && task != Guid.Empty ? task : null, nodeId is Guid node && node != Guid.Empty ? node : null, businessType.Trim(), businessId, kind, actor.Trim(), string.IsNullOrWhiteSpace(targetAssignee) ? null : targetAssignee.Trim(), string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(), dedupeKey.Trim(), occurredAt);
    }

    private static void Validate(Guid instanceId, Guid? taskId, Guid? nodeId, string businessType, Guid businessId, WorkflowOperationKind kind, string actor, string? targetAssignee, string? comment, string dedupeKey)
    {
        if (instanceId == Guid.Empty || (taskId is Guid task && task == Guid.Empty) || (nodeId is Guid node && node == Guid.Empty) || businessId == Guid.Empty)
            throw new ArgumentException("流程操作记录标识不能为空。");
        if (string.IsNullOrWhiteSpace(businessType) || businessType.Trim().Length > 100) throw new ArgumentException("业务对象类型不能为空且不能超过 100 个字符。", nameof(businessType));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(actor) || actor.Trim().Length > 200) throw new ArgumentException("流程操作人不能为空且不能超过 200 个字符。", nameof(actor));
        if (targetAssignee?.Trim().Length > 200) throw new ArgumentException("目标审批人不能超过 200 个字符。", nameof(targetAssignee));
        if (comment?.Trim().Length > 2000) throw new ArgumentException("操作意见不能超过 2000 个字符。", nameof(comment));
        if (string.IsNullOrWhiteSpace(dedupeKey) || dedupeKey.Trim().Length > 200) throw new ArgumentException("流程操作去重键不能为空且不能超过 200 个字符。", nameof(dedupeKey));
    }
}
