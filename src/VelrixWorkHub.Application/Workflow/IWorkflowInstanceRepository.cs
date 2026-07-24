using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

public interface IWorkflowInstanceRepository
{
    IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null);
    void Add(WorkflowInstance instance);

    /// <summary>以运行实例唯一业务键执行原子插入；竞争失败返回 false，不得用先查后写代替。</summary>
    bool TryAdd(WorkflowInstance instance);

    void Update(WorkflowInstance instance);

    /// <summary>以实例当前 Revision 作为期望版本执行原子更新；所有持久化适配器必须显式提供 CAS 结果。</summary>
    bool TryUpdate(WorkflowInstance instance);
}

/// <summary>
/// 可选的数据库行锁能力。重试等可能调用外部动作的流程操作，必须在动作执行前锁定实例行，
/// 避免终态操作已经提交后仍执行一次无法提交的外部动作。
/// </summary>
public interface IWorkflowInstanceLockRepository
{
    void LockForUpdate(WorkflowInstance instance);
}
