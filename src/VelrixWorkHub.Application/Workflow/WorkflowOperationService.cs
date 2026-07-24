using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

public sealed class WorkflowOperationService(IWorkflowOperationRepository repository)
{
    public IReadOnlyList<WorkflowOperation> List(Guid? instanceId = null, string? businessType = null, Guid? businessId = null, WorkflowOperationKind? kind = null)
        => repository.List(instanceId, businessType, businessId, kind).OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToArray();

    public bool Has(Guid instanceId, WorkflowOperationKind kind, Guid nodeId)
        => repository.List(instanceId, kind: kind).Any(x => x.NodeId == nodeId);

    public bool Exists(string dedupeKey) => repository.FindByDedupeKey(dedupeKey.Trim()) is not null;

    public WorkflowOperation Record(WorkflowInstance instance, WorkflowOperationKind kind, string actor, string? comment, string dedupeKey, Guid? taskId = null, Guid? nodeId = null, string? targetAssignee = null, DateTime? occurredAt = null)
        => Record(instance.Id, taskId, nodeId, instance.BusinessType, instance.BusinessId, kind, actor, targetAssignee, comment, dedupeKey, occurredAt);

    public WorkflowOperation Record(WorkflowTask task, WorkflowOperationKind kind, string actor, string? comment, string dedupeKey, string? targetAssignee = null, DateTime? occurredAt = null)
        => Record(task.InstanceId, task.Id, task.NodeId, task.BusinessType, task.BusinessId, kind, actor, targetAssignee, comment, dedupeKey, occurredAt);

    public bool TryRecord(WorkflowInstance instance, WorkflowOperationKind kind, string actor, string? comment, string dedupeKey, out WorkflowOperation operation, Guid? taskId = null, Guid? nodeId = null, string? targetAssignee = null, DateTime? occurredAt = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return TryRecord(instance.Id, taskId, nodeId, instance.BusinessType, instance.BusinessId, kind, actor, targetAssignee, comment, dedupeKey, occurredAt, out operation);
    }

    private WorkflowOperation Record(Guid instanceId, Guid? taskId, Guid? nodeId, string businessType, Guid businessId, WorkflowOperationKind kind, string actor, string? targetAssignee, string? comment, string dedupeKey, DateTime? occurredAt)
    {
        TryRecord(instanceId, taskId, nodeId, businessType, businessId, kind, actor, targetAssignee, comment, dedupeKey, occurredAt, out var operation);
        return operation;
    }

    private bool TryRecord(Guid instanceId, Guid? taskId, Guid? nodeId, string businessType, Guid businessId, WorkflowOperationKind kind, string actor, string? targetAssignee, string? comment, string dedupeKey, DateTime? occurredAt, out WorkflowOperation operation)
    {
        var normalizedKey = dedupeKey.Trim();
        var candidate = new WorkflowOperation(instanceId, taskId, nodeId, businessType, businessId, kind, actor, targetAssignee, comment, normalizedKey, occurredAt);
        if (repository.TryAdd(candidate))
        {
            operation = candidate;
            return true;
        }
        var concurrent = repository.FindByDedupeKey(normalizedKey)
            ?? throw new InvalidOperationException("操作历史原子写入未返回胜出记录。");
        operation = concurrent;
        return false;
    }
}
