using System.Security.Cryptography;
using System.Text;

namespace VelrixWorkHub.Domain;

public enum WorkflowTaskStatus { Pending, Approved, Rejected, Cancelled, Transferred, Returned }

public sealed class WorkflowTask
{
    /// <summary>
    /// 待办由实例、节点、轮次和审批人确定。稳定标识使并发补偿可借助主键安全地幂等写入。
    /// </summary>
    public Guid Id { get; init; }
    public Guid InstanceId { get; }
    public Guid DefinitionId { get; }
    public int DefinitionVersion { get; }
    public Guid NodeId { get; }
    public string NodeName { get; }
    public string BusinessType { get; }
    public Guid BusinessId { get; }
    public string Assignee { get; }
    /// <summary>同一节点因回退再次进入时递增，历史待办不被覆盖。</summary>
    public int Round { get; }
    public WorkflowTaskStatus Status { get; private set; } = WorkflowTaskStatus.Pending;
    /// <summary>持久化版本号，用于审批预占和结果提交的乐观并发控制。</summary>
    public long Revision { get; private set; } = 1;
    public string? TransferTarget { get; private set; }
    public string? DecisionComment { get; private set; }
    public string? DecisionActor { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? CompletedAt { get; private set; }

    public WorkflowTask(WorkflowInstance instance, Guid nodeId, string nodeName, string assignee, DateTime? createdAt = null, int round = 1)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.Status != WorkflowInstanceStatus.Running) throw new InvalidOperationException("只有运行中的流程实例可以创建审批待办。");
        if (nodeId == Guid.Empty) throw new ArgumentException("审批节点不能为空。", nameof(nodeId));
        if (string.IsNullOrWhiteSpace(nodeName)) throw new ArgumentException("审批节点名称不能为空。", nameof(nodeName));
        if (string.IsNullOrWhiteSpace(assignee)) throw new ArgumentException("审批人不能为空。", nameof(assignee));
        if (nodeName.Trim().Length > 200 || assignee.Trim().Length > 200) throw new ArgumentException("审批节点名称和审批人不能超过 200 个字符。");
        if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
        if (instance.BusinessType.Trim().Length > 100) throw new ArgumentException("业务对象类型不能超过 100 个字符。", nameof(instance));
        Id = CreateId(instance.Id, nodeId, round, assignee);
        InstanceId = instance.Id;
        DefinitionId = instance.DefinitionId;
        DefinitionVersion = instance.DefinitionVersion;
        NodeId = nodeId;
        NodeName = nodeName.Trim();
        BusinessType = instance.BusinessType;
        BusinessId = instance.BusinessId;
        Assignee = assignee.Trim();
        Round = round;
        CreatedAt = createdAt ?? DateTime.Now;
    }

    private static Guid CreateId(Guid instanceId, Guid nodeId, int round, string assignee)
    {
        var source = $"{instanceId:N}:{nodeId:N}:{round}:{assignee.Trim().ToUpperInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return new Guid(hash.AsSpan(0, 16));
    }

    private WorkflowTask(Guid id, Guid instanceId, Guid definitionId, int definitionVersion, Guid nodeId, string nodeName, string businessType, Guid businessId, string assignee, int round, WorkflowTaskStatus status, string? transferTarget, string? decisionComment, string? decisionActor, DateTime createdAt, DateTime? completedAt, long revision)
    {
        Id = id; InstanceId = instanceId; DefinitionId = definitionId; DefinitionVersion = definitionVersion; NodeId = nodeId; NodeName = nodeName; BusinessType = businessType; BusinessId = businessId; Assignee = assignee; Round = round; Status = status; Revision = revision; TransferTarget = transferTarget; DecisionComment = decisionComment; DecisionActor = decisionActor; CreatedAt = createdAt; CompletedAt = completedAt;
    }

    public static WorkflowTask Rehydrate(Guid id, Guid instanceId, Guid definitionId, int definitionVersion, Guid nodeId, string nodeName, string businessType, Guid businessId, string assignee, WorkflowTaskStatus status, string? decisionComment, string? decisionActor, DateTime createdAt, DateTime? completedAt, string? transferTarget = null, long revision = 1, int round = 1)
    {
        if (id == Guid.Empty || instanceId == Guid.Empty || definitionId == Guid.Empty || nodeId == Guid.Empty || businessId == Guid.Empty) throw new ArgumentException("审批待办标识不能为空。");
        if (definitionVersion < 1 || string.IsNullOrWhiteSpace(nodeName) || string.IsNullOrWhiteSpace(businessType) || string.IsNullOrWhiteSpace(assignee)) throw new ArgumentException("审批待办持久化数据不完整。");
        if (nodeName.Trim().Length > 200 || businessType.Trim().Length > 100 || assignee.Trim().Length > 200) throw new ArgumentException("审批待办字段超出持久化长度。");
        if (decisionComment?.Length > 2000) throw new ArgumentException("审批意见不能超过 2000 个字符。", nameof(decisionComment));
        if (decisionActor?.Length > 200) throw new ArgumentException("审批操作人不能超过 200 个字符。", nameof(decisionActor));
        if (transferTarget?.Length > 200) throw new ArgumentException("转交目标不能超过 200 个字符。", nameof(transferTarget));
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (status == WorkflowTaskStatus.Pending && completedAt is not null) throw new ArgumentException("待办不能有完成时间。");
        if (status != WorkflowTaskStatus.Pending && completedAt is null) throw new ArgumentException("已处理待办必须有完成时间。");
        if (status == WorkflowTaskStatus.Pending && (decisionActor is not null || decisionComment is not null)) throw new ArgumentException("待办不能有处理结果。");
        if (status != WorkflowTaskStatus.Pending && string.IsNullOrWhiteSpace(decisionActor)) throw new ArgumentException("已处理待办必须有审批人。");
        if (status == WorkflowTaskStatus.Transferred && string.IsNullOrWhiteSpace(transferTarget)) throw new ArgumentException("已转交待办必须有转交目标。", nameof(transferTarget));
        if (status != WorkflowTaskStatus.Transferred && !string.IsNullOrWhiteSpace(transferTarget)) throw new ArgumentException("只有已转交待办可以有转交目标。", nameof(transferTarget));
        return new WorkflowTask(id, instanceId, definitionId, definitionVersion, nodeId, nodeName.Trim(), businessType.Trim(), businessId, assignee.Trim(), round, status, transferTarget?.Trim(), decisionComment, decisionActor, createdAt, completedAt, revision);
    }

    /// <summary>由仓储在 CAS 成功后推进版本号。</summary>
    public void MarkPersistedRevision(long revision)
    {
        if (revision != Revision + 1) throw new InvalidOperationException("审批待办版本号必须连续递增。");
        Revision = revision;
    }

    /// <summary>最终提交 CAS 失败时恢复本次未提交的内存状态。</summary>
    public void RestorePersistedState(WorkflowTaskStatus status, string? transferTarget, string? decisionComment, string? decisionActor, DateTime? completedAt, long revision)
    {
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        Status = status;
        TransferTarget = transferTarget;
        DecisionComment = decisionComment;
        DecisionActor = decisionActor;
        CompletedAt = completedAt;
        Revision = revision;
    }

    public void Approve(string actor, string? comment = null, DateTime? completedAt = null) => Decide(WorkflowTaskStatus.Approved, actor, comment, completedAt);
    public void Reject(string actor, string? comment = null, DateTime? completedAt = null) => Decide(WorkflowTaskStatus.Rejected, actor, comment, completedAt);
    public void Cancel(string actor, string? comment = null, DateTime? completedAt = null) => Decide(WorkflowTaskStatus.Cancelled, actor, comment, completedAt);
    public void Return(string actor, string? comment = null, DateTime? completedAt = null) => Decide(WorkflowTaskStatus.Returned, actor, comment, completedAt);

    public void Transfer(string actor, string targetAssignee, string? comment = null, DateTime? completedAt = null)
    {
        ValidateDecision(actor, comment);
        if (string.IsNullOrWhiteSpace(targetAssignee)) throw new ArgumentException("转交目标不能为空。", nameof(targetAssignee));
        var normalizedTarget = targetAssignee.Trim();
        if (normalizedTarget.Length > 200) throw new ArgumentException("转交目标不能超过 200 个字符。", nameof(targetAssignee));
        if (Assignee.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("转交目标不能是当前审批人。");
        Status = WorkflowTaskStatus.Transferred;
        TransferTarget = normalizedTarget;
        DecisionActor = actor.Trim();
        DecisionComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        CompletedAt = completedAt ?? DateTime.Now;
    }

    public void ValidateDecision(string actor, string? comment = null)
    {
        if (Status != WorkflowTaskStatus.Pending) throw new InvalidOperationException("已处理审批待办不能重复操作。");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("审批操作人不能为空。", nameof(actor));
        if (actor.Trim().Length > 200) throw new ArgumentException("审批操作人不能超过 200 个字符。", nameof(actor));
        if (!string.IsNullOrWhiteSpace(comment) && comment.Trim().Length > 2000) throw new ArgumentException("审批意见不能超过 2000 个字符。", nameof(comment));
    }

    private void Decide(WorkflowTaskStatus status, string actor, string? comment, DateTime? completedAt)
    {
        ValidateDecision(actor, comment);
        var normalizedActor = actor.Trim();
        var normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        Status = status;
        TransferTarget = null;
        DecisionActor = normalizedActor;
        DecisionComment = normalizedComment;
        CompletedAt = completedAt ?? DateTime.Now;
    }
}
