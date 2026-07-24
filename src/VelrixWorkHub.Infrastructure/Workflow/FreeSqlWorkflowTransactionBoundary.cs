using FreeSql;
using System.Runtime.CompilerServices;
using VelrixWorkHub.Application.Workflow;

namespace VelrixWorkHub.Infrastructure.Workflow;

public sealed class FreeSqlWorkflowTransactionBoundary(IFreeSql fsql) : IWorkflowTransactionBoundary
{
    // 同一 FreeSql 连接可能由多个 Application 服务分别构造边界实例；
    // 回调注册表必须按连接共享，否则嵌套服务会误判当前事务是外部事务。
    private static readonly ConditionalWeakTable<IFreeSql, CallbackState> callbackStates = new();
    private readonly CallbackState callbackState = callbackStates.GetValue(fsql, static _ => new CallbackState());

    private sealed class CallbackState
    {
        public AsyncLocal<List<Action<Exception>>?> RollbackCallbacks { get; } = new();
        public AsyncLocal<List<Action>?> CommitCallbacks { get; } = new();
    }

    public void Execute(Action operation, Action<Exception>? afterRollback = null)
        => Execute(operation, afterRollback, null);

    public void Execute(Action operation, Action<Exception>? afterRollback, Action? afterCommit)
    {
        ArgumentNullException.ThrowIfNull(operation);
        // 审批决策本身也可能在同一边界内调用运行时；FreeSql 的当前线程事务已经存在时，
        // 直接加入它，避免嵌套事务把一个审批拆成多个提交。
        if (fsql.Ado.TransactionCurrentThread is not null)
        {
            if ((afterRollback is not null || afterCommit is not null)
                && (callbackState.RollbackCallbacks.Value is null || callbackState.CommitCallbacks.Value is null))
                throw new InvalidOperationException("当前 FreeSql 事务不是由 Workflow 事务边界管理，无法安全登记提交或回滚回调。");
            if (afterRollback is not null && callbackState.RollbackCallbacks.Value is not null)
                callbackState.RollbackCallbacks.Value.Add(afterRollback);
            if (afterCommit is not null && callbackState.CommitCallbacks.Value is not null)
                callbackState.CommitCallbacks.Value.Add(afterCommit);
            else if (afterCommit is not null)
                afterCommit();
            operation();
            return;
        }

        var previousCallbacks = callbackState.RollbackCallbacks.Value;
        var previousCommitCallbacks = callbackState.CommitCallbacks.Value;
        var currentCallbacks = new List<Action<Exception>>();
        var currentCommitCallbacks = new List<Action>();
        if (afterRollback is not null) currentCallbacks.Add(afterRollback);
        if (afterCommit is not null) currentCommitCallbacks.Add(afterCommit);
        callbackState.RollbackCallbacks.Value = currentCallbacks;
        callbackState.CommitCallbacks.Value = currentCommitCallbacks;
        Action[] committedCallbacks = [];
        try
        {
            fsql.Transaction(operation);
            committedCallbacks = currentCommitCallbacks.ToArray();
        }
        catch (Exception exception)
        {
            // 内层节点先恢复，外层审批事务最后恢复，避免回滚后的内存快照停在中间节点。
            foreach (var callback in currentCallbacks.AsEnumerable().Reverse().ToArray())
            {
                try { callback(exception); }
                catch { /* 回滚后的失败审计不能覆盖原始异常。 */ }
            }
            throw;
        }
        finally
        {
            callbackState.RollbackCallbacks.Value = previousCallbacks;
            callbackState.CommitCallbacks.Value = previousCommitCallbacks;
        }

        foreach (var callback in committedCallbacks)
        {
            try
            {
                callback();
            }
            catch (Exception exception)
            {
                // 主事务已经提交，后置副作用失败不能反向阻断已提交业务，
                // 也不能阻断同一事务中其他通知/锁释放回调；保留 Trace 供宿主日志接管。
                System.Diagnostics.Trace.TraceError("Workflow 提交后回调失败：{0}", exception);
            }
        }
    }
}
