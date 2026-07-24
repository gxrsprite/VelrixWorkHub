using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class WorkflowBindingServiceTests
{
    [Fact]
    public void StartOrGet_UsesLatestPublishedVersionAndIsIdempotent()
    {
        var definitionRepository = new DefinitionRepository();
        var instanceRepository = new InstanceRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var instances = new WorkflowInstanceService(instanceRepository);
        var binding = new WorkflowBindingService(definitions, instances);
        var businessId = Guid.CreateVersion7();

        var first = definitions.CreateDraft(WorkflowBindingCodes.ContractApproval, "合同审批");
        AddLinearGraph(first);
        definitions.Publish(first);
        var second = definitions.CreateDraft(WorkflowBindingCodes.ContractApproval, "合同审批 V2");
        AddLinearGraph(second);
        definitions.Publish(second);

        var started = binding.StartOrGet(WorkflowBindingCodes.ContractApproval, "SalesContract", businessId, startedBy: "admin");
        var repeated = binding.StartOrGet(WorkflowBindingCodes.ContractApproval, "SalesContract", businessId);

        Assert.Equal(second.VersionNumber, started.DefinitionVersion);
        Assert.Equal("admin", started.StartedBy);
        Assert.Equal(second.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id, started.CurrentNodeId);
        Assert.Same(started, repeated);
        Assert.Equal(1, instanceRepository.AddCount);
    }

    [Fact]
    public void StartOrGet_RequiresPublishedDefinitionAndSeparatesBusinessObjects()
    {
        var definitionRepository = new DefinitionRepository();
        var instanceRepository = new InstanceRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var binding = new WorkflowBindingService(definitions, new WorkflowInstanceService(instanceRepository));

        Assert.Throws<InvalidOperationException>(() => binding.StartOrGet(WorkflowBindingCodes.SettlementApproval, "ErpSettlement", Guid.CreateVersion7()));

        var draft = definitions.CreateDraft(WorkflowBindingCodes.SettlementApproval, "核销审批");
        AddLinearGraph(draft);
        definitions.Publish(draft);
        var first = binding.StartOrGet(WorkflowBindingCodes.SettlementApproval, "ErpSettlement", Guid.CreateVersion7());
        var second = binding.StartOrGet(WorkflowBindingCodes.SettlementApproval, "ErpSettlement", Guid.CreateVersion7());

        Assert.NotEqual(first.BusinessId, second.BusinessId);
        Assert.Single(binding.List("ErpSettlement", first.BusinessId));
    }

    [Fact]
    public void StartOrGet_ReturnsWinnerWhenDatabaseReportsRunningInstanceConflict()
    {
        var definitionRepository = new DefinitionRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var definition = definitions.CreateDraft(WorkflowBindingCodes.ContractApproval, "合同审批");
        AddLinearGraph(definition);
        definitions.Publish(definition);
        var instances = new ConflictThenWinnerInstanceRepository();
        var binding = new WorkflowBindingService(definitions, new WorkflowInstanceService(instances));
        var businessId = Guid.CreateVersion7();

        var result = binding.StartOrGet(WorkflowBindingCodes.ContractApproval, "SalesContract", businessId, startedBy: "admin");

        Assert.Same(instances.Winner, result);
        Assert.Equal(1, instances.AddAttempts);
        Assert.Equal(WorkflowInstanceStatus.Running, result.Status);
    }

    [Fact]
    public void Resubmit_ReturnsWinnerWhenDatabaseReportsRunningInstanceConflict()
    {
        var definitionRepository = new DefinitionRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var definition = definitions.CreateDraft(WorkflowBindingCodes.ContractApproval, "合同审批");
        AddLinearGraph(definition);
        definitions.Publish(definition);
        var instances = new ConflictThenWinnerInstanceRepository();
        var businessId = Guid.CreateVersion7();
        var previous = WorkflowInstance.Start(definition, "SalesContract", businessId, startedBy: "admin");
        previous.Reject();
        instances.Seed(previous);
        var binding = new WorkflowBindingService(definitions, new WorkflowInstanceService(instances));

        var result = binding.Resubmit(WorkflowBindingCodes.ContractApproval, "SalesContract", businessId, startedBy: "admin");

        Assert.Same(instances.Winner, result);
        Assert.Equal(previous.Id, result.PreviousInstanceId);
        Assert.Equal(1, instances.AddAttempts);
    }

    [Fact]
    public void StartOrGet_AfterRejectedInstanceCreatesLinkedResubmissionForInitiator()
    {
        var definitionRepository = new DefinitionRepository();
        var instanceRepository = new InstanceRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var instances = new WorkflowInstanceService(instanceRepository);
        var binding = new WorkflowBindingService(definitions, instances);
        var definition = definitions.CreateDraft(WorkflowBindingCodes.ContractApproval, "合同审批");
        AddLinearGraph(definition);
        definitions.Publish(definition);
        var businessId = Guid.CreateVersion7();
        var first = binding.StartOrGet(WorkflowBindingCodes.ContractApproval, "SalesContract", businessId, startedBy: "admin");
        instances.Reject(first, new DateTime(2026, 7, 15, 10, 0, 0));

        Assert.Throws<InvalidOperationException>(() => binding.StartOrGet(WorkflowBindingCodes.ContractApproval, "SalesContract", businessId, startedBy: "finance"));
        var second = binding.StartOrGet(WorkflowBindingCodes.ContractApproval, "SalesContract", businessId, startedBy: "admin");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Id, second.PreviousInstanceId);
        Assert.Equal(WorkflowInstanceStatus.Running, second.Status);
        Assert.Equal(2, instanceRepository.AddCount);
    }

    [Fact]
    public void StartOrGet_WithRuntimeActivatesOnlyCurrentApprovalTask()
    {
        var definitionRepository = new DefinitionRepository();
        var instanceRepository = new InstanceRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var instances = new WorkflowInstanceService(instanceRepository);
        var taskRepository = new TaskRepository();
        var runtime = new WorkflowRuntimeService(instances, new WorkflowActionExecutor([]), new NotificationService(new NotificationRepository()));
        var tasks = new WorkflowTaskService(taskRepository, instances, runtime: runtime);
        var binding = new WorkflowBindingService(definitions, instances, tasks, runtime);
        var definition = definitions.CreateDraft(WorkflowBindingCodes.ContractApproval, "运行时绑定");
        AddLinearGraph(definition);
        definitions.Publish(definition);

        var instance = binding.StartOrGet(WorkflowBindingCodes.ContractApproval, "SalesContract", Guid.CreateVersion7(), startedBy: "admin");

        Assert.Equal(WorkflowNodeType.Approval, instance.GetNodeType(instance.CurrentNodeId));
        Assert.Single(taskRepository.Items);
        Assert.Equal(instance.CurrentNodeId, taskRepository.Items[0].NodeId);
    }

    [Fact]
    public void StartOrGet_WhenInitialTaskPreparationFails_RemovesCreatedInstance()
    {
        var definitionRepository = new DefinitionRepository();
        var instanceRepository = new InstanceRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var definition = definitions.CreateDraft("BINDING_START_COMPENSATION", "启动实例补偿");
        AddLinearGraph(definition);
        definitions.Publish(definition);
        var boundary = new NestedRollbackTransactionBoundary();
        var instances = new WorkflowInstanceService(instanceRepository, transactions: boundary);
        var approval = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
        var taskRepository = new ThrowingTaskRepository(approval.Id);
        var tasks = new WorkflowTaskService(taskRepository, instances, transactions: boundary);
        var binding = new WorkflowBindingService(definitions, instances, tasks, transactions: boundary);

        Assert.Throws<InvalidOperationException>(() => binding.StartOrGet(definition.Code, "custom.document", Guid.CreateVersion7(), startedBy: "admin"));

        Assert.Empty(instanceRepository.List());
        Assert.Empty(taskRepository.Items);
    }

    [Fact]
    public void StartOrGet_WhenInitialTaskPreparationFailsWithoutTransaction_RemovesCreatedInstance()
    {
        var definitionRepository = new DefinitionRepository();
        var instanceRepository = new InstanceRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var definition = definitions.CreateDraft("BINDING_START_NO_TRANSACTION_COMPENSATION", "无事务启动实例补偿");
        AddLinearGraph(definition);
        definitions.Publish(definition);
        var instances = new WorkflowInstanceService(instanceRepository);
        var approval = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
        var tasks = new WorkflowTaskService(new ThrowingTaskRepository(approval.Id), instances);
        var binding = new WorkflowBindingService(definitions, instances, tasks);

        Assert.Throws<InvalidOperationException>(() => binding.StartOrGet(definition.Code, "custom.document", Guid.CreateVersion7(), startedBy: "admin"));

        Assert.Empty(instanceRepository.List());
    }

    [Fact]
    public void StartOrGet_WhenWinnerPreparationFails_DoesNotRemoveWinnerInstance()
    {
        var definitionRepository = new DefinitionRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var definition = definitions.CreateDraft("BINDING_WINNER_COMPENSATION", "胜者实例保护");
        AddLinearGraph(definition);
        definitions.Publish(definition);
        var winner = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "winner");
        var instanceRepository = new ExistingWinnerInstanceRepository(winner);
        var instances = new WorkflowInstanceService(instanceRepository);
        var approval = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
        var tasks = new WorkflowTaskService(new ThrowingTaskRepository(approval.Id), instances);
        var binding = new WorkflowBindingService(definitions, instances, tasks);

        Assert.Throws<InvalidOperationException>(() => binding.StartOrGet(definition.Code, winner.BusinessType, winner.BusinessId, startedBy: "contender"));

        Assert.Same(winner, instanceRepository.List().Single());
        Assert.Empty(instanceRepository.RemovedIds);
    }

    [Fact]
    public void StartOrGet_WhenRepairingRunningInstanceTaskFails_RestoresApprovalSnapshot()
    {
        var definitionRepository = new DefinitionRepository();
        var instanceRepository = new InstanceRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var definition = definitions.CreateDraft("BINDING_REPAIR_ROLLBACK", "运行实例补偿回滚");
        AddLinearGraph(definition);
        definitions.Publish(definition);
        var boundary = new NestedRollbackTransactionBoundary();
        var instances = new WorkflowInstanceService(instanceRepository, transactions: boundary);
        var instance = instances.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var runtime = new WorkflowRuntimeService(instances, new WorkflowActionExecutor([]), new NotificationService(new NotificationRepository()), transactions: boundary);
        runtime.Continue(instance);
        var approval = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
        var taskRepository = new ThrowingTaskRepository(approval.Id);
        var tasks = new WorkflowTaskService(taskRepository, instances, runtime: runtime, transactions: boundary);
        var binding = new WorkflowBindingService(definitions, instances, tasks, runtime, boundary);

        Assert.Throws<InvalidOperationException>(() => binding.StartOrGet(definition.Code, "custom.document", instance.BusinessId, startedBy: "admin"));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(approval.Id, instance.CurrentNodeId);
        Assert.Equal("{}", instance.ApprovalAssigneesJson);
        Assert.Empty(taskRepository.Items);
    }

    [Fact]
    public void ApprovalSnapshot_ReusesConcurrentWinnerAfterCasConflict()
    {
        var definitionRepository = new DefinitionRepository();
        var instanceRepository = new SnapshotWinnerInstanceRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var definition = definitions.CreateDraft("APPROVAL_SNAPSHOT_CONCURRENT", "并发审批人快照");
        AddLinearGraph(definition);
        definitions.Publish(definition);
        var instances = new WorkflowInstanceService(instanceRepository);
        var instance = instances.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var approval = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
        instances.Advance(instance, approval.Id);

        var winner = WorkflowInstance.Rehydrate(
            instance.Id,
            instance.DefinitionId,
            instance.DefinitionCode,
            instance.DefinitionVersion,
            instance.BusinessType,
            instance.BusinessId,
            instance.StartedBy,
            instance.DefinitionSnapshotJson,
            instance.Status,
            instance.CurrentNodeId,
            instance.StartedAt,
            instance.CompletedAt,
            instance.PreviousInstanceId,
            instance.Revision,
            instance.ActiveNodeIdsJson,
            instance.ParallelJoinArrivalsJson,
            instance.LoopIterationsJson,
            instance.ApprovalAssigneesJson);
        winner.CaptureApprovalAssignees(approval.Id, ["winner"]);
        winner.MarkPersistedRevision(instance.Revision + 1);
        instanceRepository.ArmWinner(winner);

        var assignees = instances.EnsureApprovalAssigneeSnapshot(instance, approval.Id, ["stale"]);

        Assert.Equal(["winner"], assignees);
        Assert.Equal(3, instance.Revision);
        Assert.Equal(["winner"], instance.GetApprovalAssignees(approval.Id));
    }

    [Fact]
    public void StartOrGet_WithoutRuntimeRejectsGraphRuntimeNodesBeforeCreatingInstance()
    {
        var definitionRepository = new DefinitionRepository();
        var instanceRepository = new InstanceRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var binding = new WorkflowBindingService(definitions, new WorkflowInstanceService(instanceRepository));
        var definition = definitions.CreateDraft("BINDING_RUNTIME_REQUIRED", "运行时注入保护");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var notification = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "通知", configJson: "{\"recipients\":\"admin\",\"content\":\"请处理\"}");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"owner\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, notification.Id);
        definition.Connect(notification.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definitions.Publish(definition);

        var error = Assert.Throws<InvalidOperationException>(() => binding.StartOrGet(definition.Code, "SalesContract", Guid.CreateVersion7()));

        Assert.Contains("WorkflowRuntimeService", error.Message);
        Assert.Equal(0, instanceRepository.AddCount);
    }

    [Fact]
    public void StartOrGet_WithoutRuntimeContinuesRunningLegacyVersionAfterComplexVersionIsPublished()
    {
        var definitionRepository = new DefinitionRepository();
        var instanceRepository = new InstanceRepository();
        var definitions = new WorkflowDefinitionService(definitionRepository);
        var binding = new WorkflowBindingService(definitions, new WorkflowInstanceService(instanceRepository));
        var first = definitions.CreateDraft(WorkflowBindingCodes.ContractApproval, "线性审批");
        AddLinearGraph(first);
        definitions.Publish(first);
        var businessId = Guid.CreateVersion7();
        var running = binding.StartOrGet(first.Code, "SalesContract", businessId, startedBy: "admin");

        var second = definitions.CreateDraft(WorkflowBindingCodes.ContractApproval, "复杂审批");
        var start = second.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var notification = second.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "通知", configJson: "{\"recipients\":\"admin\",\"content\":\"请处理\"}");
        var approval = second.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"owner\"}");
        var end = second.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        second.Connect(start.Id, notification.Id);
        second.Connect(notification.Id, approval.Id);
        second.Connect(approval.Id, end.Id);
        definitions.Publish(second);

        var repeated = binding.StartOrGet(second.Code, "SalesContract", businessId);

        Assert.Same(running, repeated);
        Assert.Equal(first.VersionNumber, repeated.DefinitionVersion);
        Assert.Equal(1, instanceRepository.AddCount);
    }

    private static void AddLinearGraph(WorkflowDefinition definition)
    {
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"owner\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
    }

    private sealed class DefinitionRepository : IWorkflowDefinitionRepository
    {
        private readonly List<WorkflowDefinition> items = [];
        public IReadOnlyList<WorkflowDefinition> List(string? code = null, WorkflowDefinitionStatus? status = null) => items.Where(x => (code is null || x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) && (status is null || x.Status == status)).ToArray();
        public void Add(WorkflowDefinition definition) => items.Add(definition);
        public bool TryAdd(WorkflowDefinition definition)
        {
            if (items.Any(x => x.Id == definition.Id || (x.Code.Equals(definition.Code, StringComparison.OrdinalIgnoreCase) && x.VersionNumber == definition.VersionNumber))) return false;
            Add(definition);
            return true;
        }
        public void Update(WorkflowDefinition definition) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InstanceRepository : IWorkflowInstanceRepository, IWorkflowInstanceCompensationRepository
    {
        private readonly List<WorkflowInstance> items = [];
        public int AddCount { get; private set; }
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null) => items.Where(x => (businessType is null || x.BusinessType == businessType) && (businessId is null || x.BusinessId == businessId) && (status is null || x.Status == status)).ToArray();
        public void Add(WorkflowInstance instance) { items.Add(instance); AddCount++; }
        public bool TryAdd(WorkflowInstance instance) { Add(instance); return true; }
        public void Remove(Guid instanceId) => items.RemoveAll(x => x.Id == instanceId);
        public void Update(WorkflowInstance instance) { }
        public bool TryUpdate(WorkflowInstance instance) { var nextRevision = checked(instance.Revision + 1); Update(instance); instance.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class ExistingWinnerInstanceRepository(WorkflowInstance winner) : IWorkflowInstanceRepository, IWorkflowInstanceCompensationRepository
    {
        public List<Guid> RemovedIds { get; } = [];
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
            => (businessType is null || winner.BusinessType == businessType)
                && (businessId is null || winner.BusinessId == businessId)
                && (status is null || winner.Status == status)
                ? [winner]
                : [];
        public void Add(WorkflowInstance instance) => throw new NotSupportedException();
        public bool TryAdd(WorkflowInstance instance) => false;
        public void Remove(Guid instanceId) => RemovedIds.Add(instanceId);
        public void Update(WorkflowInstance instance) { }
        public bool TryUpdate(WorkflowInstance instance) { var nextRevision = checked(instance.Revision + 1); instance.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class ConflictThenWinnerInstanceRepository : IWorkflowInstanceRepository
    {
        private readonly List<WorkflowInstance> items = [];
        public WorkflowInstance? Winner { get; private set; }
        public int AddAttempts { get; private set; }
        public void Seed(WorkflowInstance instance) => items.Add(instance);
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
            => items.Where(x => (businessType is null || x.BusinessType == businessType) && (businessId is null || x.BusinessId == businessId) && (status is null || x.Status == status)).ToArray();
        public void Add(WorkflowInstance instance)
        {
            AddAttempts++;
            Winner = instance;
            items.Add(instance);
            throw new WorkflowRunningInstanceConflictException();
        }
        public bool TryAdd(WorkflowInstance instance) { Add(instance); return true; }
        public void Update(WorkflowInstance instance) { }
        public bool TryUpdate(WorkflowInstance instance) { var nextRevision = checked(instance.Revision + 1); Update(instance); instance.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class SnapshotWinnerInstanceRepository : IWorkflowInstanceRepository
    {
        private readonly List<WorkflowInstance> items = [];
        private WorkflowInstance? winner;

        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
            => items.Where(x => businessType is null || x.BusinessType == businessType)
                .Where(x => businessId is null || x.BusinessId == businessId)
                .Where(x => status is null || x.Status == status)
                .ToArray();

        public void Add(WorkflowInstance instance) => items.Add(instance);

        public bool TryAdd(WorkflowInstance instance)
        {
            if (items.Any(x => x.Id == instance.Id)) return false;
            Add(instance);
            return true;
        }

        public void ArmWinner(WorkflowInstance instance) => winner = instance;

        public void Update(WorkflowInstance instance) { }

        public bool TryUpdate(WorkflowInstance instance)
        {
            if (winner is not null)
            {
                items.RemoveAll(x => x.Id == instance.Id);
                items.Add(winner);
                winner = null;
                return false;
            }

            var nextRevision = checked(instance.Revision + 1);
            instance.MarkPersistedRevision(nextRevision);
            return true;
        }
    }

    private sealed class TaskRepository : IWorkflowTaskRepository
    {
        public List<WorkflowTask> Items { get; } = [];
        public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
            => Items.Where(x => instanceId is null || x.InstanceId == instanceId).Where(x => assignee is null || x.Assignee.Equals(assignee, StringComparison.OrdinalIgnoreCase)).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowTask task) => Items.Add(task);
        public bool TryAdd(WorkflowTask task) { if (Items.Any(x => x.Id == task.Id)) return false; Add(task); return true; }
        public void Update(WorkflowTask task) { }
        public bool TryUpdate(WorkflowTask task) { var nextRevision = checked(task.Revision + 1); Update(task); task.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class ThrowingTaskRepository(Guid nodeId) : IWorkflowTaskRepository
    {
        public List<WorkflowTask> Items { get; } = [];
        public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
            => Items.Where(x => instanceId is null || x.InstanceId == instanceId).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowTask task)
        {
            if (task.NodeId == nodeId) throw new InvalidOperationException("补偿待办写入失败");
            Items.Add(task);
        }
        public bool TryAdd(WorkflowTask task) { Add(task); return true; }
        public void Update(WorkflowTask task) { }
        public bool TryUpdate(WorkflowTask task) { var nextRevision = checked(task.Revision + 1); Update(task); task.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class NestedRollbackTransactionBoundary : IWorkflowTransactionBoundary
    {
        private readonly Stack<List<Action<Exception>>> scopes = [];

        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            var callbacks = new List<Action<Exception>>();
            if (afterRollback is not null) callbacks.Add(afterRollback);
            scopes.Push(callbacks);
            try
            {
                operation();
            }
            catch (Exception exception)
            {
                scopes.Pop();
                foreach (var callback in callbacks.AsEnumerable().Reverse().ToArray()) callback(exception);
                throw;
            }

            scopes.Pop();
            if (scopes.Count > 0) scopes.Peek().AddRange(callbacks);
        }
    }

    private sealed class NotificationRepository : INotificationRepository
    {
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => [];
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => null;
        public void Add(WorkNotification notification) { }
        public bool TryAdd(WorkNotification notification) => true;
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }
}
