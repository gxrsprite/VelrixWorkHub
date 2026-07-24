using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

public sealed class WorkflowInstanceService(IWorkflowInstanceRepository repository, WorkflowOperationService? operations = null, IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null) => repository.List(businessType, businessId, status).OrderByDescending(x => x.StartedAt).ToArray();

    /// <summary>在当前数据库事务内锁定实例行；内存仓储没有跨连接锁能力时保持兼容。</summary>
    public void LockForUpdate(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (repository is IWorkflowInstanceLockRepository locking)
            locking.LockForUpdate(instance);
    }

    public WorkflowInstance Start(WorkflowDefinition definition, string businessType, Guid businessId, DateTime? startedAt = null, string? startedBy = null, Guid? previousInstanceId = null)
        => StartCore(definition, businessType, businessId, startedAt, startedBy, previousInstanceId, null);

    /// <summary>
    /// 启动绑定专用入口；只记录仓储确认成功插入的实例，避免并发竞争时补偿掉胜出实例。
    /// </summary>
    internal WorkflowInstance StartWithCompensation(WorkflowDefinition definition, string businessType, Guid businessId, DateTime? startedAt, string? startedBy, Guid? previousInstanceId, ICollection<Guid> createdInstanceIds)
        => StartCore(definition, businessType, businessId, startedAt, startedBy, previousInstanceId, createdInstanceIds);

    internal void RemoveCreatedInstances(IReadOnlyCollection<Guid> instanceIds)
    {
        if (repository is not IWorkflowInstanceCompensationRepository compensation) return;
        foreach (var instanceId in instanceIds.Distinct()) compensation.Remove(instanceId);
    }

    private WorkflowInstance StartCore(WorkflowDefinition definition, string businessType, Guid businessId, DateTime? startedAt, string? startedBy, Guid? previousInstanceId, ICollection<Guid>? createdInstanceIds)
    {
        WorkflowInstance? instance = null;
        ExecuteTransaction(() =>
        {
            instance = WorkflowInstance.Start(definition, businessType, businessId, startedAt, startedBy, previousInstanceId);
            if (!repository.TryAdd(instance!))
            {
                instance = repository.List(businessType, businessId, WorkflowInstanceStatus.Running)
                    .SingleOrDefault(x => x.DefinitionCode.Equals(definition.Code, StringComparison.OrdinalIgnoreCase))
                    ?? throw new WorkflowRunningInstanceConflictException();
                return;
            }
            createdInstanceIds?.Add(instance.Id);
            var kind = previousInstanceId is null ? WorkflowOperationKind.Started : WorkflowOperationKind.Resubmitted;
            var comment = previousInstanceId is null ? "发起审批" : "重新提交审批";
            var dedupeKey = previousInstanceId is null ? $"workflow-instance-started:{instance.Id}" : $"workflow-instance-resubmitted:{instance.Id}";
            operations?.Record(instance, kind, instance.StartedBy, comment, dedupeKey, occurredAt: instance.StartedAt);
        });
        return instance!;
    }

    public void Complete(WorkflowInstance instance, DateTime? completedAt = null) => Finish(instance, WorkflowInstanceStatus.Completed, completedAt);
    public void Reject(WorkflowInstance instance, DateTime? completedAt = null) => Finish(instance, WorkflowInstanceStatus.Rejected, completedAt);
    public void Cancel(WorkflowInstance instance, DateTime? completedAt = null) => Finish(instance, WorkflowInstanceStatus.Cancelled, completedAt);

    /// <summary>以实例 CAS 固化审批人快照；进程重启或组织成员变化后仍只按首次解析结果补待办。</summary>
    public IReadOnlyList<string> EnsureApprovalAssigneeSnapshot(WorkflowInstance instance, Guid nodeId, IReadOnlyCollection<string> assignees)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var existing = instance.GetApprovalAssignees(nodeId);
        if (existing.Count > 0) return existing;

        var previousNodeId = instance.CurrentNodeId;
        var previousStatus = instance.Status;
        var previousCompletedAt = instance.CompletedAt;
        var expectedRevision = instance.Revision;
        var previousActiveNodes = instance.ActiveNodeIdsJson;
        var previousJoinArrivals = instance.ParallelJoinArrivalsJson;
        var previousLoopIterations = instance.LoopIterationsJson;
        var previousApprovalAssignees = instance.ApprovalAssigneesJson;
        try
        {
            ExecuteTransaction(() =>
            {
                instance.CaptureApprovalAssignees(nodeId, assignees);
                PersistOrRestore(instance, previousNodeId, previousStatus, previousCompletedAt, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations, previousApprovalAssignees);
            }, _ => instance.RestorePersistedState(previousNodeId, previousStatus, previousCompletedAt, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations, previousApprovalAssignees), instance);
        }
        catch (InvalidOperationException exception) when (exception.Message == "流程实例状态已变化，请刷新后重试。")
        {
            // 两个进程首次补偿同一审批节点时，另一方可能已经成功固化快照；
            // 重新读取胜出实例并复用其快照，避免把正常幂等竞争暴露成页面错误。
            var winner = repository.List().SingleOrDefault(x => x.Id == instance.Id);
            var winnerAssignees = winner?.GetApprovalAssignees(nodeId) ?? [];
            if (winner is null || winner.Status != WorkflowInstanceStatus.Running || winnerAssignees.Count == 0)
                throw;

            RestoreInstanceState(instance, winner);
            return winnerAssignees;
        }
        return instance.GetApprovalAssignees(nodeId);
    }
    public void ReturnTo(WorkflowInstance instance, Guid sourceNodeId, Guid targetNodeId)
    {
        var previousNodeId = instance.CurrentNodeId;
        var expectedRevision = instance.Revision;
        var previousActiveNodes = instance.ActiveNodeIdsJson;
        var previousJoinArrivals = instance.ParallelJoinArrivalsJson;
        var previousLoopIterations = instance.LoopIterationsJson;
        ExecuteTransaction(() =>
        {
            instance.ReturnTo(sourceNodeId, targetNodeId);
            PersistOrRestore(instance, previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations);
            RecordTransition(instance, sourceNodeId, targetNodeId, "return");
        }, _ => instance.RestorePersistedState(previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations), instance);
    }
    public void Advance(WorkflowInstance instance, Guid targetNodeId, string? conditionKey = null)
    {
        var previousNodeId = instance.CurrentNodeId;
        var expectedRevision = instance.Revision;
        var previousActiveNodes = instance.ActiveNodeIdsJson;
        var previousJoinArrivals = instance.ParallelJoinArrivalsJson;
        var previousLoopIterations = instance.LoopIterationsJson;
        ExecuteTransaction(() =>
        {
            instance.AdvanceTo(targetNodeId, conditionKey);
            PersistOrRestore(instance, previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations);
            RecordTransition(instance, previousNodeId, targetNodeId, conditionKey);
        }, _ => instance.RestorePersistedState(previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations), instance);
    }

    public void AdvanceActive(WorkflowInstance instance, Guid sourceNodeId, Guid targetNodeId, string? conditionKey = null)
    {
        var previousNodeId = instance.CurrentNodeId;
        var expectedRevision = instance.Revision;
        var previousActiveNodes = instance.ActiveNodeIdsJson;
        var previousJoinArrivals = instance.ParallelJoinArrivalsJson;
        var previousLoopIterations = instance.LoopIterationsJson;
        ExecuteTransaction(() =>
        {
            instance.AdvanceActiveNode(sourceNodeId, targetNodeId, conditionKey);
            PersistOrRestore(instance, previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations);
            RecordTransition(instance, sourceNodeId, targetNodeId, conditionKey);
        }, _ => instance.RestorePersistedState(previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations), instance);
    }

    public bool ArriveAtParallelJoin(WorkflowInstance instance, Guid sourceNodeId, Guid joinNodeId)
    {
        var previousNodeId = instance.CurrentNodeId;
        var expectedRevision = instance.Revision;
        var previousActiveNodes = instance.ActiveNodeIdsJson;
        var previousJoinArrivals = instance.ParallelJoinArrivalsJson;
        var previousLoopIterations = instance.LoopIterationsJson;
        var joined = false;
        ExecuteTransaction(() =>
        {
            joined = instance.ArriveAtParallelJoin(sourceNodeId, joinNodeId);
            PersistOrRestore(instance, previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations);
            var revisionScope = GetTransitionRevisionScope(instance);
            operations?.Record(instance, WorkflowOperationKind.NodeCompleted, "system", "并行分支到达汇聚", $"workflow-node-completed:{instance.Id}:{sourceNodeId}:{joinNodeId}:join:{revisionScope}", nodeId: sourceNodeId);
            if (joined) operations?.Record(instance, WorkflowOperationKind.NodeEntered, "system", "并行汇聚完成", $"workflow-node-entered:{instance.Id}:{sourceNodeId}:{joinNodeId}:join:{revisionScope}", nodeId: joinNodeId);
        }, _ => instance.RestorePersistedState(previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations), instance);
        return joined;
    }

    public IReadOnlyList<Guid> SplitParallel(WorkflowInstance instance, Guid splitNodeId)
    {
        var previousNodeId = instance.CurrentNodeId;
        var expectedRevision = instance.Revision;
        var previousActiveNodes = instance.ActiveNodeIdsJson;
        var previousJoinArrivals = instance.ParallelJoinArrivalsJson;
        var previousLoopIterations = instance.LoopIterationsJson;
        IReadOnlyList<Guid> targets = [];
        ExecuteTransaction(() =>
        {
            targets = instance.SplitParallel(splitNodeId);
            PersistOrRestore(instance, previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations);
            var revisionScope = GetTransitionRevisionScope(instance);
            operations?.Record(instance, WorkflowOperationKind.NodeCompleted, "system", "并行拆分", $"workflow-node-completed:{instance.Id}:{splitNodeId}:split:{revisionScope}", nodeId: splitNodeId);
            foreach (var target in targets)
                operations?.Record(instance, WorkflowOperationKind.NodeEntered, "system", "并行分支进入", $"workflow-node-entered:{instance.Id}:{splitNodeId}:{target}:split:{revisionScope}", nodeId: target);
        }, _ => instance.RestorePersistedState(previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations), instance);
        return targets;
    }

    public WorkflowConnection AdvanceCondition(WorkflowInstance instance, IReadOnlyDictionary<string, object?> fields)
    {
        var previousNodeId = instance.CurrentNodeId;
        var expectedRevision = instance.Revision;
        var previousActiveNodes = instance.ActiveNodeIdsJson;
        var previousJoinArrivals = instance.ParallelJoinArrivalsJson;
        var previousLoopIterations = instance.LoopIterationsJson;
        WorkflowConnection? transition = null;
        ExecuteTransaction(() =>
        {
            transition = instance.AdvanceCondition(fields);
            PersistOrRestore(instance, previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations);
            RecordTransition(instance, previousNodeId, transition!.TargetNodeId, transition.ConditionKey);
        }, _ => instance.RestorePersistedState(previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations), instance);
        return transition!;
    }

    private void Finish(WorkflowInstance instance, WorkflowInstanceStatus status, DateTime? completedAt)
    {
        var previousNodeId = instance.CurrentNodeId;
        var previousStatus = instance.Status;
        var previousCompletedAt = instance.CompletedAt;
        var expectedRevision = instance.Revision;
        var previousActiveNodes = instance.ActiveNodeIdsJson;
        var previousJoinArrivals = instance.ParallelJoinArrivalsJson;
        var previousLoopIterations = instance.LoopIterationsJson;
        ExecuteTransaction(() =>
        {
            switch (status)
            {
                case WorkflowInstanceStatus.Completed: instance.Complete(completedAt); break;
                case WorkflowInstanceStatus.Rejected: instance.Reject(completedAt); break;
                case WorkflowInstanceStatus.Cancelled: instance.Cancel(completedAt); break;
                default: throw new ArgumentOutOfRangeException(nameof(status));
            }
            PersistOrRestore(instance, previousNodeId, previousStatus, previousCompletedAt, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations);
        }, _ => instance.RestorePersistedState(previousNodeId, previousStatus, previousCompletedAt, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations), instance);
    }

    public WorkflowConnection AdvanceLoop(WorkflowInstance instance, Guid loopNodeId)
    {
        var previousNodeId = instance.CurrentNodeId;
        var expectedRevision = instance.Revision;
        var previousActiveNodes = instance.ActiveNodeIdsJson;
        var previousJoinArrivals = instance.ParallelJoinArrivalsJson;
        var previousLoopIterations = instance.LoopIterationsJson;
        WorkflowConnection? transition = null;
        ExecuteTransaction(() =>
        {
            transition = instance.AdvanceLoop(loopNodeId);
            PersistOrRestore(instance, previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations);
            if (instance.GetNodeType(transition!.TargetNodeId) == WorkflowNodeType.ParallelJoin && !instance.ActiveNodeIds.Contains(transition.TargetNodeId))
            {
                var transitionKey = transition.ConditionKey?.Trim() ?? "default";
                operations?.Record(instance, WorkflowOperationKind.NodeCompleted, "system", "节点完成", $"workflow-node-completed:{instance.Id}:{loopNodeId}:{transition.TargetNodeId}:{transitionKey}:{GetTransitionRevisionScope(instance)}", nodeId: loopNodeId);
            }
            else RecordTransition(instance, loopNodeId, transition.TargetNodeId, transition.ConditionKey);
        }, _ => instance.RestorePersistedState(previousNodeId, WorkflowInstanceStatus.Running, null, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations), instance);
        return transition!;
    }

    private void PersistOrRestore(WorkflowInstance instance, Guid previousNodeId, WorkflowInstanceStatus previousStatus, DateTime? previousCompletedAt, long expectedRevision, string? previousActiveNodes = null, string? previousJoinArrivals = null, string? previousLoopIterations = null, string? previousApprovalAssignees = null)
    {
        if (repository.TryUpdate(instance)) return;
        instance.RestorePersistedState(previousNodeId, previousStatus, previousCompletedAt, expectedRevision, previousActiveNodes, previousJoinArrivals, previousLoopIterations, previousApprovalAssignees);
        throw new InvalidOperationException("流程实例状态已变化，请刷新后重试。");
    }

    private void RecordTransition(WorkflowInstance instance, Guid previousNodeId, Guid targetNodeId, string? conditionKey)
    {
        var transitionKey = conditionKey?.Trim() ?? "default";
        var revisionScope = GetTransitionRevisionScope(instance);
        operations?.Record(instance, WorkflowOperationKind.NodeCompleted, "system", "节点完成", $"workflow-node-completed:{instance.Id}:{previousNodeId}:{targetNodeId}:{transitionKey}:{revisionScope}", nodeId: previousNodeId);
        operations?.Record(instance, WorkflowOperationKind.NodeEntered, "system", "节点进入", $"workflow-node-entered:{instance.Id}:{previousNodeId}:{targetNodeId}:{transitionKey}:{revisionScope}", nodeId: targetNodeId);
    }

    private static string GetTransitionRevisionScope(WorkflowInstance instance) => $"revision-{instance.Revision}";

    private static void RestoreInstanceState(WorkflowInstance target, WorkflowInstance source)
        => target.RestorePersistedState(
            source.CurrentNodeId,
            source.Status,
            source.CompletedAt,
            source.Revision,
            source.ActiveNodeIdsJson,
            source.ParallelJoinArrivalsJson,
            source.LoopIterationsJson,
            source.ApprovalAssigneesJson);

    private void ExecuteTransaction(Action operation, Action<Exception>? afterRollback = null, WorkflowInstance? lockInstance = null)
    {
        if (transactions is not null && lockInstance is not null)
        {
            var lockedOperation = operation;
            operation = () =>
            {
                LockForUpdate(lockInstance);
                lockedOperation();
            };
        }

        if (transactions is not null)
        {
            transactions.Execute(operation, afterRollback);
            return;
        }

        try
        {
            operation();
        }
        catch (Exception exception)
        {
            afterRollback?.Invoke(exception);
            throw;
        }
    }
}
