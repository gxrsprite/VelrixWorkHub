using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

public interface IWorkflowOperationRepository
{
    IReadOnlyList<WorkflowOperation> List(Guid? instanceId = null, string? businessType = null, Guid? businessId = null, WorkflowOperationKind? kind = null);
    WorkflowOperation? FindByDedupeKey(string dedupeKey);
    void Add(WorkflowOperation operation);
    /// <summary>以稳定 DedupeKey 原子插入；已存在时返回 false，不得用先查后写代替。</summary>
    bool TryAdd(WorkflowOperation operation);
}
