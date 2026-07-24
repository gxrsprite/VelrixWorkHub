using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

public sealed class WorkflowTaskService(IWorkflowTaskRepository repository, WorkflowInstanceService? instances = null, WorkflowActionExecutor? actionExecutor = null, NotificationService? notifications = null, WorkflowOperationService? operations = null, WorkflowRuntimeService? runtime = null, IWorkflowTransactionBoundary? transactions = null, IWorkflowApproverResolver? approverResolver = null, IServiceProvider? serviceProvider = null)
{
    private readonly IWorkflowApproverResolver resolvedApproverResolver = approverResolver ?? new DefaultWorkflowApproverResolver();
    private WorkflowActionExecutor? ResolvedActionExecutor => actionExecutor ?? serviceProvider?.GetService(typeof(WorkflowActionExecutor)) as WorkflowActionExecutor;
    private WorkflowRuntimeService? ResolvedRuntime => runtime ?? serviceProvider?.GetService(typeof(WorkflowRuntimeService)) as WorkflowRuntimeService;

    public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
    {
        // 不把 assignee 下推到仓储层，避免不同数据库排序规则导致大小写敏感，
        // 收件箱的 admin/ADMIN 筛选保持一致。
        var tasks = repository.List(instanceId, status: status);
        if (!string.IsNullOrWhiteSpace(assignee)) tasks = tasks.Where(x => x.Assignee.Equals(assignee.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        return tasks.OrderByDescending(x => x.CreatedAt).ToArray();
    }

    /// <summary>
    /// 按实例一次读取已有待办，再批量补齐审批节点，避免多审批人场景逐人查询待办表。
    /// </summary>
    public IReadOnlyList<WorkflowTask> EnsureApprovalTasks(WorkflowInstance instance, WorkflowDefinition definition, DateTime? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(definition);
        var previousState = CaptureState(instance);
        var createdTaskIds = new List<Guid>();
        IReadOnlyList<WorkflowTask> result = [];
        void RestoreTaskCreationState()
        {
            RestoreState(instance, previousState);
            RestoreCreatedTasks(createdTaskIds);
        }
        try
        {
            ExecuteTransaction(() => result = EnsureApprovalTasksCore(instance, definition, createdAt, createdTaskIds), RestoreTaskCreationState);
            return result;
        }
        catch
        {
            RestoreTaskCreationState();
            throw;
        }
    }

    private IReadOnlyList<WorkflowTask> EnsureApprovalTasksCore(WorkflowInstance instance, WorkflowDefinition definition, DateTime? createdAt, ICollection<Guid>? createdTaskIds = null)
    {
        EnsureRunningInstanceForTaskCreation(instance);
        EnsureCurrentInstanceForTaskCreation(instance);
        var allTasks = repository.List(instance.Id);
        var existing = allTasks
            .Where(x => x.Status == WorkflowTaskStatus.Pending)
            .GroupBy(x => TaskKey(x.NodeId, x.Assignee), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var rounds = new Dictionary<Guid, int>();
        var tasks = new List<WorkflowTask>();
        foreach (var node in definition.Nodes.Where(x => x.Type == WorkflowNodeType.Approval))
        {
            var pendingAssignees = allTasks.Where(x => x.Status == WorkflowTaskStatus.Pending && x.NodeId == node.Id).Select(x => x.Assignee).ToArray();
            var approvers = EnsureApprovalAssignees(instance, node.Id, node.ConfigJson, pendingAssignees);
            foreach (var approver in approvers)
            {
                var key = TaskKey(node.Id, approver);
                if (existing.ContainsKey(key)) continue;
                if (!rounds.TryGetValue(node.Id, out var round))
                {
                    round = GetCurrentOrNextRound(allTasks, node.Id);
                    rounds[node.Id] = round;
                }
                tasks.Add(CreateApprovalTaskCore(instance, node.Id, node.Name, approver, createdAt, round, existing, createdTaskIds));
            }
        }

        return tasks;
    }

    public IReadOnlyList<WorkflowTask> EnsureCurrentApprovalTask(WorkflowInstance instance, DateTime? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var previousState = CaptureState(instance);
        var createdTaskIds = new List<Guid>();
        IReadOnlyList<WorkflowTask> result = [];
        void RestoreTaskCreationState()
        {
            RestoreState(instance, previousState);
            RestoreCreatedTasks(createdTaskIds);
        }
        try
        {
            ExecuteTransaction(() => result = EnsureCurrentApprovalTaskCore(instance, createdAt, createdTaskIds), RestoreTaskCreationState);
            return result;
        }
        catch
        {
            RestoreTaskCreationState();
            throw;
        }
    }

    private IReadOnlyList<WorkflowTask> EnsureCurrentApprovalTaskCore(WorkflowInstance instance, DateTime? createdAt, ICollection<Guid>? createdTaskIds = null)
    {
        EnsureRunningInstanceForTaskCreation(instance);
        EnsureCurrentInstanceForTaskCreation(instance);
        var allTasks = repository.List(instance.Id);
        var existing = allTasks
            .Where(x => x.Status == WorkflowTaskStatus.Pending)
            .GroupBy(x => TaskKey(x.NodeId, x.Assignee), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var tasks = new List<WorkflowTask>();
        // EnsureApprovalAssignees 可能因快照 CAS 竞争失败而恢复胜出实例；恢复会重建 ActiveNodeIds，
        // 因此这里必须遍历稳定副本，不能在恢复过程中继续枚举领域对象内部的 HashSet。
        foreach (var nodeId in instance.ActiveNodeIds
                     .Where(x => instance.GetNodeType(x) == WorkflowNodeType.Approval)
                     .ToArray())
        {
            var round = GetCurrentOrNextRound(allTasks, nodeId);
            var pendingAssignees = allTasks.Where(x => x.Status == WorkflowTaskStatus.Pending && x.NodeId == nodeId).Select(x => x.Assignee).ToArray();
            var approvers = EnsureApprovalAssignees(instance, nodeId, instance.GetNodeConfig(nodeId), pendingAssignees);
            var effectiveApprovers = GetEffectiveApproversForRound(allTasks, nodeId, round, approvers);
            // 未知 Pending 可能由人工修复或未来扩展创建，不能把它当成初始快照缺失后覆盖掉。
            if (pendingAssignees.Any(assignee => !effectiveApprovers.Contains(assignee, StringComparer.OrdinalIgnoreCase)))
                continue;
            foreach (var approver in effectiveApprovers)
            {
                var key = TaskKey(nodeId, approver);
                if (existing.ContainsKey(key)) continue;
                tasks.Add(CreateApprovalTaskCore(instance, nodeId, instance.GetNodeName(nodeId), approver, createdAt, round, existing, createdTaskIds));
            }
        }
        return tasks;
    }

    private void EnsureCurrentInstanceForTaskCreation(WorkflowInstance instance)
    {
        // 只有真实 Workflow 事务才能持有数据库行锁；无事务内存宿主和旧测试仓储继续走原有 CAS 补偿路径。
        if (instances is null || transactions is null) return;

        try
        {
            instances.LockForUpdate(instance);
        }
        catch (InvalidOperationException exception) when (exception.Message == "流程实例状态已变化，请刷新后重试。")
        {
            // 退回/推进可能已经先提交。刷新胜出实例后再补偿当前活动节点，
            // 避免陈旧进程沿用旧 ActiveNodeIds 给历史审批节点创建孤儿待办。
            var winner = instances.List().SingleOrDefault(x => x.Id == instance.Id);
            if (winner is null || winner.Status != WorkflowInstanceStatus.Running) throw;

            RestoreState(instance, CaptureState(winner));
            instances.LockForUpdate(instance);
        }
    }

    /// <summary>
    /// 重试失败自动节点并在同一应用事务内补齐后续审批待办。
    /// RuntimeService 只负责图状态推进，不能单独作为收件箱的重试用例使用。
    /// </summary>
    public WorkflowRuntimeResult Retry(WorkflowInstance instance, string actor, DateTime? occurredAt = null, Guid? failedNodeId = null)
    {
        var resolvedRuntime = ResolvedRuntime;
        if (resolvedRuntime is null) throw new InvalidOperationException("当前未配置流程运行时，不能重试自动节点。");
        var previousInstanceState = CaptureState(instance);
        WorkflowRuntimeResult? result = null;
        void RestoreRetryState() => RestoreState(instance, previousInstanceState);
        try
        {
            ExecuteTransaction(() =>
            {
                result = resolvedRuntime.Retry(instance, actor, occurredAt, failedNodeId);
                if (result.State == WorkflowRuntimeState.WaitingForApproval)
                    EnsureCurrentApprovalTask(instance, occurredAt);
            }, RestoreRetryState);
            return result!;
        }
        catch
        {
            // 待办写入失败时，运行时可能已经把实例推进到了审批节点；无数据库事务的宿主也必须恢复内存态。
            RestoreRetryState();
            throw;
        }
    }

    public WorkflowTask CreateApprovalTask(WorkflowInstance instance, Guid nodeId, string nodeName, string assignee, DateTime? createdAt = null, int? round = null)
    {
        var previousState = CaptureState(instance);
        var createdTaskIds = new List<Guid>();
        WorkflowTask? result = null;
        void RestoreTaskCreationState()
        {
            RestoreState(instance, previousState);
            RestoreCreatedTasks(createdTaskIds);
        }
        try
        {
            ExecuteTransaction(() => result = CreateApprovalTaskCoreEntry(instance, nodeId, nodeName, assignee, createdAt, round, createdTaskIds), RestoreTaskCreationState);
            return result!;
        }
        catch
        {
            RestoreTaskCreationState();
            throw;
        }
    }

    private WorkflowTask CreateApprovalTaskCoreEntry(WorkflowInstance instance, Guid nodeId, string nodeName, string assignee, DateTime? createdAt, int? round, ICollection<Guid>? createdTaskIds = null)
    {
        LockInstanceForDecision(instance);
        EnsureRunningInstanceForTaskCreation(instance);
        if (transactions is not null) EnsureApprovalTaskNodeIsActive(instance, nodeId);
        if (string.IsNullOrWhiteSpace(assignee)) throw new ArgumentException("审批人不能为空。", nameof(assignee));
        var normalizedAssignee = assignee.Trim();
        var allTasks = repository.List(instance.Id);
        var existing = allTasks.FirstOrDefault(x => x.Status == WorkflowTaskStatus.Pending && x.NodeId == nodeId && x.Assignee.Equals(normalizedAssignee, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        return CreateApprovalTaskCore(instance, nodeId, nodeName, normalizedAssignee, createdAt, round ?? GetCurrentOrNextRound(allTasks, nodeId), createdTaskIds: createdTaskIds);
    }

    private WorkflowTask CreateApprovalTaskCore(WorkflowInstance instance, Guid nodeId, string nodeName, string assignee, DateTime? createdAt, int round, IDictionary<string, WorkflowTask>? existing = null, ICollection<Guid>? createdTaskIds = null)
    {
        var normalizedAssignee = assignee.Trim();
        var task = new WorkflowTask(instance, nodeId, nodeName, normalizedAssignee, createdAt, round);
        if (!repository.TryAdd(task))
        {
            task = repository.List(instance.Id).SingleOrDefault(x => x.Id == task.Id)
                ?? throw new InvalidOperationException("待办原子写入未返回胜出记录。");
        }
        else createdTaskIds?.Add(task.Id);
        if (existing is not null) existing[TaskKey(nodeId, normalizedAssignee)] = task;
        operations?.Record(task, WorkflowOperationKind.Assigned, task.Assignee, "生成审批待办", $"workflow-task-assigned:{task.Id}", occurredAt: task.CreatedAt);
        QueueTaskNotification(task);
        return task;
    }

    private void QueueTaskNotification(WorkflowTask task)
    {
        if (notifications is null) return;
        void Publish() => notifications.Publish(
            task.Assignee,
            WorkNotificationKind.Approval,
            $"待审批：{task.NodeName}",
            $"{task.BusinessType} 业务对象需要你的审批。",
            $"/Workflow/Inbox?assignee={Uri.EscapeDataString(task.Assignee)}&businessType={Uri.EscapeDataString(task.BusinessType)}&businessId={task.BusinessId}",
            $"workflow-task:{task.Id}",
            task.CreatedAt);

        if (transactions is null) Publish();
        else transactions.Execute(static () => { }, afterRollback: null, afterCommit: Publish);
    }

    private void QueueNotificationRead(string recipient, string dedupeKey, DateTime? readAt)
    {
        if (notifications is null) return;
        void MarkRead() => notifications.MarkReadByDedupeKey(recipient, dedupeKey, readAt);
        if (transactions is null) MarkRead();
        else transactions.Execute(static () => { }, afterRollback: null, afterCommit: MarkRead);
    }

    private static string TaskKey(Guid nodeId, string assignee) => $"{nodeId:N}:{assignee.Trim()}";

    private static void EnsureRunningInstanceForTaskCreation(WorkflowInstance instance)
    {
        if (instance.Status != WorkflowInstanceStatus.Running)
            throw new InvalidOperationException("已结束的流程实例不能创建审批待办。");
    }

    private static void EnsureApprovalTaskNodeIsActive(WorkflowInstance instance, Guid nodeId)
    {
        // Start 阶段保留旧宿主手工构造待办的兼容路径；进入图运行时后，独立创建入口不能绕过活动节点门禁。
        if (instance.ActiveNodeIds.Any(x => instance.GetNodeType(x) == WorkflowNodeType.Start)) return;
        if (!instance.ActiveNodeIds.Contains(nodeId) || instance.GetNodeType(nodeId) != WorkflowNodeType.Approval)
            throw new InvalidOperationException("审批待办节点不属于流程实例当前活动审批节点，不能创建。");
    }

    private static int GetCurrentOrNextRound(IEnumerable<WorkflowTask> tasks, Guid nodeId)
    {
        var nodeTasks = tasks.Where(x => x.NodeId == nodeId).ToArray();
        var pendingRound = nodeTasks.Where(x => x.Status == WorkflowTaskStatus.Pending).Select(x => x.Round).DefaultIfEmpty(0).Max();
        return pendingRound > 0 ? pendingRound : nodeTasks.Select(x => x.Round).DefaultIfEmpty(0).Max() + 1;
    }

    private IReadOnlyList<string> EnsureApprovalAssignees(WorkflowInstance instance, Guid nodeId, string nodeConfigJson, IReadOnlyCollection<string> pendingAssignees)
    {
        var snapshot = instance.GetApprovalAssignees(nodeId);
        if (snapshot.Count > 0) return snapshot;
        // 兼容旧实例：已有待办即视为它的历史快照，避免升级后把组织新增成员加入进行中的审批。
        var initial = pendingAssignees.Count > 0 ? pendingAssignees : resolvedApproverResolver.Resolve(instance, nodeConfigJson);
        if (instances is not null) return instances.EnsureApprovalAssigneeSnapshot(instance, nodeId, initial);
        instance.CaptureApprovalAssignees(nodeId, initial);
        return instance.GetApprovalAssignees(nodeId);
    }

    private static IReadOnlyList<string> GetEffectiveApproversForRound(IReadOnlyList<WorkflowTask> allTasks, Guid nodeId, int round, IReadOnlyList<string> initialApprovers)
    {
        var transfers = allTasks
            .Where(x => x.NodeId == nodeId && x.Round == round && x.Status == WorkflowTaskStatus.Transferred && !string.IsNullOrWhiteSpace(x.TransferTarget))
            .GroupBy(x => x.Assignee, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last().TransferTarget!, StringComparer.OrdinalIgnoreCase);
        return initialApprovers
            .Select(assignee => ResolveTransferTarget(assignee, transfers))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveTransferTarget(string assignee, IReadOnlyDictionary<string, string> transfers)
    {
        var current = assignee;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (transfers.TryGetValue(current, out var target) && visited.Add(current)) current = target;
        return current;
    }

    public void Approve(WorkflowTask task, string actor, string? comment = null, DateTime? completedAt = null)
    {
        EnsureCanDecide(task, actor, comment);
        var originalState = CaptureState(task);
        var originalRevision = task.Revision;
        var instance = FindInstance(task);
        (Guid CurrentNodeId, WorkflowInstanceStatus Status, DateTime? CompletedAt, long Revision, string ActiveNodeIdsJson, string ParallelJoinArrivalsJson, string LoopIterationsJson, string ApprovalAssigneesJson)? originalInstanceState = instance is null ? null : CaptureState(instance);
        var actionExecuted = false;
        var cancelledSiblings = new List<WorkflowTask>();
        var siblingSnapshots = new List<(WorkflowTask Task, (WorkflowTaskStatus Status, string? TransferTarget, string? DecisionComment, string? DecisionActor, DateTime? CompletedAt) State, long Revision)>();
        void RestoreApprovalState()
        {
            task.RestorePersistedState(originalState.Status, originalState.TransferTarget, originalState.DecisionComment, originalState.DecisionActor, originalState.CompletedAt, originalRevision);
            if (instance is not null && originalInstanceState is { } state)
                RestoreState(instance, state);
            foreach (var sibling in siblingSnapshots)
                sibling.Task.RestorePersistedState(sibling.State.Status, sibling.State.TransferTarget, sibling.State.DecisionComment, sibling.State.DecisionActor, sibling.State.CompletedAt, sibling.Revision);
        }
        try
        {
            ExecuteTransaction(() =>
            {
                LockInstanceForDecision(instance);
                var expectedRevision = ClaimTask(task);
                var previousState = CaptureState(task);
                actionExecuted = ExecuteFinalAction(task, WorkflowActionTrigger.Approved, comment, actor);
                PersistDecision(task, expectedRevision, previousState, () => task.Approve(actor, comment, completedAt));
                operations?.Record(task, WorkflowOperationKind.Approved, task.DecisionActor!, task.DecisionComment, $"workflow-task-approved:{task.Id}", occurredAt: task.CompletedAt);
                CancelSiblingTasksAfterApprovalThreshold(task, completedAt, cancelledSiblings, siblingSnapshots);
                CompleteInstanceIfNoPending(task, actionExecuted, actor);
            }, RestoreApprovalState);
        }
        catch
        {
            RestoreApprovalState();
            throw;
        }
        QueueNotificationRead(task.Assignee, $"workflow-task:{task.Id}", task.CompletedAt);
        foreach (var sibling in cancelledSiblings)
            QueueNotificationRead(sibling.Assignee, $"workflow-task:{sibling.Id}", sibling.CompletedAt);
    }

    public void Reject(WorkflowTask task, string actor, string? comment = null, DateTime? completedAt = null)
    {
        EnsureCanDecide(task, actor, comment);
        var originalState = CaptureState(task);
        var originalRevision = task.Revision;
        var instance = FindInstance(task);
        (Guid CurrentNodeId, WorkflowInstanceStatus Status, DateTime? CompletedAt, long Revision, string ActiveNodeIdsJson, string ParallelJoinArrivalsJson, string LoopIterationsJson, string ApprovalAssigneesJson)? originalInstanceState = instance is null ? null : CaptureState(instance);
        var cancelledTasks = new List<WorkflowTask>();
        var cancelledTaskSnapshots = new List<(WorkflowTask Task, (WorkflowTaskStatus Status, string? TransferTarget, string? DecisionComment, string? DecisionActor, DateTime? CompletedAt) State, long Revision)>();
        void RestoreDecisionState()
        {
            task.RestorePersistedState(originalState.Status, originalState.TransferTarget, originalState.DecisionComment, originalState.DecisionActor, originalState.CompletedAt, originalRevision);
            if (instance is not null && originalInstanceState is { } state)
                RestoreState(instance, state);
            foreach (var cancelled in cancelledTaskSnapshots)
                cancelled.Task.RestorePersistedState(cancelled.State.Status, cancelled.State.TransferTarget, cancelled.State.DecisionComment, cancelled.State.DecisionActor, cancelled.State.CompletedAt, cancelled.Revision);
        }
        try
        {
            ExecuteTransaction(() =>
            {
                LockInstanceForDecision(instance);
                var expectedRevision = ClaimTask(task);
                var previousState = CaptureState(task);
                var actionExecuted = ExecuteAction(task, WorkflowActionTrigger.Rejected, comment, actor);
                PersistDecision(task, expectedRevision, previousState, () => task.Reject(actor, comment, completedAt));
                operations?.Record(task, WorkflowOperationKind.Rejected, task.DecisionActor!, task.DecisionComment, $"workflow-task-rejected:{task.Id}", occurredAt: task.CompletedAt);
                cancelledTasks.AddRange(FinishInstance(task, WorkflowInstanceStatus.Rejected, completedAt, actionExecuted, actor, cancelledTaskSnapshots));
            }, RestoreDecisionState);
        }
        catch
        {
            RestoreDecisionState();
            throw;
        }
        ReleaseTerminalRuntimeLock(task);
        QueueNotificationRead(task.Assignee, $"workflow-task:{task.Id}", task.CompletedAt);
        foreach (var cancelled in cancelledTasks)
            QueueNotificationRead(cancelled.Assignee, $"workflow-task:{cancelled.Id}", cancelled.CompletedAt);
    }

    public void Cancel(WorkflowTask task, string actor, string? comment = null, DateTime? completedAt = null)
    {
        EnsureCanDecide(task, actor, comment);
        var originalState = CaptureState(task);
        var originalRevision = task.Revision;
        var instance = FindInstance(task);
        (Guid CurrentNodeId, WorkflowInstanceStatus Status, DateTime? CompletedAt, long Revision, string ActiveNodeIdsJson, string ParallelJoinArrivalsJson, string LoopIterationsJson, string ApprovalAssigneesJson)? originalInstanceState = instance is null ? null : CaptureState(instance);
        var cancelledTasks = new List<WorkflowTask>();
        var cancelledTaskSnapshots = new List<(WorkflowTask Task, (WorkflowTaskStatus Status, string? TransferTarget, string? DecisionComment, string? DecisionActor, DateTime? CompletedAt) State, long Revision)>();
        void RestoreDecisionState()
        {
            task.RestorePersistedState(originalState.Status, originalState.TransferTarget, originalState.DecisionComment, originalState.DecisionActor, originalState.CompletedAt, originalRevision);
            if (instance is not null && originalInstanceState is { } state)
                RestoreState(instance, state);
            foreach (var cancelled in cancelledTaskSnapshots)
                cancelled.Task.RestorePersistedState(cancelled.State.Status, cancelled.State.TransferTarget, cancelled.State.DecisionComment, cancelled.State.DecisionActor, cancelled.State.CompletedAt, cancelled.Revision);
        }
        try
        {
            ExecuteTransaction(() =>
            {
                LockInstanceForDecision(instance);
                var expectedRevision = ClaimTask(task);
                var previousState = CaptureState(task);
                var actionExecuted = ExecuteAction(task, WorkflowActionTrigger.Cancelled, comment, actor);
                PersistDecision(task, expectedRevision, previousState, () => task.Cancel(actor, comment, completedAt));
                operations?.Record(task, WorkflowOperationKind.Cancelled, task.DecisionActor!, task.DecisionComment, $"workflow-task-cancelled:{task.Id}", occurredAt: task.CompletedAt);
                cancelledTasks.AddRange(FinishInstance(task, WorkflowInstanceStatus.Cancelled, completedAt, actionExecuted, actor, cancelledTaskSnapshots));
            }, RestoreDecisionState);
        }
        catch
        {
            RestoreDecisionState();
            throw;
        }
        ReleaseTerminalRuntimeLock(task);
        QueueNotificationRead(task.Assignee, $"workflow-task:{task.Id}", task.CompletedAt);
        foreach (var cancelled in cancelledTasks)
            QueueNotificationRead(cancelled.Assignee, $"workflow-task:{cancelled.Id}", cancelled.CompletedAt);
    }

    public WorkflowTask Transfer(WorkflowTask task, string actor, string targetAssignee, string? comment = null, DateTime? completedAt = null)
    {
        EnsureCanDecide(task, actor, comment);
        if (instances is null) throw new InvalidOperationException("当前未配置流程实例服务，不能转交审批。" );
        var instance = FindInstance(task) ?? throw new InvalidOperationException("流程实例不存在或已被删除。" );
        if (instance.Status != WorkflowInstanceStatus.Running) throw new InvalidOperationException("已结束的流程实例不能转交审批。" );
        var normalizedTarget = targetAssignee?.Trim() ?? string.Empty;
        if (normalizedTarget.Length == 0) throw new ArgumentException("转交审批人不能为空。", nameof(targetAssignee));
        if (normalizedTarget.Length > 200) throw new ArgumentException("转交审批人不能超过 200 个字符。", nameof(targetAssignee));
        var roundTasks = repository.List(task.InstanceId)
            .Where(x => x.NodeId == task.NodeId && x.Round == task.Round)
            .ToArray();
        if (roundTasks.Any(x => x.Status == WorkflowTaskStatus.Pending && x.Assignee.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("该审批节点已经存在相同审批人的待办。" );
        if (roundTasks.Any(x => x.Assignee.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase)
            || x.TransferTarget?.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase) == true))
            throw new InvalidOperationException("转交目标会回到当前审批轮次的历史审批人，可能形成转交循环。" );

        var originalState = CaptureState(task);
        var originalRevision = task.Revision;
        WorkflowTask? transferred = null;
        var createdTaskIds = new List<Guid>();
        void RestoreTransferState()
        {
            task.RestorePersistedState(originalState.Status, originalState.TransferTarget, originalState.DecisionComment, originalState.DecisionActor, originalState.CompletedAt, originalRevision);
            RestoreCreatedTasks(createdTaskIds);
        }
        try
        {
            ExecuteTransaction(() =>
            {
                LockInstanceForDecision(instance);
                var expectedRevision = ClaimTask(task);
                var previousState = CaptureState(task);
                PersistDecision(task, expectedRevision, previousState, () => task.Transfer(actor, normalizedTarget, comment, completedAt));
                operations?.Record(task, WorkflowOperationKind.Transferred, task.DecisionActor!, task.DecisionComment, $"workflow-task-transferred:{task.Id}", targetAssignee: task.TransferTarget, occurredAt: task.CompletedAt);
                transferred = CreateApprovalTaskCoreEntry(instance, task.NodeId, task.NodeName, normalizedTarget, completedAt, task.Round, createdTaskIds);
            }, RestoreTransferState);
        }
        catch
        {
            RestoreTransferState();
            throw;
        }
        QueueNotificationRead(task.Assignee, $"workflow-task:{task.Id}", task.CompletedAt);
        return transferred!;
    }

    /// <summary>
    /// 将当前审批退回到节点配置中声明的历史审批节点。原待办保留为 Returned，
    /// 目标节点会创建新的执行轮次，因而不会覆盖旧的审批意见。
    /// </summary>
    public IReadOnlyList<WorkflowTask> ReturnToNode(WorkflowTask task, string actor, Guid targetNodeId, string? comment = null, DateTime? completedAt = null)
    {
        EnsureCanDecide(task, actor, comment);
        if (instances is null) throw new InvalidOperationException("当前未配置流程实例服务，不能回退审批。");
        if (targetNodeId == Guid.Empty) throw new ArgumentException("回退目标节点不能为空。", nameof(targetNodeId));
        var instance = FindInstance(task) ?? throw new InvalidOperationException("流程实例不存在或已被删除。");
        if (instance.Status != WorkflowInstanceStatus.Running || !instance.ActiveNodeIds.Contains(task.NodeId))
            throw new InvalidOperationException("当前审批待办不再位于流程实例的活动节点。");

        var originalState = CaptureState(task);
        var originalRevision = task.Revision;
        var previousInstanceState = CaptureState(instance);
        var siblingSnapshots = new List<(WorkflowTask Task, (WorkflowTaskStatus Status, string? TransferTarget, string? DecisionComment, string? DecisionActor, DateTime? CompletedAt) State, long Revision)>();
        var cancelledSiblings = new List<WorkflowTask>();
        IReadOnlyList<WorkflowTask> returnedTasks = [];
        void RestoreReturnState()
        {
            task.RestorePersistedState(originalState.Status, originalState.TransferTarget, originalState.DecisionComment, originalState.DecisionActor, originalState.CompletedAt, originalRevision);
            RestoreState(instance, previousInstanceState);
            foreach (var sibling in siblingSnapshots)
                sibling.Task.RestorePersistedState(sibling.State.Status, sibling.State.TransferTarget, sibling.State.DecisionComment, sibling.State.DecisionActor, sibling.State.CompletedAt, sibling.Revision);
        }
        try
        {
            ExecuteTransaction(() =>
            {
                LockInstanceForDecision(instance);
                var expectedRevision = ClaimTask(task);
                var previousState = CaptureState(task);
                PersistDecision(task, expectedRevision, previousState, () => task.Return(actor, comment, completedAt));
                operations?.Record(task, WorkflowOperationKind.Returned, task.DecisionActor!, task.DecisionComment, $"workflow-task-returned:{task.Id}", occurredAt: task.CompletedAt);

                foreach (var sibling in repository.List(task.InstanceId, status: WorkflowTaskStatus.Pending))
                {
                    siblingSnapshots.Add((sibling, CaptureState(sibling), sibling.Revision));
                    PersistPendingCancellation(sibling, task.DecisionActor!, "流程已退回", completedAt);
                    operations?.Record(sibling, WorkflowOperationKind.Cancelled, sibling.DecisionActor!, sibling.DecisionComment, $"workflow-task-cancelled:{sibling.Id}", occurredAt: sibling.CompletedAt);
                    cancelledSiblings.Add(sibling);
                }

                instances.ReturnTo(instance, task.NodeId, targetNodeId);
                returnedTasks = EnsureCurrentApprovalTask(instance, completedAt);
            }, RestoreReturnState);
        }
        catch
        {
            RestoreReturnState();
            throw;
        }

        QueueNotificationRead(task.Assignee, $"workflow-task:{task.Id}", task.CompletedAt);
        foreach (var sibling in cancelledSiblings)
            QueueNotificationRead(sibling.Assignee, $"workflow-task:{sibling.Id}", sibling.CompletedAt);
        return returnedTasks;
    }

    public void Withdraw(Guid instanceId, string actor, string? comment = null, DateTime? completedAt = null)
    {
        if (instances is null) throw new InvalidOperationException("当前未配置流程实例服务，不能撤回流程。");
        var instance = instances.List().SingleOrDefault(x => x.Id == instanceId)
            ?? throw new InvalidOperationException("流程实例不存在或已被删除。");
        EnsureCanWithdraw(instance, actor, comment);

        var normalizedActor = actor.Trim();
        var reason = string.IsNullOrWhiteSpace(comment) ? "流程发起人已撤回" : comment.Trim();
        IReadOnlyList<WorkflowTask> pendingTasks = [];
        var previousInstanceState = (instance.CurrentNodeId, instance.Status, instance.CompletedAt, instance.Revision, instance.ActiveNodeIdsJson, instance.ParallelJoinArrivalsJson, instance.LoopIterationsJson);
        IReadOnlyList<(WorkflowTask Task, (WorkflowTaskStatus Status, string? TransferTarget, string? DecisionComment, string? DecisionActor, DateTime? CompletedAt) State, long Revision)> previousTaskStates = [];
        void RestoreWithdrawState()
        {
            instance.RestorePersistedState(previousInstanceState.CurrentNodeId, previousInstanceState.Status, previousInstanceState.CompletedAt, previousInstanceState.Revision, previousInstanceState.ActiveNodeIdsJson, previousInstanceState.ParallelJoinArrivalsJson, previousInstanceState.LoopIterationsJson);
            foreach (var previous in previousTaskStates)
                previous.Task.RestorePersistedState(previous.State.Status, previous.State.TransferTarget, previous.State.DecisionComment, previous.State.DecisionActor, previous.State.CompletedAt, previous.Revision);
        }
        try
        {
            ExecuteTransaction(() =>
            {
                LockInstanceForDecision(instance);
                instances.Cancel(instance, completedAt);
                // 先以实例 Revision CAS 锁定撤回，再读取待办；否则并发创建待办可能
                // 在撤回快照之外提交，留下已撤回实例上的孤儿 Pending 待办。
                pendingTasks = repository.List(instanceId, status: WorkflowTaskStatus.Pending).ToArray();
                previousTaskStates = pendingTasks
                    .Select(task => (Task: task, State: CaptureState(task), Revision: task.Revision))
                    .ToArray();
                operations?.Record(instance, WorkflowOperationKind.Withdrawn, normalizedActor, reason, $"workflow-instance-withdrawn:{instance.Id}", occurredAt: completedAt);
                foreach (var pending in pendingTasks)
                {
                    PersistPendingCancellation(pending, normalizedActor, reason, completedAt);
                    operations?.Record(pending, WorkflowOperationKind.Cancelled, normalizedActor, reason, $"workflow-task-cancelled:{pending.Id}", occurredAt: pending.CompletedAt);
                }
            }, RestoreWithdrawState);
        }
        catch
        {
            RestoreWithdrawState();
            throw;
        }

        foreach (var pending in pendingTasks)
            QueueNotificationRead(pending.Assignee, $"workflow-task:{pending.Id}", pending.CompletedAt);
        ReleaseTerminalRuntimeLock(instance);
    }

    private static void EnsureActor(WorkflowTask task, string actor)
    {
        if (string.IsNullOrWhiteSpace(actor) || !task.Assignee.Equals(actor.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("只有该审批待办的指定审批人可以处理。");
    }

    private static void EnsureCanWithdraw(WorkflowInstance instance, string actor, string? comment)
    {
        if (instance.Status != WorkflowInstanceStatus.Running) throw new InvalidOperationException("已结束的流程实例不能撤回。");
        if (string.IsNullOrWhiteSpace(actor) || !instance.StartedBy.Equals(actor.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("只有流程发起人可以撤回审批。");
        if (actor.Trim().Length > 200) throw new ArgumentException("审批操作人不能超过 200 个字符。", nameof(actor));
        if (!string.IsNullOrWhiteSpace(comment) && comment.Trim().Length > 2000)
            throw new ArgumentException("审批意见不能超过 2000 个字符。", nameof(comment));
    }

    private void EnsureCanDecide(WorkflowTask task, string actor, string? comment)
    {
        EnsureActor(task, actor);
        task.ValidateDecision(actor, comment);
        var persisted = repository.List(task.InstanceId).SingleOrDefault(x => x.Id == task.Id);
        if (persisted is null || persisted.Status != WorkflowTaskStatus.Pending || persisted.Revision != task.Revision)
            throw new InvalidOperationException("审批待办状态已变化，请刷新后重试。");
        var instance = FindInstance(task);
        var isPreRuntimeStartFixture = instance is not null && instance.ActiveNodeIds.Any(x => instance.GetNodeType(x) == WorkflowNodeType.Start);
        if (instance is not null && (instance.Status != WorkflowInstanceStatus.Running || (!instance.ActiveNodeIds.Contains(task.NodeId) && !isPreRuntimeStartFixture)))
            throw new InvalidOperationException("审批待办已不属于流程实例的活动节点，请刷新后重试。");
    }

    private long ClaimTask(WorkflowTask task)
    {
        if (!repository.TryUpdate(task)) throw new InvalidOperationException("审批待办状态已变化，请刷新后重试。");
        return task.Revision;
    }

    private static (WorkflowTaskStatus Status, string? TransferTarget, string? DecisionComment, string? DecisionActor, DateTime? CompletedAt) CaptureState(WorkflowTask task)
        => (task.Status, task.TransferTarget, task.DecisionComment, task.DecisionActor, task.CompletedAt);

    private static (Guid CurrentNodeId, WorkflowInstanceStatus Status, DateTime? CompletedAt, long Revision, string ActiveNodeIdsJson, string ParallelJoinArrivalsJson, string LoopIterationsJson, string ApprovalAssigneesJson) CaptureState(WorkflowInstance instance)
        => (instance.CurrentNodeId, instance.Status, instance.CompletedAt, instance.Revision, instance.ActiveNodeIdsJson, instance.ParallelJoinArrivalsJson, instance.LoopIterationsJson, instance.ApprovalAssigneesJson);

    private static void RestoreState(WorkflowInstance instance, (Guid CurrentNodeId, WorkflowInstanceStatus Status, DateTime? CompletedAt, long Revision, string ActiveNodeIdsJson, string ParallelJoinArrivalsJson, string LoopIterationsJson, string ApprovalAssigneesJson) state)
        => instance.RestorePersistedState(state.CurrentNodeId, state.Status, state.CompletedAt, state.Revision, state.ActiveNodeIdsJson, state.ParallelJoinArrivalsJson, state.LoopIterationsJson, state.ApprovalAssigneesJson);

    private void RestoreCreatedTasks(IReadOnlyCollection<Guid> createdTaskIds)
    {
        if (repository is not IWorkflowTaskCompensationRepository compensation) return;
        foreach (var taskId in createdTaskIds.Distinct()) compensation.Remove(taskId);
    }

    private void PersistDecision(WorkflowTask task, long expectedRevision, (WorkflowTaskStatus Status, string? TransferTarget, string? DecisionComment, string? DecisionActor, DateTime? CompletedAt) previousState, Action mutation)
    {
        try
        {
            mutation();
        }
        catch
        {
            task.RestorePersistedState(previousState.Status, previousState.TransferTarget, previousState.DecisionComment, previousState.DecisionActor, previousState.CompletedAt, expectedRevision);
            throw;
        }

        if (repository.TryUpdate(task)) return;
        task.RestorePersistedState(previousState.Status, previousState.TransferTarget, previousState.DecisionComment, previousState.DecisionActor, previousState.CompletedAt, expectedRevision);
        throw new InvalidOperationException("审批待办状态已变化，请刷新后重试。");
    }

    private void PersistPendingCancellation(WorkflowTask task, string actor, string reason, DateTime? completedAt)
    {
        EnsurePendingRevision(task);
        var expectedRevision = ClaimTask(task);
        var previousState = CaptureState(task);
        PersistDecision(task, expectedRevision, previousState, () => task.Cancel(actor, reason, completedAt));
    }

    private void CancelSiblingTasksAfterApprovalThreshold(
        WorkflowTask approvedTask,
        DateTime? completedAt,
        ICollection<WorkflowTask> cancelledSiblings,
        ICollection<(WorkflowTask Task, (WorkflowTaskStatus Status, string? TransferTarget, string? DecisionComment, string? DecisionActor, DateTime? CompletedAt) State, long Revision)> siblingSnapshots)
    {
        var instance = FindInstance(approvedTask);
        if (instance is null || !HasReachedApprovalThreshold(instance, approvedTask))
            return;

        foreach (var sibling in repository.List(approvedTask.InstanceId, status: WorkflowTaskStatus.Pending).Where(x => x.NodeId == approvedTask.NodeId))
        {
            siblingSnapshots.Add((sibling, CaptureState(sibling), sibling.Revision));
            PersistPendingCancellation(sibling, approvedTask.DecisionActor ?? "system", "同节点审批已达到通过门槛", completedAt);
            operations?.Record(sibling, WorkflowOperationKind.Cancelled, sibling.DecisionActor!, sibling.DecisionComment, $"workflow-task-cancelled:{sibling.Id}", occurredAt: sibling.CompletedAt);
            cancelledSiblings.Add(sibling);
        }
    }

    private bool HasReachedApprovalThreshold(WorkflowInstance instance, WorkflowTask approvedTask)
    {
        var mode = WorkflowApprovalConfiguration.ParseMode(instance.GetNodeConfig(approvedTask.NodeId));
        if (mode == WorkflowApprovalMode.All) return false;

        var roundTasks = repository.List(approvedTask.InstanceId)
            .Where(x => x.NodeId == approvedTask.NodeId && x.Round == approvedTask.Round)
            .ToArray();
        var approvedCount = roundTasks.Count(x => x.Status == WorkflowTaskStatus.Approved);
        if (mode == WorkflowApprovalMode.Any) return approvedCount > 0;

        var configuredCount = instance.GetApprovalAssignees(approvedTask.NodeId).Count;
        var voterCount = configuredCount > 0
            ? configuredCount
            : roundTasks.Count(x => x.Status != WorkflowTaskStatus.Transferred);
        if (voterCount == 0) return false;
        return mode switch
        {
            WorkflowApprovalMode.Majority => approvedCount > voterCount / 2,
            WorkflowApprovalMode.Quorum => approvedCount >= GetQuorum(instance, approvedTask.NodeId, voterCount),
            _ => false
        };
    }

    private static int GetQuorum(WorkflowInstance instance, Guid nodeId, int voterCount)
    {
        var required = WorkflowApprovalConfiguration.GetRequiredApprovals(instance.GetNodeConfig(nodeId));
        if (required > voterCount)
            throw new InvalidOperationException("Quorum 所需同意人数不能超过当前审批人数量。");
        return required;
    }

    private void EnsurePendingRevision(WorkflowTask task)
    {
        var persisted = repository.List(task.InstanceId).SingleOrDefault(x => x.Id == task.Id);
        if (persisted is null || persisted.Status != WorkflowTaskStatus.Pending || persisted.Revision != task.Revision)
            throw new InvalidOperationException("审批待办状态已变化，请刷新后重试。");
    }

    private void LockInstanceForDecision(WorkflowInstance? instance)
    {
        // 数据库行锁必须运行在当前 Workflow 事务内；无事务内存宿主继续依赖待办/实例 CAS。
        if (instance is not null && instances is not null && transactions is not null)
            instances.LockForUpdate(instance);
    }

    private void ExecuteTransaction(Action operation, Action? restoreAfterRollback = null)
    {
        if (transactions is null) operation();
        else transactions.Execute(operation, restoreAfterRollback is null ? null : _ => restoreAfterRollback());
    }

    private bool ExecuteFinalAction(WorkflowTask task, WorkflowActionTrigger trigger, string? reason, string actor)
    {
        if (repository.List(task.InstanceId, status: WorkflowTaskStatus.Pending).Count != 1) return false;
        return ExecuteAction(task, trigger, reason, actor);
    }

    private bool ExecuteAction(WorkflowTask task, WorkflowActionTrigger trigger, string? reason, string? actor = null)
    {
        var instance = FindInstance(task);
        if (instance is null || instance.Status != WorkflowInstanceStatus.Running) return false;
        var resolvedActionExecutor = ResolvedActionExecutor;
        if (resolvedActionExecutor is null) return false;

        return resolvedActionExecutor.Execute(instance, task.NodeId, trigger, reason, actor);
    }

    private void CompleteInstanceIfNoPending(WorkflowTask task, bool actionExecuted, string actor)
    {
        if (instances is null || repository.List(task.InstanceId, status: WorkflowTaskStatus.Pending).Any(x => x.NodeId == task.NodeId)) return;
        var instance = FindInstance(task);
        if (instance is null || instance.Status != WorkflowInstanceStatus.Running) return;
        if (!actionExecuted) ExecuteAction(task, WorkflowActionTrigger.Approved, task.DecisionComment, actor);
        if (TryAdvanceAfterApproval(instance, task.NodeId, task.CompletedAt, actor)) return;
        instances.Complete(instance, task.CompletedAt);
    }

    private bool TryAdvanceAfterApproval(WorkflowInstance instance, Guid completedNodeId, DateTime? completedAt, string actor)
    {
        // 没有待办不代表当前节点就是刚完成的节点；并发/重试场景下不能因此把实例误标记为完成。
        var resolvedRuntime = ResolvedRuntime;
        if (resolvedRuntime is not null)
        {
            if (!instance.ActiveNodeIds.Contains(completedNodeId)) return true;
            var result = resolvedRuntime.ContinueAfterApproval(instance, completedNodeId, occurredAt: completedAt, actor: actor);
            if (result.State == WorkflowRuntimeState.WaitingForApproval) EnsureCurrentApprovalTask(instance, completedAt);
            return true;
        }
        if (instance.CurrentNodeId != completedNodeId) return instance.GetNodeType(instance.CurrentNodeId) != WorkflowNodeType.Start;
        var transition = instance.GetOutgoingTransitions(completedNodeId).SingleOrDefault(x => x.ConditionKey is null);
        if (transition is null) return true;
        var targetType = instance.GetNodeType(transition.TargetNodeId);
        if (targetType is WorkflowNodeType.Condition or WorkflowNodeType.Notification or WorkflowNodeType.BusinessAction)
        {
            instances!.Advance(instance, transition.TargetNodeId, transition.ConditionKey);
            return true;
        }
        if (targetType is not (WorkflowNodeType.Approval or WorkflowNodeType.End)) return false;

        instances!.Advance(instance, transition.TargetNodeId, transition.ConditionKey);
        switch (targetType)
        {
            case WorkflowNodeType.Approval:
                EnsureCurrentApprovalTask(instance, completedAt);
                return true;
            case WorkflowNodeType.End:
                instances.Complete(instance, completedAt);
                return true;
            default:
                return false;
        }
    }

    private IReadOnlyList<WorkflowTask> FinishInstance(
        WorkflowTask task,
        WorkflowInstanceStatus status,
        DateTime? completedAt,
        bool actionExecuted,
        string actor,
        ICollection<(WorkflowTask Task, (WorkflowTaskStatus Status, string? TransferTarget, string? DecisionComment, string? DecisionActor, DateTime? CompletedAt) State, long Revision)> siblingSnapshots)
    {
        if (instances is null) return [];
        var instance = FindInstance(task);
        if (instance is null) return [];
        if (instance.Status == WorkflowInstanceStatus.Running)
        {
            if (status == WorkflowInstanceStatus.Rejected)
            {
                if (!actionExecuted) ExecuteAction(task, WorkflowActionTrigger.Rejected, task.DecisionComment, actor);
                instances.Reject(instance, completedAt);
            }
            else
            {
                if (!actionExecuted) ExecuteAction(task, WorkflowActionTrigger.Cancelled, task.DecisionComment, actor);
                instances.Cancel(instance, completedAt);
            }
        }

        var cancellationActor = task.DecisionActor ?? actor;
        var cancelledTasks = repository.List(task.InstanceId, status: WorkflowTaskStatus.Pending).ToArray();
        foreach (var pending in cancelledTasks)
        {
            siblingSnapshots.Add((pending, CaptureState(pending), pending.Revision));
            PersistPendingCancellation(pending, cancellationActor, "流程实例已终止", completedAt);
            operations?.Record(pending, WorkflowOperationKind.Cancelled, cancellationActor, "流程实例已终止", $"workflow-task-cancelled:{pending.Id}", occurredAt: pending.CompletedAt);
        }
        return cancelledTasks;
    }

    private void ReleaseTerminalRuntimeLock(WorkflowTask task)
    {
        var instance = FindInstance(task);
        ReleaseTerminalRuntimeLock(instance);
    }

    private void ReleaseTerminalRuntimeLock(WorkflowInstance? instance)
    {
        var resolvedRuntime = ResolvedRuntime;
        if (instance is null || resolvedRuntime is null) return;
        if (transactions is null)
        {
            resolvedRuntime.ReleaseTerminalInstanceLock(instance);
            return;
        }

        // 审批终止动作可能嵌套在业务外层事务中；锁必须等最外层提交后释放，
        // 否则外层回滚到 Running 时会丢失进程内串行化保护。
        transactions.Execute(static () => { }, afterRollback: null, afterCommit: () => resolvedRuntime.ReleaseTerminalInstanceLock(instance));
    }

    private WorkflowInstance? FindInstance(WorkflowTask task)
        => instances?.List(task.BusinessType, task.BusinessId).SingleOrDefault(x => x.Id == task.InstanceId);
}
