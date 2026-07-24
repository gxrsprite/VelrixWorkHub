namespace VelrixWorkHub.Application.Workflow;

/// <summary>
/// 为无数据库事务的宿主提供待办创建补偿；真实数据库优先依赖外层事务回滚。
/// </summary>
public interface IWorkflowTaskCompensationRepository
{
    void Remove(Guid taskId);
}
