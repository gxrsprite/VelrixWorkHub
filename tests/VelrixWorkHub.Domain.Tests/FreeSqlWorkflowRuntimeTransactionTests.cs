using FreeSql;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.Workflow;

namespace VelrixWorkHub.Domain.Tests;

public sealed class FreeSqlWorkflowRuntimeTransactionTests
{
    [Fact]
    public void AutomaticActionFailure_RollsBackAdvanceAndExecutionAudit_ButKeepsFailureAudit()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-workflow-runtime-transaction-{Guid.NewGuid():N}.db");
        try
        {
            using var fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
                .UseAutoSyncStructure(true)
                .Build();
            var instanceRepository = new FreeSqlWorkflowInstanceRepository(fsql);
            var operationRepository = new FreeSqlWorkflowOperationRepository(fsql);
            var operations = new WorkflowOperationService(operationRepository);
            var instanceService = new WorkflowInstanceService(instanceRepository, operations);
            var definition = CreateDefinition();
            var instance = instanceService.Start(definition, "transaction.runtime", Guid.CreateVersion7(), startedBy: "admin");
            var actionNodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.BusinessAction).Id;
            var runtime = new WorkflowRuntimeService(
                instanceService,
                new WorkflowActionExecutor([new ThrowingActionHandler()]),
                new NotificationService(new EmptyNotificationRepository()),
                operations,
                new FreeSqlWorkflowTransactionBoundary(fsql));

            Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));

            var persisted = Assert.Single(instanceRepository.List(businessId: instance.BusinessId));
            Assert.Equal(actionNodeId, persisted.CurrentNodeId);
            Assert.Equal(WorkflowInstanceStatus.Running, persisted.Status);
            Assert.Equal(2, persisted.Revision);
            Assert.Equal(persisted.CurrentNodeId, instance.CurrentNodeId);
            Assert.Equal(2, instance.Revision);

            var history = operations.List(instanceId: instance.Id);
            Assert.DoesNotContain(history, x => x.Kind == WorkflowOperationKind.NodeExecuted && x.NodeId == actionNodeId);
            Assert.Single(history, x => x.Kind == WorkflowOperationKind.NodeFailed && x.NodeId == actionNodeId);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }

    [Fact]
    public void AutomaticActionFailureInsideApprovalTransaction_KeepsFailureAuditAfterOuterRollback()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-workflow-runtime-nested-transaction-{Guid.NewGuid():N}.db");
        try
        {
            using var fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
                .UseAutoSyncStructure(true)
                .Build();
            var instanceRepository = new FreeSqlWorkflowInstanceRepository(fsql);
            var taskRepository = new FreeSqlWorkflowTaskRepository(fsql);
            var operationRepository = new FreeSqlWorkflowOperationRepository(fsql);
            var operations = new WorkflowOperationService(operationRepository);
            var instanceService = new WorkflowInstanceService(instanceRepository, operations);
            var definition = CreateApprovalActionDefinition();
            var instance = instanceService.Start(definition, "transaction.runtime", Guid.CreateVersion7(), startedBy: "admin");
            var transactionBoundary = new FreeSqlWorkflowTransactionBoundary(fsql);
            var runtime = new WorkflowRuntimeService(
                instanceService,
                new WorkflowActionExecutor([new ThrowingActionHandler()]),
                new NotificationService(new EmptyNotificationRepository()),
                operations,
                transactionBoundary);
            runtime.Continue(instance);
            var approvalNodeId = instance.CurrentNodeId;
            var actionNodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.BusinessAction).Id;
            var task = new WorkflowTask(instance, approvalNodeId, "审批", "admin");
            taskRepository.Add(task);
            var taskService = new WorkflowTaskService(taskRepository, instanceService, runtime: runtime, transactions: transactionBoundary);

            Assert.Throws<InvalidOperationException>(() => taskService.Approve(task, "admin", "触发自动动作失败"));

            var persistedInstance = Assert.Single(instanceRepository.List(businessId: instance.BusinessId));
            Assert.Equal(approvalNodeId, persistedInstance.CurrentNodeId);
            Assert.Equal(approvalNodeId, instance.CurrentNodeId);
            Assert.Equal(WorkflowTaskStatus.Pending, Assert.Single(taskRepository.List(instance.Id)).Status);
            var history = operations.List(instanceId: instance.Id);
            Assert.Single(history, x => x.Kind == WorkflowOperationKind.NodeFailed && x.NodeId == actionNodeId);
            Assert.DoesNotContain(history, x => x.Kind == WorkflowOperationKind.NodeExecuted && x.NodeId == actionNodeId);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }

    [Fact]
    public void AutomaticActionFailure_CanRetryFromRestoredNodeAndComplete()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-workflow-runtime-retry-{Guid.NewGuid():N}.db");
        try
        {
            using var fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
                .UseAutoSyncStructure(true)
                .Build();
            var instanceRepository = new FreeSqlWorkflowInstanceRepository(fsql);
            var operationRepository = new FreeSqlWorkflowOperationRepository(fsql);
            var operations = new WorkflowOperationService(operationRepository);
            var instanceService = new WorkflowInstanceService(instanceRepository, operations);
            var definition = CreateDefinition();
            var instance = instanceService.Start(definition, "transaction.runtime", Guid.CreateVersion7(), startedBy: "admin");
            var actionNodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.BusinessAction).Id;
            var handler = new FailOnceActionHandler();
            var runtime = new WorkflowRuntimeService(
                instanceService,
                new WorkflowActionExecutor([handler]),
                new NotificationService(new EmptyNotificationRepository()),
                operations,
                new FreeSqlWorkflowTransactionBoundary(fsql));

            Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));
            var failedRevision = instance.Revision;

            var result = runtime.Continue(instance);

            Assert.Equal(WorkflowRuntimeState.Completed, result.State);
            Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
            Assert.Equal(failedRevision + 2, instance.Revision);
            var persisted = Assert.Single(instanceRepository.List(businessId: instance.BusinessId));
            Assert.Equal(WorkflowInstanceStatus.Completed, persisted.Status);
            Assert.Equal(2, handler.Attempts);
            var history = operations.List(instanceId: instance.Id);
            Assert.Single(history, x => x.Kind == WorkflowOperationKind.NodeFailed && x.NodeId == actionNodeId);
            Assert.Single(history, x => x.Kind == WorkflowOperationKind.NodeExecuted && x.NodeId == actionNodeId);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }

    private static WorkflowDefinition CreateDefinition()
    {
        var definition = new WorkflowDefinition("RUNTIME_TRANSACTION", "自动动作事务");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var action = definition.AddNode(
            Guid.CreateVersion7(),
            WorkflowNodeType.BusinessAction,
            "动作",
            configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, action.Id);
        definition.Connect(action.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private static WorkflowDefinition CreateApprovalActionDefinition()
    {
        var definition = new WorkflowDefinition("RUNTIME_NESTED_TRANSACTION", "审批后自动动作事务");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var action = definition.AddNode(
            Guid.CreateVersion7(),
            WorkflowNodeType.BusinessAction,
            "自动动作",
            configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, action.Id);
        definition.Connect(action.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private sealed class ThrowingActionHandler : IWorkflowActionHandler
    {
        public bool CanHandle(string businessType) => businessType == "transaction.runtime";

        public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
            => throw new InvalidOperationException("模拟自动业务动作失败");
    }

    private sealed class FailOnceActionHandler : IWorkflowActionHandler
    {
        public int Attempts { get; private set; }

        public bool CanHandle(string businessType) => businessType == "transaction.runtime";

        public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
        {
            Attempts++;
            if (Attempts == 1) throw new InvalidOperationException("模拟首次自动业务动作失败");
        }
    }

    private sealed class EmptyNotificationRepository : INotificationRepository
    {
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => [];
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => null;
        public void Add(WorkNotification notification) { }
        public bool TryAdd(WorkNotification notification) => true;
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }
}
