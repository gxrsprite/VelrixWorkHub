using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class WorkflowOperationServiceTests
{
    [Fact]
    public void Record_IsIdempotentByDedupeKeyAndListsByInstance()
    {
        var repository = new InMemoryOperationRepository();
        var service = new WorkflowOperationService(repository);
        var instance = StartInstance();

        var first = service.Record(instance, WorkflowOperationKind.Started, "admin", "发起审批", "workflow-instance-started:1", occurredAt: new DateTime(2026, 7, 15, 10, 0, 0));
        Assert.Equal(0, repository.FindCount);
        var second = service.Record(instance, WorkflowOperationKind.Started, "admin", "重复调用", "workflow-instance-started:1", occurredAt: new DateTime(2026, 7, 15, 10, 1, 0));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, repository.FindCount);
        Assert.Single(service.List(instanceId: instance.Id));
        Assert.Equal("发起审批", first.Comment);
    }

    [Fact]
    public void Record_WhenUniqueInsertRaces_ReturnsConcurrentExistingOperation()
    {
        var repository = new RacingOperationRepository();
        var service = new WorkflowOperationService(repository);
        var instance = StartInstance();

        var recorded = service.Record(instance, WorkflowOperationKind.NodeExecuted, "system", "节点完成", "workflow-node-executed:racing", nodeId: instance.CurrentNodeId);

        Assert.Same(repository.ConcurrentOperation, recorded);
    }

    [Fact]
    public void TryRecord_WhenUniqueInsertRaces_ReturnsConcurrentExistingOperationWithoutDuplicate()
    {
        var repository = new RacingOperationRepository();
        var service = new WorkflowOperationService(repository);
        var instance = StartInstance();

        var inserted = service.TryRecord(instance, WorkflowOperationKind.NodeExecuted, "system", "节点完成", "workflow-node-executed:atomic", out var operation, nodeId: instance.CurrentNodeId);

        Assert.False(inserted);
        Assert.Same(repository.ConcurrentOperation, operation);
        Assert.Single(service.List(instanceId: instance.Id));
    }

    [Fact]
    public void TaskService_WritesStartAssignmentTransferAndApprovalHistory()
    {
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceRepository = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository, operations);
        var taskRepository = new InMemoryTaskRepository();
        var taskService = new WorkflowTaskService(taskRepository, instanceService, operations: operations);
        var instance = instanceService.Start(CreateDefinition(), nameof(SalesContract), Guid.CreateVersion7(), new DateTime(2026, 7, 15, 10, 0, 0), "admin");
        var nodeId = CreateDefinitionNodeId(instance);
        var original = taskService.CreateApprovalTask(instance, nodeId, "审批", "admin", new DateTime(2026, 7, 15, 10, 1, 0));

        var transferred = taskService.Transfer(original, "admin", "finance", "请财务复核", new DateTime(2026, 7, 15, 10, 2, 0));
        taskService.Approve(transferred, "finance", "已复核", new DateTime(2026, 7, 15, 10, 3, 0));

        var history = operations.List(instanceId: instance.Id);
        Assert.Contains(history, x => x.Kind == WorkflowOperationKind.Started && x.Actor == "admin");
        Assert.Contains(history, x => x.Kind == WorkflowOperationKind.Assigned && x.TaskId == original.Id);
        Assert.Contains(history, x => x.Kind == WorkflowOperationKind.Transferred && x.TaskId == original.Id && x.TargetAssignee == "finance");
        Assert.Contains(history, x => x.Kind == WorkflowOperationKind.Assigned && x.TaskId == transferred.Id && x.Actor == "finance");
        Assert.Contains(history, x => x.Kind == WorkflowOperationKind.Approved && x.TaskId == transferred.Id && x.Comment == "已复核");
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public void Resubmission_WritesResubmittedOperation()
    {
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceRepository = new InMemoryInstanceRepository();
        var instances = new WorkflowInstanceService(instanceRepository, operations);
        var definition = CreateDefinition();
        var first = instances.Start(definition, nameof(SalesContract), Guid.CreateVersion7(), startedBy: "admin");
        instances.Reject(first, new DateTime(2026, 7, 15, 11, 0, 0));
        var second = instances.Start(definition, nameof(SalesContract), first.BusinessId, new DateTime(2026, 7, 15, 11, 1, 0), "admin", first.Id);

        Assert.Equal(first.Id, second.PreviousInstanceId);
        Assert.Contains(operations.List(instanceId: second.Id), x => x.Kind == WorkflowOperationKind.Resubmitted && x.Comment == "重新提交审批");
    }

    [Fact]
    public void InstanceService_Advance_WritesNodeExecutionHistory()
    {
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceRepository = new InMemoryInstanceRepository();
        var instances = new WorkflowInstanceService(instanceRepository, operations);
        var definition = CreateDefinition();
        var instance = instances.Start(definition, nameof(SalesContract), Guid.CreateVersion7(), startedBy: "admin");
        var approvalNodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id;

        instances.Advance(instance, approvalNodeId);

        var history = operations.List(instanceId: instance.Id);
        Assert.Equal(approvalNodeId, instance.CurrentNodeId);
        Assert.Contains(history, x => x.Kind == WorkflowOperationKind.NodeCompleted && x.NodeId == definition.Nodes.Single(x => x.Type == WorkflowNodeType.Start).Id);
        Assert.Contains(history, x => x.Kind == WorkflowOperationKind.NodeEntered && x.NodeId == approvalNodeId);
    }

    private static WorkflowInstance StartInstance()
        => WorkflowInstance.Start(CreateDefinition(), nameof(SalesContract), Guid.CreateVersion7(), startedBy: "admin");

    private static WorkflowDefinition CreateDefinition()
    {
        var definition = new WorkflowDefinition("OPERATION_TEST", "操作历史测试");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private static Guid CreateDefinitionNodeId(WorkflowInstance instance)
    {
        using var document = System.Text.Json.JsonDocument.Parse(instance.DefinitionSnapshotJson);
        var nodes = document.RootElement.EnumerateObject().Single(x => x.Name.Equals("Nodes", StringComparison.OrdinalIgnoreCase)).Value;
        var node = nodes.EnumerateArray().Single(x => x.EnumerateObject().Single(p => p.Name.Equals("Type", StringComparison.OrdinalIgnoreCase)).Value.GetString() == nameof(WorkflowNodeType.Approval));
        return node.EnumerateObject().Single(x => x.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)).Value.GetGuid();
    }

    private sealed class InMemoryOperationRepository : IWorkflowOperationRepository
    {
        private readonly List<WorkflowOperation> items = [];
        public int FindCount { get; private set; }
        public IReadOnlyList<WorkflowOperation> List(Guid? instanceId = null, string? businessType = null, Guid? businessId = null, WorkflowOperationKind? kind = null)
            => items.Where(x => instanceId is null || x.InstanceId == instanceId).Where(x => businessType is null || x.BusinessType == businessType).Where(x => businessId is null || x.BusinessId == businessId).Where(x => kind is null || x.Kind == kind).ToArray();
        public WorkflowOperation? FindByDedupeKey(string dedupeKey)
        {
            FindCount++;
            return items.FirstOrDefault(x => x.DedupeKey == dedupeKey);
        }
        public void Add(WorkflowOperation operation) => items.Add(operation);
        public bool TryAdd(WorkflowOperation operation)
        {
            if (items.Any(x => x.DedupeKey == operation.DedupeKey)) return false;
            items.Add(operation);
            return true;
        }
    }

    private sealed class RacingOperationRepository : IWorkflowOperationRepository
    {
        public WorkflowOperation? ConcurrentOperation { get; private set; }
        public IReadOnlyList<WorkflowOperation> List(Guid? instanceId = null, string? businessType = null, Guid? businessId = null, WorkflowOperationKind? kind = null) => ConcurrentOperation is null ? [] : [ConcurrentOperation];
        public WorkflowOperation? FindByDedupeKey(string dedupeKey) => ConcurrentOperation?.DedupeKey == dedupeKey ? ConcurrentOperation : null;
        public void Add(WorkflowOperation operation)
        {
            ConcurrentOperation = operation;
            throw new InvalidOperationException("模拟唯一键竞态");
        }
        public bool TryAdd(WorkflowOperation operation)
        {
            ConcurrentOperation = operation;
            return false;
        }
    }

    private sealed class InMemoryInstanceRepository : IWorkflowInstanceRepository
    {
        private readonly List<WorkflowInstance> items = [];
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
            => items.Where(x => businessType is null || x.BusinessType == businessType).Where(x => businessId is null || x.BusinessId == businessId).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowInstance instance) => items.Add(instance);
        public bool TryAdd(WorkflowInstance instance) { if (items.Any(x => x.Id == instance.Id)) return false; Add(instance); return true; }
        public void Update(WorkflowInstance instance) { }
        public bool TryUpdate(WorkflowInstance instance) { var nextRevision = checked(instance.Revision + 1); Update(instance); instance.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class InMemoryTaskRepository : IWorkflowTaskRepository
    {
        private readonly List<WorkflowTask> items = [];
        public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
            => items.Where(x => instanceId is null || x.InstanceId == instanceId).Where(x => assignee is null || x.Assignee.Equals(assignee, StringComparison.OrdinalIgnoreCase)).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowTask task) => items.Add(task);
        public bool TryAdd(WorkflowTask task) { if (items.Any(x => x.Id == task.Id)) return false; Add(task); return true; }
        public void Update(WorkflowTask task) { }
        public bool TryUpdate(WorkflowTask task) { var nextRevision = checked(task.Revision + 1); Update(task); task.MarkPersistedRevision(nextRevision); return true; }
    }
}
