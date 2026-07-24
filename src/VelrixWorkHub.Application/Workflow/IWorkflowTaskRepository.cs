using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

public interface IWorkflowTaskRepository
{
    IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null);
    void Add(WorkflowTask task);
    /// <summary>以稳定待办主键执行原子插入；已存在时返回 false，不得用先查后写代替。</summary>
    bool TryAdd(WorkflowTask task);
    void Update(WorkflowTask task);

    /// <summary>以待办当前 Revision 为期望版本执行原子更新；所有持久化适配器必须显式提供 CAS 结果。</summary>
    bool TryUpdate(WorkflowTask task);
}
