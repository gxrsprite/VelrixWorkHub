namespace VelrixWorkHub.Application.Workflow;

/// <summary>
/// Workflow 主交易边界。基础设施层负责把它绑定到具体数据库事务，
/// Application 层只声明动作、运行态和审计必须处于同一提交边界。
/// </summary>
public interface IWorkflowTransactionBoundary
{
    /// <param name="afterRollback">事务回滚后执行的副作用，例如保留失败审计。</param>
    void Execute(Action operation, Action<Exception>? afterRollback = null);

    /// <summary>在事务提交后执行不应污染主交易的副作用，例如待办通知。</summary>
    void Execute(Action operation, Action<Exception>? afterRollback, Action? afterCommit)
    {
        Execute(operation, afterRollback);
        afterCommit?.Invoke();
    }
}
