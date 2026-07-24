using System.Collections.Concurrent;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

public enum WorkflowRuntimeState
{
    WaitingForApproval,
    WaitingForCondition,
    Completed
}

public sealed record WorkflowRuntimeResult(WorkflowRuntimeState State, Guid CurrentNodeId);

/// <summary>
/// 统一驱动流程图中的自动节点。每次调用都从实例当前快照继续，因此可用于启动、审批后续和失败重试。
/// </summary>
public sealed class WorkflowRuntimeService(
    WorkflowInstanceService instances,
    WorkflowActionExecutor actions,
    NotificationService notifications,
    WorkflowOperationService? operations = null,
    IWorkflowTransactionBoundary? transactions = null)
{
    private readonly ConcurrentDictionary<Guid, object> instanceLocks = new();

    internal void ReleaseTerminalInstanceLock(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.Status != WorkflowInstanceStatus.Running)
            instanceLocks.TryRemove(instance.Id, out _);
    }

    public WorkflowRuntimeResult Continue(WorkflowInstance instance, IReadOnlyDictionary<string, object?>? fields = null, DateTime? occurredAt = null, Guid? preferredNodeId = null, string? actor = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.Status == WorkflowInstanceStatus.Completed)
        {
            instanceLocks.TryRemove(instance.Id, out _);
            return new(WorkflowRuntimeState.Completed, instance.CurrentNodeId);
        }
        if (instance.Status != WorkflowInstanceStatus.Running)
        {
            instanceLocks.TryRemove(instance.Id, out _);
            throw new InvalidOperationException("已终止的流程实例不能继续运行。");
        }
        var instanceLock = instanceLocks.GetOrAdd(instance.Id, static _ => new object());
        try
        {
            lock (instanceLock)
            {
                // 发布校验已拒绝未经过 Loop.repeat 的图环；运行时用连续自动步数兜底，
                // 不能再用节点去重，否则合法 Loop 重入同一自动节点会被误判为死循环。
                var nextPreferredNodeId = preferredNodeId;
                for (var step = 0; step < 1000; step++)
                {
                    var nodeId = nextPreferredNodeId is Guid preferred
                        && instance.ActiveNodeIds.Contains(preferred)
                        ? preferred
                        : SelectNextActiveNode(instance, fields is not null);
                    nextPreferredNodeId = null;
                    switch (instance.GetNodeType(nodeId))
                    {
                        case WorkflowNodeType.End:
                            if (instance.ActiveNodeIds.Count > 1)
                                throw new InvalidOperationException("并行分支不能在其他分支仍活动时直接结束，必须先汇聚到 ParallelJoin。");
                            ExecuteStateTransaction(instance, () =>
                            {
                                instances.Complete(instance, occurredAt);
                                operations?.Record(instance, WorkflowOperationKind.NodeCompleted, "system", "结束节点完成", $"workflow-node-completed:{instance.Id}:{nodeId}:end:revision-{instance.Revision}", nodeId: nodeId, occurredAt: occurredAt);
                            });
                            return new(WorkflowRuntimeState.Completed, nodeId);
                        case WorkflowNodeType.Approval:
                            return new(WorkflowRuntimeState.WaitingForApproval, nodeId);
                        case WorkflowNodeType.Condition:
                            if (fields is null) return new(WorkflowRuntimeState.WaitingForCondition, nodeId);
                            var conditionAdvanced = false;
                            ExecuteStateTransaction(instance, () => conditionAdvanced = AdvanceConditionNode(instance, nodeId, fields));
                            if (!conditionAdvanced) return new(WorkflowRuntimeState.WaitingForCondition, nodeId);
                            continue;
                        case WorkflowNodeType.Notification:
                            ExecuteAutomaticNode(instance, nodeId, occurredAt, () => ExecuteNotification(instance, nodeId, occurredAt));
                            continue;
                        case WorkflowNodeType.BusinessAction:
                            ExecuteAutomaticNode(instance, nodeId, occurredAt, () => ExecuteBusinessAction(instance, nodeId, occurredAt, actor));
                            continue;
                        case WorkflowNodeType.ParallelSplit:
                            ExecuteStateTransaction(instance, () => instances.SplitParallel(instance, nodeId));
                            continue;
                        case WorkflowNodeType.ParallelJoin:
                            ExecuteStateTransaction(instance, () => AdvanceAutomaticNode(instance, nodeId));
                            continue;
                        case WorkflowNodeType.Loop:
                            ExecuteStateTransaction(instance, () => instances.AdvanceLoop(instance, nodeId));
                            continue;
                        case WorkflowNodeType.Start:
                            ExecuteStateTransaction(instance, () => AdvanceAutomaticNode(instance, nodeId));
                            continue;
                        default:
                            throw new InvalidOperationException($"流程节点类型不受运行时支持：{instance.GetNodeType(nodeId)}。");
                    }
                }

                throw new InvalidOperationException("流程自动节点超过最大连续执行步数 1000，已停止以保护运行时。");
            }
        }
        finally
        {
            ScheduleTerminalInstanceLockRelease(instance);
        }
    }

    /// <summary>
    /// 由流程发起人重试失败的自动节点。指定节点时只重试该失败节点；未指定时保持兼容，选择当前活动快照中的首个失败自动节点。
    /// </summary>
    public WorkflowRuntimeResult Retry(WorkflowInstance instance, string actor, DateTime? occurredAt = null, Guid? failedNodeId = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (string.IsNullOrWhiteSpace(actor) || actor.Trim().Length > 200)
            throw new ArgumentException("重试操作者无效。", nameof(actor));
        if (!instance.StartedBy.Equals(actor.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("只有流程发起人可以重试失败的自动节点。");
        if (instance.Status != WorkflowInstanceStatus.Running)
            throw new InvalidOperationException("已终止的流程实例不能重试自动节点。");
        if (operations is null)
            throw new InvalidOperationException("当前未配置流程操作记录，不能确认失败节点。");

        WorkflowRuntimeResult? result = null;
        var instanceLock = instanceLocks.GetOrAdd(instance.Id, static _ => new object());
        lock (instanceLock)
        {
            var candidates = GetRetryableNodeIds(instance);
            var retryNodeId = failedNodeId ?? candidates.FirstOrDefault();
            if (retryNodeId == Guid.Empty || !candidates.Contains(retryNodeId))
                throw new InvalidOperationException(failedNodeId is null
                    ? "当前流程没有可重试的失败自动节点。"
                    : "指定的失败自动节点不在当前活动快照中，或尚未失败。传入当前 NodeId 后重试。");

            var failedOperation = GetLatestNodeOperation(instance, retryNodeId);
            if (failedOperation is null || failedOperation.Kind != WorkflowOperationKind.NodeFailed)
                throw new InvalidOperationException("当前失败节点的失败审计已变化，请刷新后重试。");

            ExecuteTransaction(() =>
            {
                // Retry 会在锁内执行可能产生外部副作用的 action。先锁定并校验实例行，
                // 使并发撤回/完成先提交时在动作执行前失败，而不是执行后才输掉实例 CAS。
                try
                {
                    instances.LockForUpdate(instance);
                }
                catch (InvalidOperationException exception) when (exception.Message == "流程实例状态已变化，请刷新后重试。")
                {
                    throw new InvalidOperationException("该失败节点已由其他请求重试，请刷新后重试。", exception);
                }

                // NodeFailed 是回滚后的审计，不会递增实例 Revision；因此即使实例行锁成功，
                // 也必须在锁内重新确认候选节点和最近失败 ID，避免旧请求抢占已经过期的失败尝试。
                var currentCandidates = GetRetryableNodeIds(instance);
                var currentRetryNodeId = failedNodeId ?? currentCandidates.FirstOrDefault();
                var currentFailure = currentRetryNodeId == retryNodeId
                    ? GetLatestNodeOperation(instance, retryNodeId)
                    : null;
                if (currentRetryNodeId != retryNodeId
                    || !currentCandidates.Contains(retryNodeId)
                    || currentFailure is null
                    || currentFailure.Kind != WorkflowOperationKind.NodeFailed
                    || currentFailure.Id != failedOperation.Id)
                    throw new InvalidOperationException("当前失败节点的失败审计已变化，请刷新后重试。");

                var retryDedupeKey = $"workflow-runtime-retried:{instance.Id}:{retryNodeId}:{currentFailure.Id:N}";
                // Retried 是一次失败尝试的并发抢占标记。竞争失败时不能继续调用
                // Continue：通知节点没有和业务动作相同的 NodeExecuted 预占，可能被重复执行。
                if (!operations.TryRecord(instance, WorkflowOperationKind.Retried, actor.Trim(), "重试失败自动节点", retryDedupeKey, out _, nodeId: retryNodeId, occurredAt: occurredAt))
                    throw new InvalidOperationException("该失败节点已由其他请求重试，请刷新后重试。");
                result = Continue(instance, occurredAt: occurredAt, preferredNodeId: retryNodeId, actor: actor);
            });
        }
        return result!;
    }

    /// <summary>返回当前活动快照中仍可由发起人定向重试的失败自动节点。</summary>
    public IReadOnlyList<Guid> GetRetryableNodeIds(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (operations is null) return [];

        var failedNodes = operations.List(instanceId: instance.Id)
            .Where(x => x.NodeId is not null && IsExecutionOutcome(x.Kind))
            .GroupBy(x => x.NodeId!.Value)
            .Select(group => group.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Last())
            .Where(operation => operation.Kind == WorkflowOperationKind.NodeFailed)
            .Select(operation => operation.NodeId!.Value)
            .ToHashSet();
        return instance.ActiveNodeIds
            .Where(failedNodes.Contains)
            .Where(nodeId => IsRetryableAutomaticNode(instance, nodeId))
            .ToArray();
    }

    private WorkflowOperation? GetLatestNodeOperation(WorkflowInstance instance, Guid nodeId)
        => operations?.List(instanceId: instance.Id)
            .Where(x => x.NodeId == nodeId && IsExecutionOutcome(x.Kind))
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.Id)
            .LastOrDefault();

    private static bool IsExecutionOutcome(WorkflowOperationKind kind)
        => kind is WorkflowOperationKind.NodeFailed or WorkflowOperationKind.NodeExecuted;

    private static bool IsRetryableAutomaticNode(WorkflowInstance instance, Guid nodeId)
    {
        try
        {
            return instance.GetNodeType(nodeId) is WorkflowNodeType.Notification or WorkflowNodeType.BusinessAction;
        }
        catch (InvalidOperationException)
        {
            // 历史失败审计可能来自已损坏或旧版本快照；跳过它，不阻断其他有效节点。
            return false;
        }
    }

    public WorkflowRuntimeResult ContinueAfterApproval(WorkflowInstance instance, Guid approvalNodeId, IReadOnlyDictionary<string, object?>? fields = null, DateTime? occurredAt = null, string? actor = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        lock (instanceLocks.GetOrAdd(instance.Id, static _ => new object()))
        {
            if (!instance.ActiveNodeIds.Contains(approvalNodeId) || instance.GetNodeType(approvalNodeId) != WorkflowNodeType.Approval)
                throw new InvalidOperationException("完成的待办不属于当前活动审批节点。");
            ExecuteStateTransaction(instance, () => AdvanceAutomaticNode(instance, approvalNodeId));
            return Continue(instance, fields, occurredAt, actor: actor);
        }
    }

    /// <summary>
    /// 只推进指定的活动条件节点。并行流程中不把同一组字段隐式应用到其他活动条件，
    /// 其他条件由调用方分别提交字段后继续。
    /// </summary>
    public WorkflowRuntimeResult ContinueAfterCondition(WorkflowInstance instance, Guid conditionNodeId, IReadOnlyDictionary<string, object?> fields, DateTime? occurredAt = null, string? actor = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(fields);
        lock (instanceLocks.GetOrAdd(instance.Id, static _ => new object()))
        {
            if (!instance.ActiveNodeIds.Contains(conditionNodeId) || instance.GetNodeType(conditionNodeId) != WorkflowNodeType.Condition)
                throw new InvalidOperationException("指定的条件节点不属于当前活动条件节点。");
            var conditionAdvanced = false;
            ExecuteStateTransaction(instance, () => conditionAdvanced = AdvanceConditionNode(instance, conditionNodeId, fields));
            if (!conditionAdvanced) return new(WorkflowRuntimeState.WaitingForCondition, conditionNodeId);
            return Continue(instance, occurredAt: occurredAt, actor: actor);
        }
    }

    private void ExecuteBusinessAction(WorkflowInstance instance, Guid nodeId, DateTime? occurredAt, string? actor)
    {
        var executionKey = $"workflow-node-executed:{instance.Id}:{nodeId}:{GetExecutionScope(instance)}";
        var legacyExecutionKey = $"workflow-node-executed:{instance.Id}:{nodeId}";
        if (operations?.Exists(executionKey) == true || operations?.Exists(legacyExecutionKey) == true) return;
        var executionActor = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();

        // 在真实事务宿主中先原子占用稳定键，再执行动作。占用和动作处于同一个事务，
        // 因此并发调用只有一个进程能进入 handler；动作或后续推进失败时占用也会回滚。
        if (operations is not null && transactions is not null)
        {
            if (!operations.TryRecord(instance, WorkflowOperationKind.NodeExecuted, executionActor, "业务动作执行完成", executionKey, out _, nodeId: nodeId, occurredAt: occurredAt))
                return;
            if (!actions.ExecuteNode(instance, nodeId, "自动业务动作", actor)) throw new InvalidOperationException($"业务动作节点“{instance.GetNodeName(nodeId)}”缺少 action 配置。");
            return;
        }

        if (!actions.ExecuteNode(instance, nodeId, "自动业务动作", actor)) throw new InvalidOperationException($"业务动作节点“{instance.GetNodeName(nodeId)}”缺少 action 配置。");
        operations?.Record(instance, WorkflowOperationKind.NodeExecuted, executionActor, "业务动作执行完成", executionKey, nodeId: nodeId, occurredAt: occurredAt);
    }

    private void ExecuteAutomaticNode(WorkflowInstance instance, Guid nodeId, DateTime? occurredAt, Action operation)
    {
        var operationFailed = false;
        ExecuteStateTransaction(instance, () =>
        {
            try
            {
                operation();
            }
            catch
            {
                operationFailed = true;
                throw;
            }

            AdvanceAutomaticNode(instance, nodeId);
        }, exception =>
        {
            // 动作已成功但后续实例 CAS/持久化失败时，不能把并发冲突伪造成第二次节点失败。
            // 只有动作或通知本身抛错，才创建可重试的失败审计。
            if (operationFailed)
                RecordNodeFailure(instance, nodeId, exception, occurredAt);
        });
    }

    private void AdvanceAutomaticNode(WorkflowInstance instance, Guid nodeId)
    {
        var transition = GetAutomaticTransition(instance, nodeId);
        var targetType = instance.GetNodeType(transition.TargetNodeId);
        if (targetType == WorkflowNodeType.End && instance.ActiveNodeIds.Count > 1)
            throw new InvalidOperationException("并行分支不能在其他分支仍活动时直接结束，必须先汇聚到 ParallelJoin。");
        if (targetType == WorkflowNodeType.ParallelJoin)
        {
            instances.ArriveAtParallelJoin(instance, nodeId, transition.TargetNodeId);
            return;
        }
        instances.AdvanceActive(instance, nodeId, transition.TargetNodeId, transition.ConditionKey);
    }

    private bool AdvanceConditionNode(WorkflowInstance instance, Guid nodeId, IReadOnlyDictionary<string, object?> fields)
    {
        var transition = instance.TrySelectConditionTransition(nodeId, fields);
        if (transition is null) return false;
        if (instance.GetNodeType(transition.TargetNodeId) == WorkflowNodeType.ParallelJoin)
        {
            instances.ArriveAtParallelJoin(instance, nodeId, transition.TargetNodeId);
            return true;
        }
        instances.AdvanceActive(instance, nodeId, transition.TargetNodeId, transition.ConditionKey);
        return true;
    }

    private void ExecuteTransaction(Action operation, Action<Exception>? afterRollback = null)
    {
        if (transactions is null) operation();
        else transactions.Execute(operation, afterRollback);
    }

    private void ScheduleTerminalInstanceLockRelease(WorkflowInstance instance)
    {
        if (instance.Status == WorkflowInstanceStatus.Running) return;
        if (transactions is null)
        {
            ReleaseTerminalInstanceLock(instance);
            return;
        }

        // Continue 可能只是外层审批事务的嵌套步骤；终态锁必须等最外层提交后再释放，
        // 否则后续持久化失败回滚到 Running 时会丢失进程内串行化保护。
        transactions.Execute(static () => { }, afterRollback: null, afterCommit: () => ReleaseTerminalInstanceLock(instance));
    }

    private void ExecuteStateTransaction(WorkflowInstance instance, Action operation, Action<Exception>? afterRollback = null)
    {
        var previousNodeId = instance.CurrentNodeId;
        var previousStatus = instance.Status;
        var previousCompletedAt = instance.CompletedAt;
        var previousRevision = instance.Revision;
        var previousActiveNodes = instance.ActiveNodeIdsJson;
        var previousJoinArrivals = instance.ParallelJoinArrivalsJson;
        var previousLoopIterations = instance.LoopIterationsJson;
        var previousApprovalAssignees = instance.ApprovalAssigneesJson;
        try
        {
            ExecuteTransaction(() =>
            {
                // 所有会改变图快照的运行时状态都必须在事务内先锁定实例行，
                // 不只限于通知/业务动作；否则并发 Continue 可能在 Condition、Split、Loop
                // 或 End 分支上同时读到同一 Revision，再分别计算出不一致的下一状态。
                if (transactions is not null)
                    instances.LockForUpdate(instance);
                operation();
            }, transactions is null ? null : exception =>
            {
                instance.RestorePersistedState(previousNodeId, previousStatus, previousCompletedAt, previousRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations, previousApprovalAssignees);
                afterRollback?.Invoke(exception);
            });
        }
        catch (Exception exception)
        {
            if (transactions is null)
            {
                instance.RestorePersistedState(previousNodeId, previousStatus, previousCompletedAt, previousRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations, previousApprovalAssignees);
                afterRollback?.Invoke(exception);
            }
            throw;
        }
    }

    private void RecordNodeFailure(WorkflowInstance instance, Guid nodeId, Exception exception, DateTime? occurredAt)
    {
        try
        {
            operations?.Record(instance, WorkflowOperationKind.NodeFailed, "system", TrimFailure(exception.Message), $"workflow-node-failed:{instance.Id}:{nodeId}:{Guid.CreateVersion7()}", nodeId: nodeId, occurredAt: occurredAt);
        }
        catch
        {
            // 失败审计不能覆盖原始节点异常。
        }
    }

    private static string TrimFailure(string message) => message.Length <= 2000 ? message : message[..2000];

    private void ExecuteNotification(WorkflowInstance instance, Guid nodeId, DateTime? occurredAt)
    {
        var configuration = WorkflowNotificationDefinition.Parse(
            instance.GetNodeConfig(nodeId),
            instance.GetNodeName(nodeId),
            $"{instance.BusinessType} 业务对象已进入流程节点“{instance.GetNodeName(nodeId)}”。");
        foreach (var recipient in configuration.Recipients)
            notifications.Publish(recipient, configuration.Kind, configuration.Title!, configuration.Content!, configuration.Href,
                $"workflow-node-notification:{instance.Id}:{nodeId}:{GetExecutionScope(instance)}:{recipient.Trim().ToLowerInvariant()}", occurredAt);
        operations?.Record(instance, WorkflowOperationKind.NodeExecuted, "system", "通知节点执行完成", $"workflow-node-executed:{instance.Id}:{nodeId}:{GetExecutionScope(instance)}", nodeId: nodeId, occurredAt: occurredAt);
    }

    private static WorkflowConnection GetAutomaticTransition(WorkflowInstance instance, Guid nodeId)
    {
        var transitions = instance.GetOutgoingTransitions(nodeId);
        var transition = transitions.SingleOrDefault(x => x.ConditionKey is null);
        if (transition is null)
            throw new InvalidOperationException($"节点“{instance.GetNodeName(nodeId)}”没有唯一的自动连线。");
        return transition;
    }

    private static Guid SelectNextActiveNode(WorkflowInstance instance, bool hasFields)
    {
        // 并行分支中不能因为某条人工审批先进入等待，就饿死另一条通知/动作/网关分支。
        var automatic = instance.ActiveNodeIds
            .Where(nodeId => instance.GetNodeType(nodeId) is WorkflowNodeType.Start or WorkflowNodeType.Notification or WorkflowNodeType.BusinessAction or WorkflowNodeType.ParallelSplit or WorkflowNodeType.ParallelJoin or WorkflowNodeType.Loop or WorkflowNodeType.End)
            .OrderBy(nodeId => nodeId)
            .FirstOrDefault();
        if (automatic != Guid.Empty) return automatic;
        if (hasFields)
        {
            var condition = instance.ActiveNodeIds.Where(nodeId => instance.GetNodeType(nodeId) == WorkflowNodeType.Condition).OrderBy(nodeId => nodeId).FirstOrDefault();
            if (condition != Guid.Empty) return condition;
        }
        var approval = instance.ActiveNodeIds.Where(nodeId => instance.GetNodeType(nodeId) == WorkflowNodeType.Approval).OrderBy(nodeId => nodeId).FirstOrDefault();
        if (approval != Guid.Empty) return approval;
        return instance.ActiveNodeIds.Contains(instance.CurrentNodeId)
            ? instance.CurrentNodeId
            : instance.ActiveNodeIds.OrderBy(nodeId => nodeId).First();
    }

    private static string GetExecutionScope(WorkflowInstance instance)
        // Revision 在每次状态推进后持久化递增；使用它区分审批退回、循环重入和
        // 并行分支再次进入同一自动节点，同时让事务回滚后的重试复用同一执行键。
        => $"revision-{instance.Revision}";
}
