using System.Text.Json;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class WorkflowTaskServiceTests
{
    [Fact]
    public void CreateApprovalTask_IsIdempotent_AndRejectsNonAssignee()
    {
        var instance = StartInstance();
        var repository = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(repository);

        var first = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批", "admin");
        var second = service.CreateApprovalTask(instance, first.NodeId, "审批", "ADMIN");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(repository.Items);
        Assert.Throws<InvalidOperationException>(() => service.Approve(first, "finance"));
    }

    [Fact]
    public void WorkflowTask_UsesStableIdForSameInstanceNodeRoundAndAssignee()
    {
        var instance = StartInstance();
        var nodeId = Guid.CreateVersion7();

        var first = new WorkflowTask(instance, nodeId, "审批", "Admin", round: 2);
        var retry = new WorkflowTask(instance, nodeId, "审批", "admin", round: 2);
        var nextRound = new WorkflowTask(instance, nodeId, "审批", "admin", round: 3);

        Assert.Equal(first.Id, retry.Id);
        Assert.NotEqual(first.Id, nextRound.Id);
    }

    [Fact]
    public void CreateApprovalTask_Idempotency_DoesNotDuplicateAssignmentOperationOrNotification()
    {
        var instance = StartInstance();
        var tasks = new InMemoryTaskRepository();
        var operations = new InMemoryOperationRepository();
        var notifications = new InMemoryNotificationRepository();
        var service = new WorkflowTaskService(
            tasks,
            operations: new WorkflowOperationService(operations),
            notifications: new NotificationService(notifications));
        var nodeId = Guid.CreateVersion7();

        var first = service.CreateApprovalTask(instance, nodeId, "审批", "admin");
        var retry = service.CreateApprovalTask(instance, nodeId, "审批", "ADMIN");

        Assert.Equal(first.Id, retry.Id);
        Assert.Single(tasks.Items);
        Assert.Single(operations.List());
        Assert.Single(notifications.Items);
        Assert.Equal($"workflow-task-assigned:{first.Id}", operations.List().Single().DedupeKey);
        Assert.Equal($"workflow-task:{first.Id}", notifications.Items.Single().DedupeKey);
    }

    [Fact]
    public void CreateApprovalTask_PublishesNotificationOnlyAfterTransactionCommit()
    {
        var instance = StartInstance();
        var tasks = new InMemoryTaskRepository();
        var notifications = new InMemoryNotificationRepository();
        var boundary = new DeferredTransactionBoundary();
        var service = new WorkflowTaskService(
            tasks,
            notifications: new NotificationService(notifications),
            transactions: boundary);

        Assert.Throws<InvalidOperationException>(() => boundary.Execute(() =>
        {
            service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批", "admin");
            throw new InvalidOperationException("模拟外层事务失败");
        }));

        Assert.Empty(notifications.Items);

        boundary.Execute(() => service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批", "admin"));
        boundary.CommitPending();
        Assert.Single(notifications.Items);
    }

    [Fact]
    public void EnsureApprovalTasks_BatchesExistingTaskLookupAcrossNodesAndApprovers()
    {
        var definition = new WorkflowDefinition("BATCH_TASKS", "批量待办");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var firstApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approvers\":[\"admin\",\"finance\"]}");
        var secondApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务审批", configJson: "{\"approver\":\"finance\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, firstApproval.Id);
        definition.Connect(firstApproval.Id, secondApproval.Id);
        definition.Connect(secondApproval.Id, end.Id);
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var repository = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(repository);

        service.EnsureApprovalTasks(instance, definition);
        service.EnsureApprovalTasks(instance, definition);

        Assert.Equal(3, repository.Items.Count);
        Assert.Equal(2, repository.ListCallCount);
        Assert.Equal(2, repository.Items.Count(x => x.Assignee.Equals("finance", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void EnsureCurrentApprovalTask_RehydratesSnapshotAndRepairsOnlyMissingOriginalAssignee()
    {
        var definition = new WorkflowDefinition("APPROVAL_SNAPSHOT_REPAIR", "审批人快照补偿");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "会签", configJson: "{\"approvers\":[\"admin\",\"finance\"]}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var repository = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(repository);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(approval.Id);
        var initialTasks = new InMemoryTaskRepository();
        var initialService = new WorkflowTaskService(initialTasks, instanceService);
        initialService.EnsureCurrentApprovalTask(instance);

        var restarted = WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, instance.DefinitionSnapshotJson, instance.Status, instance.CurrentNodeId, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, instance.ActiveNodeIdsJson, instance.ParallelJoinArrivalsJson, instance.LoopIterationsJson, instance.ApprovalAssigneesJson);
        var partialTasks = new InMemoryTaskRepository();
        partialTasks.Add(new WorkflowTask(restarted, approval.Id, approval.Name, "admin"));
        var restartedService = new WorkflowTaskService(partialTasks, approverResolver: new FixedApproverResolver("latecomer"));

        var repaired = restartedService.EnsureCurrentApprovalTask(restarted);

        Assert.Single(repaired);
        Assert.Equal("finance", repaired[0].Assignee);
        Assert.Equal(["admin", "finance"], partialTasks.Items.Select(x => x.Assignee).OrderBy(x => x).ToArray());
        Assert.DoesNotContain(partialTasks.Items, x => x.Assignee == "latecomer");
    }

    [Fact]
    public void EnsureCurrentApprovalTask_RefreshesStaleInstanceAfterReturnAndDoesNotCreateHistoricalTask()
    {
        var definition = new WorkflowDefinition("STALE_TASK_REPAIR_AFTER_RETURN", "陈旧实例待办补偿");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approvers\":[\"admin\"]}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "复审", configJson: $"{{\"approvers\":[\"finance\"],\"returnTargets\":[\"{first.Id}\"]}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, first.Id);
        definition.Connect(first.Id, second.Id);
        definition.Connect(second.Id, end.Id);
        definition.Publish();

        var repository = new StaleLockInstanceRepository();
        var instances = new WorkflowInstanceService(repository);
        var instance = instances.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        instances.Advance(instance, first.Id);
        instances.EnsureApprovalAssigneeSnapshot(instance, first.Id, ["admin"]);
        instances.Advance(instance, second.Id);
        instances.EnsureApprovalAssigneeSnapshot(instance, second.Id, ["finance"]);

        var stale = WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, instance.DefinitionSnapshotJson, instance.Status, instance.CurrentNodeId, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, instance.ActiveNodeIdsJson, instance.ParallelJoinArrivalsJson, instance.LoopIterationsJson, instance.ApprovalAssigneesJson);
        instances.ReturnTo(instance, second.Id, first.Id);

        var tasks = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(tasks, instances, transactions: new DeferredTransactionBoundary());
        var repaired = service.EnsureCurrentApprovalTask(stale);

        Assert.Single(repaired);
        Assert.Equal(first.Id, repaired[0].NodeId);
        Assert.Equal(first.Id, stale.CurrentNodeId);
        Assert.DoesNotContain(tasks.Items, x => x.NodeId == second.Id);
    }

    [Fact]
    public void Approve_RejectsStaleInstanceBeforeExecutingAction()
    {
        var definition = new WorkflowDefinition("STALE_DECISION_LOCK", "陈旧审批决策锁");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approvers\":[\"admin\"],\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();

        var current = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        current.AdvanceTo(approval.Id);
        var stale = WorkflowInstance.Rehydrate(
            current.Id, current.DefinitionId, current.DefinitionCode, current.DefinitionVersion, current.BusinessType, current.BusinessId,
            current.StartedBy, current.DefinitionSnapshotJson, current.Status, current.CurrentNodeId, current.StartedAt, current.CompletedAt,
            current.PreviousInstanceId, current.Revision, current.ActiveNodeIdsJson, current.ParallelJoinArrivalsJson, current.LoopIterationsJson, current.ApprovalAssigneesJson);
        current.CaptureApprovalAssignees(approval.Id, ["admin"]);
        current.MarkPersistedRevision(stale.Revision + 1);

        var instanceRepository = new DecisionLockConflictInstanceRepository(stale, current);
        var instanceService = new WorkflowInstanceService(instanceRepository);
        var tasks = new InMemoryTaskRepository();
        var task = new WorkflowTask(stale, approval.Id, approval.Name, "admin");
        tasks.Add(task);
        var action = new CapturingActionHandler();
        var service = new WorkflowTaskService(
            tasks,
            instanceService,
            new WorkflowActionExecutor([action]),
            transactions: new DeferredTransactionBoundary());

        var exception = Assert.Throws<InvalidOperationException>(() => service.Approve(task, "admin"));

        Assert.Equal("流程实例状态已变化，请刷新后重试。", exception.Message);
        Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
        Assert.Null(action.Trigger);

        var createException = Assert.Throws<InvalidOperationException>(() => service.CreateApprovalTask(stale, approval.Id, approval.Name, "finance"));
        Assert.Equal("流程实例状态已变化，请刷新后重试。", createException.Message);
        Assert.Single(tasks.Items);
    }

    [Fact]
    public void CreateApprovalTask_RejectsHistoricalNodeAfterRuntimeLeavesIt()
    {
        var definition = new WorkflowDefinition("ACTIVE_TASK_NODE_GUARD", "活动审批待办门禁");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "复审", configJson: "{\"approver\":\"finance\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, first.Id);
        definition.Connect(first.Id, second.Id);
        definition.Connect(second.Id, end.Id);
        definition.Publish();

        var instanceRepository = new InMemoryInstanceRepository();
        var instances = new WorkflowInstanceService(instanceRepository);
        var instance = instances.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        instance.AdvanceTo(first.Id);
        instance.AdvanceTo(second.Id);
        var tasks = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(tasks, instances, transactions: new DeferredTransactionBoundary());

        var exception = Assert.Throws<InvalidOperationException>(() => service.CreateApprovalTask(instance, first.Id, first.Name, "admin"));

        Assert.Equal("审批待办节点不属于流程实例当前活动审批节点，不能创建。", exception.Message);
        Assert.Empty(tasks.Items);
    }

    [Fact]
    public void Retry_AdvancesAutomaticNodeAndCreatesNextApprovalTaskInOneUseCase()
    {
        var definition = CreateRetryToApprovalDefinition();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instances = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instances, operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var handler = new FailOnceActionHandler();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([handler]), new NotificationService(new InMemoryNotificationRepository()), operations);
        var tasks = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(tasks, instanceService, runtime: runtime);

        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));
        var result = service.Retry(instance, "admin");

        Assert.Equal(WorkflowRuntimeState.WaitingForApproval, result.State);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(2, handler.ExecutionCount);
        var task = Assert.Single(tasks.Items);
        Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
        Assert.Equal("admin", task.Assignee);
        Assert.Equal(definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id, task.NodeId);
    }

    [Fact]
    public void Retry_WhenNextApprovalTaskWriteFails_RestoresInstanceState()
    {
        var definition = CreateRetryToApprovalDefinition();
        var operations = new WorkflowOperationService(new InMemoryOperationRepository());
        var instances = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instances, operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var action = definition.Nodes.Single(x => x.Type == WorkflowNodeType.BusinessAction);
        var approval = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([new FailOnceActionHandler()]), new NotificationService(new InMemoryNotificationRepository()), operations);
        var tasks = new WorkflowTaskService(new ThrowingTaskAddRepository(approval.Id), instanceService, runtime: runtime);

        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));
        var originalRevision = instance.Revision;
        var originalNode = instance.CurrentNodeId;
        Assert.Throws<InvalidOperationException>(() => tasks.Retry(instance, "admin", failedNodeId: action.Id));

        Assert.Equal(originalNode, instance.CurrentNodeId);
        Assert.Equal(originalRevision, instance.Revision);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Contains(action.Id, instance.ActiveNodeIds);
        Assert.Empty(tasks.List(instance.Id));
    }

    [Fact]
    public void EnsureCurrentApprovalTask_DoesNotRestoreOriginalAssigneeAfterTransfer()
    {
        var definition = new WorkflowDefinition("APPROVAL_SNAPSHOT_TRANSFER", "审批人快照转交");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instances = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instances);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(approval.Id);
        var tasks = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(tasks, instanceService);
        var original = Assert.Single(service.EnsureCurrentApprovalTask(instance));
        service.Transfer(original, "admin", "finance", "请财务处理");

        var repaired = service.EnsureCurrentApprovalTask(instance);

        Assert.Empty(repaired);
        var pending = Assert.Single(tasks.Items, x => x.Status == WorkflowTaskStatus.Pending);
        Assert.Equal("finance", pending.Assignee);
        Assert.DoesNotContain(tasks.Items, x => x.Status == WorkflowTaskStatus.Pending && x.Assignee == "admin");
    }

    [Fact]
    public void EnsureCurrentApprovalTask_PreservesTransferAndRepairsOtherMissingAssigneeInSameRound()
    {
        var definition = new WorkflowDefinition("APPROVAL_SNAPSHOT_TRANSFER_REPAIR", "会签转交与补偿");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "会签", configJson: "{\"approvers\":[\"admin\",\"finance\"]}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instances = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instances);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(approval.Id);
        var tasks = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(tasks, instanceService);
        var initial = service.EnsureCurrentApprovalTask(instance);
        service.Transfer(initial.Single(x => x.Assignee == "admin"), "admin", "director");
        tasks.Items.Remove(initial.Single(x => x.Assignee == "finance"));

        var repaired = service.EnsureCurrentApprovalTask(instance);

        Assert.Single(repaired);
        Assert.Equal("finance", repaired[0].Assignee);
        Assert.Equal(["director", "finance"], tasks.Items.Where(x => x.Status == WorkflowTaskStatus.Pending).Select(x => x.Assignee).OrderBy(x => x).ToArray());
        Assert.DoesNotContain(tasks.Items, x => x.Status == WorkflowTaskStatus.Pending && x.Assignee == "admin");
    }

    [Fact]
    public void EnsureCurrentApprovalTask_PreservesLatestAssigneeAcrossTransferChain()
    {
        var definition = new WorkflowDefinition("APPROVAL_SNAPSHOT_TRANSFER_CHAIN", "审批人连续转交");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instances = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instances);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(approval.Id);
        var tasks = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(tasks, instanceService);
        var first = Assert.Single(service.EnsureCurrentApprovalTask(instance));
        var second = service.Transfer(first, "admin", "finance");
        service.Transfer(second, "finance", "director");

        var repaired = service.EnsureCurrentApprovalTask(instance);

        Assert.Empty(repaired);
        var pending = Assert.Single(tasks.Items, x => x.Status == WorkflowTaskStatus.Pending);
        Assert.Equal("director", pending.Assignee);
    }

    [Fact]
    public void ReturnToNode_NewRoundUsesInitialSnapshotInsteadOfPreviousRoundTransferTarget()
    {
        var definition = new WorkflowDefinition("APPROVAL_SNAPSHOT_RETURN", "审批人快照退回");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "复审", configJson: $"{{\"approver\":\"finance\",\"returnTargets\":[\"{first.Id}\"]}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, first.Id);
        definition.Connect(first.Id, second.Id);
        definition.Connect(second.Id, end.Id);
        definition.Publish();
        var instances = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instances);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(first.Id);
        var tasks = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(tasks, instanceService);
        var initial = Assert.Single(service.EnsureCurrentApprovalTask(instance));
        var transferred = service.Transfer(initial, "admin", "director");
        service.Approve(transferred, "director", "初审通过");
        var review = Assert.Single(tasks.Items, x => x.Status == WorkflowTaskStatus.Pending && x.NodeId == second.Id);

        var returned = service.ReturnToNode(review, "finance", first.Id, "退回补充");

        var retry = Assert.Single(returned);
        Assert.Equal(first.Id, retry.NodeId);
        Assert.Equal(2, retry.Round);
        Assert.Equal("admin", retry.Assignee);
        Assert.DoesNotContain(returned, x => x.Assignee == "director");
    }

    [Fact]
    public void ApprovingCurrentNode_ActivatesNextApprovalAndCompletesAtEnd()
    {
        var definition = new WorkflowDefinition("SEQUENTIAL", "串行审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var firstApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"admin\"}");
        var secondApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "复审", configJson: "{\"approver\":\"finance\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, firstApproval.Id);
        definition.Connect(firstApproval.Id, secondApproval.Id);
        definition.Connect(secondApproval.Id, end.Id);
        definition.Publish();

        var instanceRepository = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        instance.AdvanceTo(firstApproval.Id);
        var taskRepository = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(taskRepository, instanceService);
        var firstTask = service.CreateApprovalTask(instance, firstApproval.Id, firstApproval.Name, "admin");

        service.Approve(firstTask, "admin", "初审通过");

        var secondTask = Assert.Single(taskRepository.Items, x => x.Status == WorkflowTaskStatus.Pending);
        Assert.Equal(secondApproval.Id, secondTask.NodeId);
        Assert.Equal(secondApproval.Id, instance.CurrentNodeId);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);

        service.Approve(secondTask, "finance", "复审通过");

        Assert.Equal(end.Id, instance.CurrentNodeId);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public void AnyApprovalMode_CancelsSiblingTask_AndCompletesWorkflow()
    {
        var definition = new WorkflowDefinition("ANY_APPROVAL", "或签审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "或签", configJson: "{\"approvers\":[\"admin\",\"finance\"],\"approvalMode\":\"Any\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();

        var instanceRepository = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(approval.Id);
        var tasks = new InMemoryTaskRepository();
        var notifications = new InMemoryNotificationRepository();
        var service = new WorkflowTaskService(tasks, instanceService, notifications: new NotificationService(notifications));
        var admin = service.CreateApprovalTask(instance, approval.Id, approval.Name, "admin");
        var finance = service.CreateApprovalTask(instance, approval.Id, approval.Name, "finance");

        service.Approve(admin, "admin", "先同意");

        Assert.Equal(WorkflowTaskStatus.Approved, admin.Status);
        Assert.Equal(WorkflowTaskStatus.Cancelled, finance.Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.All(notifications.Items, notification => Assert.True(notification.IsRead));
        Assert.Throws<InvalidOperationException>(() => service.Approve(finance, "finance"));
    }

    [Fact]
    public void MajorityApprovalMode_WaitsForMajorityThenCancelsRemainingTasks()
    {
        var definition = new WorkflowDefinition("MAJORITY_APPROVAL", "多数审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "多数审批", configJson: "{\"approvers\":[\"admin\",\"finance\",\"director\"],\"approvalMode\":\"Majority\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();

        var instanceRepository = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(approval.Id);
        instance.CaptureApprovalAssignees(approval.Id, ["admin", "finance", "director"]);
        var tasks = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(tasks, instanceService);
        var admin = service.CreateApprovalTask(instance, approval.Id, approval.Name, "admin");
        var finance = service.CreateApprovalTask(instance, approval.Id, approval.Name, "finance");
        var director = service.CreateApprovalTask(instance, approval.Id, approval.Name, "director");

        service.Approve(admin, "admin", "第一票");

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.All(tasks.Items.Where(x => x.Id != admin.Id), x => Assert.Equal(WorkflowTaskStatus.Pending, x.Status));

        service.Approve(finance, "finance", "第二票");

        Assert.Equal(WorkflowTaskStatus.Approved, admin.Status);
        Assert.Equal(WorkflowTaskStatus.Approved, finance.Status);
        Assert.Equal(WorkflowTaskStatus.Cancelled, director.Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public void MajorityApprovalMode_TransferDoesNotChangeOriginalVoteThreshold()
    {
        var definition = new WorkflowDefinition("MAJORITY_TRANSFER", "多数审批转交");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "多数审批", configJson: "{\"approvers\":[\"admin\",\"finance\",\"director\"],\"approvalMode\":\"Majority\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();

        var instanceRepository = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(approval.Id);
        instance.CaptureApprovalAssignees(approval.Id, ["admin", "finance", "director"]);
        var tasks = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(tasks, instanceService);
        var admin = service.CreateApprovalTask(instance, approval.Id, approval.Name, "admin");
        var finance = service.CreateApprovalTask(instance, approval.Id, approval.Name, "finance");
        var director = service.CreateApprovalTask(instance, approval.Id, approval.Name, "director");

        var delegateTask = service.Transfer(admin, "admin", "assistant", "请代办");
        service.Approve(delegateTask, "assistant", "代理第一票");

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(WorkflowTaskStatus.Pending, finance.Status);
        Assert.Equal(WorkflowTaskStatus.Pending, director.Status);

        service.Approve(finance, "finance", "第二票");

        Assert.Equal(WorkflowTaskStatus.Cancelled, director.Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public void QuorumApprovalMode_UsesConfiguredThresholdInsteadOfMajority()
    {
        var definition = new WorkflowDefinition("QUORUM_APPROVAL", "法定人数审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "三票通过", configJson: "{\"approvers\":[\"admin\",\"finance\",\"director\",\"legal\",\"ceo\"],\"approvalMode\":\"Quorum\",\"requiredApprovals\":3}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id); definition.Connect(approval.Id, end.Id); definition.Publish();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(approval.Id);
        instance.CaptureApprovalAssignees(approval.Id, ["admin", "finance", "director", "legal", "ceo"]);
        var repository = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(repository, instanceService);
        var tasks = new[] { "admin", "finance", "director", "legal", "ceo" }.Select(user => service.CreateApprovalTask(instance, approval.Id, approval.Name, user)).ToArray();

        service.Approve(tasks[0], "admin"); service.Approve(tasks[1], "finance");
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(3, repository.Items.Count(x => x.Status == WorkflowTaskStatus.Pending));

        service.Approve(tasks[2], "director");
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.All(repository.Items.Where(x => x.Assignee is "legal" or "ceo"), x => Assert.Equal(WorkflowTaskStatus.Cancelled, x.Status));
    }

    [Fact]
    public void ApproveLastTask_CompletesWorkflowInstance()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance();
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var notificationRepository = new InMemoryNotificationRepository();
        var service = new WorkflowTaskService(taskRepository, new WorkflowInstanceService(instanceRepository), notifications: new NotificationService(notificationRepository));
        var task = service.CreateApprovalTask(instance, GetNodeId(instance, WorkflowNodeType.Approval), "审批", "admin");
        Assert.Single(notificationRepository.Items);
        Assert.False(notificationRepository.Items[0].IsRead);

        service.Approve(task, "admin", "同意", new DateTime(2026, 7, 15, 10, 0, 0));

        Assert.Equal(WorkflowTaskStatus.Approved, task.Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(task.CompletedAt, instance.CompletedAt);
        Assert.True(notificationRepository.Items[0].IsRead);
        Assert.Throws<InvalidOperationException>(() => service.Approve(task, "admin"));
    }

    [Fact]
    public void StaleTaskCopy_IsRejectedBeforeExecutingDecision()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance();
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(taskRepository, new WorkflowInstanceService(instanceRepository));
        var task = service.CreateApprovalTask(instance, GetNodeId(instance, WorkflowNodeType.Approval), "审批", "admin");
        var stale = WorkflowTask.Rehydrate(task.Id, task.InstanceId, task.DefinitionId, task.DefinitionVersion, task.NodeId, task.NodeName, task.BusinessType, task.BusinessId, task.Assignee, WorkflowTaskStatus.Pending, null, null, task.CreatedAt, null);

        service.Approve(task, "admin", "同意");

        var error = Assert.Throws<InvalidOperationException>(() => service.Approve(stale, "admin", "重复同意"));
        Assert.Contains("状态已变化", error.Message);
    }

    [Fact]
    public void RejectTask_RejectsInstance_AndCancelsOtherPendingTasks()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance();
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(taskRepository, new WorkflowInstanceService(instanceRepository));
        var rejected = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批一", "admin");
        var pending = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批二", "finance");

        service.Reject(rejected, "admin", "资料不完整", new DateTime(2026, 7, 15, 11, 0, 0));

        Assert.Equal(WorkflowTaskStatus.Rejected, rejected.Status);
        Assert.Equal(WorkflowTaskStatus.Cancelled, pending.Status);
        Assert.Equal(WorkflowInstanceStatus.Rejected, instance.Status);
        Assert.Throws<InvalidOperationException>(() => service.Reject(rejected, "admin"));
    }

    [Fact]
    public void RejectWithMultiplePendingTasks_PassesDecisionActorToBusinessAction()
    {
        var definition = CreateTerminalActionDefinition();
        var instanceRepository = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository);
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "requester");
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var handler = new CapturingActionHandler();
        var service = new WorkflowTaskService(
            taskRepository,
            instanceService,
            actionExecutor: new WorkflowActionExecutor([handler]));
        var approvalNode = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
        var rejected = service.CreateApprovalTask(instance, approvalNode.Id, approvalNode.Name, "admin");
        service.CreateApprovalTask(instance, approvalNode.Id, approvalNode.Name, "finance");

        service.Reject(rejected, "admin", "资料不完整");

        Assert.Equal(WorkflowActionTrigger.Rejected, handler.Trigger);
        Assert.Equal("admin", handler.Actor);
    }

    [Fact]
    public void CancelWithMultiplePendingTasks_PassesDecisionActorToBusinessAction()
    {
        var definition = CreateTerminalActionDefinition();
        var instanceRepository = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository);
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "requester");
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var handler = new CapturingActionHandler();
        var service = new WorkflowTaskService(
            taskRepository,
            instanceService,
            actionExecutor: new WorkflowActionExecutor([handler]));
        var approvalNode = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
        var cancelled = service.CreateApprovalTask(instance, approvalNode.Id, approvalNode.Name, "admin");
        service.CreateApprovalTask(instance, approvalNode.Id, approvalNode.Name, "finance");

        service.Cancel(cancelled, "admin", "主动取消");

        Assert.Equal(WorkflowActionTrigger.Cancelled, handler.Trigger);
        Assert.Equal("admin", handler.Actor);
    }

    [Fact]
    public void RejectTask_MarksCancelledTaskNotificationsRead()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance();
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var notificationRepository = new InMemoryNotificationRepository();
        var service = new WorkflowTaskService(
            taskRepository,
            new WorkflowInstanceService(instanceRepository),
            notifications: new NotificationService(notificationRepository));
        var rejected = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批一", "admin");
        var pending = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批二", "finance");

        service.Reject(rejected, "admin", "资料不完整", new DateTime(2026, 7, 15, 11, 0, 0));

        Assert.Equal(WorkflowTaskStatus.Cancelled, pending.Status);
        Assert.Equal(0, new NotificationService(notificationRepository).UnreadCount("admin"));
        Assert.Equal(0, new NotificationService(notificationRepository).UnreadCount("finance"));
    }

    [Fact]
    public void RejectTask_QueuesNotificationReadAndCancellationAuditAfterTransactionCommit()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance();
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var notificationRepository = new InMemoryNotificationRepository();
        var operationRepository = new InMemoryOperationRepository();
        var boundary = new DeferredTransactionBoundary();
        var operations = new WorkflowOperationService(operationRepository);
        var service = new WorkflowTaskService(
            taskRepository,
            new WorkflowInstanceService(instanceRepository),
            notifications: new NotificationService(notificationRepository),
            operations: operations,
            transactions: boundary);
        var rejected = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批一", "admin");
        var pending = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批二", "finance");
        boundary.CommitPending();

        service.Reject(rejected, "admin", "资料不完整", new DateTime(2026, 7, 15, 11, 0, 0));

        Assert.Equal(1, new NotificationService(notificationRepository).UnreadCount("admin"));
        Assert.Equal(1, new NotificationService(notificationRepository).UnreadCount("finance"));
        Assert.Contains(operationRepository.List(), item => item.Kind == WorkflowOperationKind.Cancelled && item.TaskId == pending.Id);

        boundary.CommitPending();

        Assert.Equal(0, new NotificationService(notificationRepository).UnreadCount("admin"));
        Assert.Equal(0, new NotificationService(notificationRepository).UnreadCount("finance"));
    }

    [Fact]
    public void RejectFailureAfterInstanceTermination_RestoresInMemoryInstanceAndTasks()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance();
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var operationRepository = new FailingCancellationOperationRepository();
        var service = new WorkflowTaskService(
            taskRepository,
            new WorkflowInstanceService(instanceRepository),
            operations: new WorkflowOperationService(operationRepository));
        var rejected = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批一", "admin");
        var pending = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批二", "finance");

        Assert.Throws<InvalidOperationException>(() => service.Reject(rejected, "admin", "故障注入"));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(1, instance.Revision);
        Assert.Equal(WorkflowTaskStatus.Pending, rejected.Status);
        Assert.Equal(1, rejected.Revision);
        Assert.Equal(WorkflowTaskStatus.Pending, pending.Status);
        Assert.Equal(1, pending.Revision);
    }

    [Fact]
    public void CancelTask_QueuesNotificationReadAndCancellationAuditAfterTransactionCommit()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance();
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var notificationRepository = new InMemoryNotificationRepository();
        var operationRepository = new InMemoryOperationRepository();
        var boundary = new DeferredTransactionBoundary();
        var operations = new WorkflowOperationService(operationRepository);
        var service = new WorkflowTaskService(
            taskRepository,
            new WorkflowInstanceService(instanceRepository),
            notifications: new NotificationService(notificationRepository),
            operations: operations,
            transactions: boundary);
        var cancelled = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批一", "admin");
        var pending = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批二", "finance");
        boundary.CommitPending();

        service.Cancel(cancelled, "admin", "主动取消", new DateTime(2026, 7, 18, 17, 0, 0));

        Assert.Equal(1, new NotificationService(notificationRepository).UnreadCount("admin"));
        Assert.Equal(1, new NotificationService(notificationRepository).UnreadCount("finance"));
        Assert.Contains(operationRepository.List(), item => item.Kind == WorkflowOperationKind.Cancelled && item.TaskId == pending.Id);

        boundary.CommitPending();

        Assert.Equal(0, new NotificationService(notificationRepository).UnreadCount("admin"));
        Assert.Equal(0, new NotificationService(notificationRepository).UnreadCount("finance"));
    }

    [Fact]
    public void WithdrawByInitiator_CancelsPendingTasksAndMarksNotificationsRead()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance(startedBy: "admin");
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var notificationRepository = new InMemoryNotificationRepository();
        var service = new WorkflowTaskService(
            taskRepository,
            new WorkflowInstanceService(instanceRepository),
            notifications: new NotificationService(notificationRepository));
        var first = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批一", "admin");
        var second = service.CreateApprovalTask(instance, Guid.CreateVersion7(), "审批二", "finance");

        service.Withdraw(instance.Id, "ADMIN", "资料需要补充", new DateTime(2026, 7, 15, 13, 0, 0));

        Assert.Equal(WorkflowInstanceStatus.Cancelled, instance.Status);
        Assert.Equal(WorkflowTaskStatus.Cancelled, first.Status);
        Assert.Equal(WorkflowTaskStatus.Cancelled, second.Status);
        Assert.Equal("资料需要补充", first.DecisionComment);
        Assert.Equal("ADMIN", first.DecisionActor);
        Assert.Equal(0, new NotificationService(notificationRepository).UnreadCount("admin"));
        Assert.Equal(0, new NotificationService(notificationRepository).UnreadCount("finance"));
    }

    [Fact]
    public void WithdrawByNonInitiator_IsRejectedWithoutChangingWorkflow()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance(startedBy: "admin");
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(taskRepository, new WorkflowInstanceService(instanceRepository));
        var task = service.CreateApprovalTask(instance, GetNodeId(instance, WorkflowNodeType.Approval), "审批", "finance");

        var error = Assert.Throws<InvalidOperationException>(() => service.Withdraw(instance.Id, "finance"));

        Assert.Contains("流程发起人", error.Message);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
    }

    [Fact]
    public void CreateApprovalTask_DoesNotCreateOnTerminalInstance()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance(startedBy: "admin");
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(taskRepository, new WorkflowInstanceService(instanceRepository));
        var current = service.CreateApprovalTask(instance, GetNodeId(instance, WorkflowNodeType.Approval), "审批", "admin");

        service.Reject(current, "admin", "资料不完整");

        var error = Assert.Throws<InvalidOperationException>(() => service.CreateApprovalTask(instance, GetNodeId(instance, WorkflowNodeType.Approval), "审批", "finance"));
        Assert.Contains("已结束", error.Message);
        Assert.Single(taskRepository.List(instance.Id));
        Assert.Equal(WorkflowTaskStatus.Rejected, taskRepository.List(instance.Id).Single().Status);
    }

    [Fact]
    public void TransferTask_CreatesNewPendingTaskAndPreservesTransferHistory()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance(startedBy: "admin");
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var notificationRepository = new InMemoryNotificationRepository();
        var service = new WorkflowTaskService(
            taskRepository,
            new WorkflowInstanceService(instanceRepository),
            notifications: new NotificationService(notificationRepository));
        var original = service.CreateApprovalTask(instance, GetNodeId(instance, WorkflowNodeType.Approval), "审批", "admin");

        var transferred = service.Transfer(original, "ADMIN", "finance", "请财务复核", new DateTime(2026, 7, 15, 14, 0, 0));

        Assert.Equal(WorkflowTaskStatus.Transferred, original.Status);
        Assert.Equal("finance", original.TransferTarget);
        Assert.Equal("ADMIN", original.DecisionActor);
        Assert.Equal("请财务复核", original.DecisionComment);
        Assert.Equal(WorkflowTaskStatus.Pending, transferred.Status);
        Assert.Equal("finance", transferred.Assignee);
        Assert.Equal(original.NodeId, transferred.NodeId);
        Assert.Equal(original.Round, transferred.Round);
        Assert.NotEqual(original.Id, transferred.Id);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(0, new NotificationService(notificationRepository).UnreadCount("admin"));
        Assert.Equal(1, new NotificationService(notificationRepository).UnreadCount("FINANCE"));
    }

    [Fact]
    public void TransferTask_RequiresCurrentAssigneeAndDistinctTarget()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance();
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(taskRepository, new WorkflowInstanceService(instanceRepository));
        var task = service.CreateApprovalTask(instance, GetNodeId(instance, WorkflowNodeType.Approval), "审批", "admin");

        Assert.Throws<InvalidOperationException>(() => service.Transfer(task, "finance", "legal"));
        Assert.Throws<InvalidOperationException>(() => service.Transfer(task, "admin", "ADMIN"));
        Assert.Throws<ArgumentException>(() => service.Transfer(task, "admin", "  "));
        Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
        Assert.Single(taskRepository.Items);
    }

    [Fact]
    public void TransferTask_RejectsHistoricalAssigneeCycle()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance();
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(taskRepository, new WorkflowInstanceService(instanceRepository));
        var original = service.CreateApprovalTask(instance, GetNodeId(instance, WorkflowNodeType.Approval), "审批", "admin");
        var transferred = service.Transfer(original, "admin", "finance");

        var error = Assert.Throws<InvalidOperationException>(() => service.Transfer(transferred, "finance", "admin"));

        Assert.Contains("历史审批人", error.Message);
        Assert.Equal(WorkflowTaskStatus.Pending, transferred.Status);
        Assert.Equal("finance", transferred.Assignee);
        Assert.DoesNotContain(taskRepository.Items, x => x.Status == WorkflowTaskStatus.Pending && x.Assignee.Equals("admin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApprovalContinuesWhenNotificationPersistenceFails()
    {
        var order = new SalesOrder("SO-NOTIFICATION-FAILURE", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 1m, 100m);
        var settlementRepository = new InMemorySettlementRepository();
        var settlementService = new SettlementService(settlementRepository, new InMemoryPurchaseOrderRepository(), new InMemorySalesOrderRepository(order));
        var settlement = settlementService.CreatePendingApproval(ErpSettlementKind.Receivable, order.Id, 40m, "REC-NOTIFICATION-FAILURE", DateOnly.FromDateTime(DateTime.Today));
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance(nameof(ErpSettlement), settlement.Id);
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var failureRecorder = new InMemoryNotificationFailureRecorder();
        var service = new WorkflowTaskService(
            taskRepository,
            new WorkflowInstanceService(instanceRepository),
            actionExecutor: new WorkflowActionExecutor([new ErpSettlementWorkflowActionHandler(settlementService)]),
            notifications: new NotificationService(new FailingNotificationRepository(), failureRecorder));

        var task = service.CreateApprovalTask(instance, GetNodeId(instance, WorkflowNodeType.Approval), "审批", "admin");
        service.Approve(task, "admin", "同意", new DateTime(2026, 7, 15, 12, 0, 0));

        Assert.Equal(WorkflowTaskStatus.Approved, task.Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(ErpSettlementStatus.Active, settlement.Status);
        Assert.Equal(2, failureRecorder.List().Count);
        Assert.Contains(failureRecorder.List(), x => x.Operation == "publish");
        Assert.Contains(failureRecorder.List(), x => x.Operation == "mark-read");
    }

    [Fact]
    public void ApprovalActionFailure_LeavesTaskPendingAndInstanceRunning()
    {
        var instanceRepository = new InMemoryInstanceRepository();
        var instance = StartInstance("custom.document");
        instanceRepository.Add(instance);
        var taskRepository = new InMemoryTaskRepository();
        var service = new WorkflowTaskService(
            taskRepository,
            new WorkflowInstanceService(instanceRepository),
            new WorkflowActionExecutor([new FailingActionHandler()]));
        var task = service.CreateApprovalTask(instance, GetNodeId(instance, WorkflowNodeType.Approval), "审批", "admin");

        Assert.Throws<InvalidOperationException>(() => service.Approve(task, "admin", "同意"));

        Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
        Assert.Equal(WorkflowTaskStatus.Pending, Assert.Single(taskRepository.Items).Status);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
    }

    [Fact]
    public void ReturnToNode_CreatesNextRoundAndPreservesApprovalHistory()
    {
        var definition = new WorkflowDefinition("RETURN_TO_NODE", "审批回退");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "复审", configJson: $"{{\"approver\":\"finance\",\"returnTargets\":[\"{first.Id}\"]}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, first.Id);
        definition.Connect(first.Id, second.Id);
        definition.Connect(second.Id, end.Id);
        definition.Publish();

        var instanceRepository = new InMemoryInstanceRepository();
        var instances = new WorkflowInstanceService(instanceRepository);
        var instance = instances.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        instances.Advance(instance, first.Id);
        var tasks = new InMemoryTaskRepository();
        var operations = new WorkflowOperationService(new InMemoryOperationRepository());
        var service = new WorkflowTaskService(tasks, instances, operations: operations);
        var initial = service.CreateApprovalTask(instance, first.Id, first.Name, "admin");

        service.Approve(initial, "admin", "初审通过");
        var review = Assert.Single(tasks.Items, x => x.Status == WorkflowTaskStatus.Pending && x.NodeId == second.Id);
        var restarted = Assert.Single(service.ReturnToNode(review, "finance", first.Id, "资料需要补充"));

        Assert.Equal(WorkflowTaskStatus.Approved, initial.Status);
        Assert.Equal(1, initial.Round);
        Assert.Equal(WorkflowTaskStatus.Returned, review.Status);
        Assert.Equal("资料需要补充", review.DecisionComment);
        Assert.Equal(first.Id, instance.CurrentNodeId);
        Assert.Equal(WorkflowTaskStatus.Pending, restarted.Status);
        Assert.Equal(first.Id, restarted.NodeId);
        Assert.Equal(2, restarted.Round);
        Assert.Contains(operations.List(instanceId: instance.Id), x => x.Kind == WorkflowOperationKind.Returned && x.TaskId == review.Id);
    }

    [Fact]
    public void ReturnToNode_TargetTaskCreationFailure_RestoresApprovalAssigneeSnapshot()
    {
        var definition = new WorkflowDefinition("RETURN_APPROVER_SNAPSHOT_ROLLBACK", "回退审批人快照回滚");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "复审", configJson: $"{{\"approver\":\"finance\",\"returnTargets\":[\"{first.Id}\"]}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, first.Id);
        definition.Connect(first.Id, second.Id);
        definition.Connect(second.Id, end.Id);
        definition.Publish();

        var instanceRepository = new InMemoryInstanceRepository();
        var instances = new WorkflowInstanceService(instanceRepository);
        var instance = instances.Start(definition, "custom.document", Guid.CreateVersion7());
        instances.Advance(instance, first.Id);
        instances.Advance(instance, second.Id);
        var tasks = new ThrowingTaskAddRepository(first.Id);
        var review = new WorkflowTask(instance, second.Id, second.Name, "finance");
        tasks.Add(review);
        var service = new WorkflowTaskService(tasks, instances);
        var originalRevision = instance.Revision;

        Assert.Throws<InvalidOperationException>(() => service.ReturnToNode(review, "finance", first.Id, "需要补充资料"));

        Assert.Equal(second.Id, instance.CurrentNodeId);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(originalRevision, instance.Revision);
        Assert.Equal("{}", instance.ApprovalAssigneesJson);
        Assert.Equal(WorkflowTaskStatus.Pending, review.Status);
        Assert.Equal(1, review.Revision);
    }

    [Fact]
    public void ReturnFromParallelBranch_CancelsOtherActiveBranchTasks()
    {
        var definition = new WorkflowDefinition("RETURN_PARALLEL", "并行分支回退");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var initial = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"admin\"}");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var returning = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: $"{{\"approver\":\"finance\",\"returnTargets\":[\"{initial.Id}\"]}}");
        var sibling = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "法务审批", configJson: "{\"approver\":\"legal\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, initial.Id);
        definition.Connect(initial.Id, split.Id);
        definition.Connect(split.Id, sibling.Id);
        definition.Connect(split.Id, returning.Id);
        definition.Connect(returning.Id, join.Id);
        definition.Connect(sibling.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instances = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instances.Start(definition, "custom.document", Guid.CreateVersion7());
        var tasks = new InMemoryTaskRepository();
        var runtime = new WorkflowRuntimeService(instances, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()));
        var service = new WorkflowTaskService(tasks, instances, runtime: runtime);

        runtime.Continue(instance);
        var first = service.EnsureCurrentApprovalTask(instance).Single();
        service.Approve(first, "admin", "初审通过");
        var branchTasks = tasks.Items.Where(x => x.Status == WorkflowTaskStatus.Pending).ToArray();
        var returnTask = branchTasks.Single(x => x.NodeId == returning.Id);
        var siblingTask = branchTasks.Single(x => x.NodeId == sibling.Id);

        service.ReturnToNode(returnTask, "finance", initial.Id, "请补充资料");

        Assert.Equal(WorkflowTaskStatus.Returned, returnTask.Status);
        Assert.Equal(WorkflowTaskStatus.Cancelled, siblingTask.Status);
        Assert.Equal(initial.Id, instance.CurrentNodeId);
        Assert.Throws<InvalidOperationException>(() => service.Approve(siblingTask, "legal"));
    }

    private static WorkflowInstance StartInstance(string businessType = nameof(SalesContract), Guid? businessId = null, string startedBy = "system")
    {
        var definition = new WorkflowDefinition("TASK-TEST", "待办测试");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approvalConfig = businessType == nameof(ErpSettlement)
            ? "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Active\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}"
            : businessType == "custom.document"
                ? "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}"
            : "{\"approver\":\"admin\"}";
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: approvalConfig);
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        return WorkflowInstance.Start(definition, businessType, businessId ?? Guid.CreateVersion7(), startedBy: startedBy);
    }

    private static WorkflowDefinition CreateRetryToApprovalDefinition()
    {
        var definition = new WorkflowDefinition("RETRY_TO_APPROVAL", "自动节点重试后进入审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "自动动作", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "人工审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, action.Id);
        definition.Connect(action.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private static WorkflowDefinition CreateTerminalActionDefinition()
    {
        var definition = new WorkflowDefinition("TERMINAL_ACTION_ACTOR", "终止动作操作者");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approvers\":[\"admin\",\"finance\"],\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Cancelled\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private static Guid GetNodeId(WorkflowInstance instance, WorkflowNodeType type)
    {
        using var document = JsonDocument.Parse(instance.DefinitionSnapshotJson);
        var nodes = document.RootElement.EnumerateObject().Single(x => x.Name.Equals("Nodes", StringComparison.OrdinalIgnoreCase)).Value;
        var node = nodes.EnumerateArray().Single(x => x.EnumerateObject().Single(p => p.Name.Equals("Type", StringComparison.OrdinalIgnoreCase)).Value.GetString() == type.ToString());
        return node.EnumerateObject().Single(x => x.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)).Value.GetGuid();
    }

    private sealed class InMemoryTaskRepository : IWorkflowTaskRepository
    {
        public List<WorkflowTask> Items { get; } = [];
        public int ListCallCount { get; private set; }
        public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
        {
            ListCallCount++;
            return Items.Where(x => instanceId is null || x.InstanceId == instanceId).Where(x => assignee is null || x.Assignee == assignee).Where(x => status is null || x.Status == status).ToArray();
        }
        public void Add(WorkflowTask task) => Items.Add(task);
        public bool TryAdd(WorkflowTask task) { if (Items.Any(x => x.Id == task.Id)) return false; Add(task); return true; }
        public void Update(WorkflowTask task) { }
        public bool TryUpdate(WorkflowTask task) { var nextRevision = checked(task.Revision + 1); Update(task); task.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class ThrowingTaskAddRepository(Guid throwOnNodeId) : IWorkflowTaskRepository
    {
        public List<WorkflowTask> Items { get; } = [];
        public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
            => Items.Where(x => instanceId is null || x.InstanceId == instanceId)
                .Where(x => assignee is null || x.Assignee == assignee)
                .Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowTask task)
        {
            if (task.NodeId == throwOnNodeId)
                throw new InvalidOperationException("目标审批待办写入故障注入");
            Items.Add(task);
        }
        public bool TryAdd(WorkflowTask task) { Add(task); return true; }
        public void Update(WorkflowTask task) { }
        public bool TryUpdate(WorkflowTask task) { var nextRevision = checked(task.Revision + 1); Update(task); task.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class FixedApproverResolver(params string[] assignees) : IWorkflowApproverResolver
    {
        public IReadOnlyList<string> Resolve(WorkflowInstance instance, string nodeConfigJson) => assignees;
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

    private sealed class StaleLockInstanceRepository : IWorkflowInstanceRepository, IWorkflowInstanceLockRepository
    {
        private readonly List<WorkflowInstance> items = [];

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

        public void Update(WorkflowInstance instance)
        {
            if (!TryUpdate(instance)) throw new InvalidOperationException("流程实例状态已变化，请刷新后重试。");
        }

        public bool TryUpdate(WorkflowInstance instance)
        {
            var persisted = items.SingleOrDefault(x => x.Id == instance.Id);
            if (persisted is null || persisted.Revision != instance.Revision) return false;
            instance.MarkPersistedRevision(instance.Revision + 1);
            return true;
        }

        public void LockForUpdate(WorkflowInstance instance)
        {
            var persisted = items.SingleOrDefault(x => x.Id == instance.Id);
            if (persisted is null || persisted.Revision != instance.Revision || persisted.Status != WorkflowInstanceStatus.Running)
                throw new InvalidOperationException("流程实例状态已变化，请刷新后重试。");
        }
    }

    private sealed class DecisionLockConflictInstanceRepository(WorkflowInstance stale, WorkflowInstance current) : IWorkflowInstanceRepository, IWorkflowInstanceLockRepository
    {
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
            => (businessType is null || stale.BusinessType == businessType)
                && (businessId is null || stale.BusinessId == businessId)
                && (status is null || stale.Status == status)
                ? [stale]
                : [];

        public void Add(WorkflowInstance instance) { }
        public bool TryAdd(WorkflowInstance instance) => true;
        public void Update(WorkflowInstance instance) => throw new InvalidOperationException("流程实例状态已变化，请刷新后重试。");
        public bool TryUpdate(WorkflowInstance instance) => false;

        public void LockForUpdate(WorkflowInstance instance)
        {
            if (instance.Revision != current.Revision)
                throw new InvalidOperationException("流程实例状态已变化，请刷新后重试。");
        }
    }

    private sealed class InMemoryOperationRepository : IWorkflowOperationRepository
    {
        private readonly List<WorkflowOperation> items = [];
        public IReadOnlyList<WorkflowOperation> List(Guid? instanceId = null, string? businessType = null, Guid? businessId = null, WorkflowOperationKind? kind = null)
            => items.Where(x => instanceId is null || x.InstanceId == instanceId)
                .Where(x => businessType is null || x.BusinessType == businessType)
                .Where(x => businessId is null || x.BusinessId == businessId)
                .Where(x => kind is null || x.Kind == kind).ToArray();
        public WorkflowOperation? FindByDedupeKey(string dedupeKey) => items.SingleOrDefault(x => x.DedupeKey == dedupeKey);
        public void Add(WorkflowOperation operation) => items.Add(operation);
        public bool TryAdd(WorkflowOperation operation)
        {
            if (items.Any(x => x.DedupeKey == operation.DedupeKey)) return false;
            items.Add(operation);
            return true;
        }
    }

    private sealed class FailingCancellationOperationRepository : IWorkflowOperationRepository
    {
        private readonly List<WorkflowOperation> items = [];
        public IReadOnlyList<WorkflowOperation> List(Guid? instanceId = null, string? businessType = null, Guid? businessId = null, WorkflowOperationKind? kind = null)
            => items.Where(x => instanceId is null || x.InstanceId == instanceId)
                .Where(x => businessType is null || x.BusinessType == businessType)
                .Where(x => businessId is null || x.BusinessId == businessId)
                .Where(x => kind is null || x.Kind == kind).ToArray();
        public WorkflowOperation? FindByDedupeKey(string dedupeKey) => items.SingleOrDefault(x => x.DedupeKey == dedupeKey);
        public void Add(WorkflowOperation operation)
        {
            if (operation.Kind == WorkflowOperationKind.Cancelled)
                throw new InvalidOperationException("取消审计故障注入");
            items.Add(operation);
        }
        public bool TryAdd(WorkflowOperation operation)
        {
            if (operation.Kind == WorkflowOperationKind.Cancelled)
                throw new InvalidOperationException("取消审计故障注入");
            if (items.Any(x => x.DedupeKey == operation.DedupeKey)) return false;
            items.Add(operation);
            return true;
        }
    }

    private sealed class InMemoryNotificationRepository : INotificationRepository
    {
        public List<WorkNotification> Items { get; } = [];
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => Items.Where(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase)).Where(x => !unreadOnly || !x.IsRead).ToArray();
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => Items.FirstOrDefault(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && x.DedupeKey == dedupeKey);
        public void Add(WorkNotification notification) => Items.Add(notification);
        public bool TryAdd(WorkNotification notification)
        {
            if (Items.Any(x => x.Recipient.Equals(notification.Recipient, StringComparison.OrdinalIgnoreCase) && x.DedupeKey == notification.DedupeKey)) return false;
            Items.Add(notification);
            return true;
        }
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }

    private sealed class FailingNotificationRepository : INotificationRepository
    {
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => [];
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => throw new InvalidOperationException("通知存储暂不可用");
        public void Add(WorkNotification notification) => throw new InvalidOperationException("通知存储暂不可用");
        public bool TryAdd(WorkNotification notification) => throw new InvalidOperationException("通知存储暂不可用");
        public void Update(WorkNotification notification) => throw new InvalidOperationException("通知存储暂不可用");
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => throw new InvalidOperationException("通知存储暂不可用");
    }

    private sealed class FailingActionHandler : IWorkflowActionHandler
    {
        public bool CanHandle(string businessType) => businessType == "custom.document";
        public void Execute(WorkflowActionContext context, WorkflowActionDefinition action) => throw new InvalidOperationException("业务动作执行失败");
    }

    private sealed class FailOnceActionHandler : IWorkflowActionHandler
    {
        public int ExecutionCount { get; private set; }
        public bool CanHandle(string businessType) => businessType == "custom.document";
        public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
        {
            ExecutionCount++;
            if (ExecutionCount == 1) throw new InvalidOperationException("自动动作首次失败");
        }
    }

    private sealed class CapturingActionHandler : IWorkflowActionHandler
    {
        public WorkflowActionTrigger? Trigger { get; private set; }
        public string? Actor { get; private set; }
        public bool CanHandle(string businessType) => businessType == "custom.document";
        public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
        {
            Trigger = context.Trigger;
            Actor = context.Actor;
        }
    }

    private sealed class DeferredTransactionBoundary : IWorkflowTransactionBoundary
    {
        private readonly List<Action> pendingCommits = [];
        private readonly Stack<List<Action>> scopes = [];

        public void Execute(Action operation, Action<Exception>? afterRollback = null)
            => ExecuteCore(operation, afterRollback, null);

        public void Execute(Action operation, Action<Exception>? afterRollback, Action? afterCommit)
            => ExecuteCore(operation, afterRollback, afterCommit);

        private void ExecuteCore(Action operation, Action<Exception>? afterRollback, Action? afterCommit)
        {
            var scope = new List<Action>();
            scopes.Push(scope);
            try { operation(); }
            catch (Exception exception)
            {
                scopes.Pop();
                afterRollback?.Invoke(exception);
                throw;
            }
            scopes.Pop();
            if (afterCommit is not null) scope.Add(afterCommit);
            if (scopes.Count > 0) scopes.Peek().AddRange(scope);
            else pendingCommits.AddRange(scope);
        }

        public void CommitPending()
        {
            foreach (var callback in pendingCommits.ToArray()) callback();
            pendingCommits.Clear();
        }
    }

    private sealed class InMemorySettlementRepository : ISettlementRepository
    {
        private readonly List<ErpSettlement> items = [];
        public IReadOnlyList<ErpSettlement> List() => items;
        public void Add(ErpSettlement item) => items.Add(item);
        public void Update(ErpSettlement item) { }
    }

    private sealed class InMemoryPurchaseOrderRepository : IPurchaseOrderRepository
    {
        private readonly List<PurchaseOrder> items = [];
        public IReadOnlyList<PurchaseOrder> List() => items;
        public void Add(PurchaseOrder item) => items.Add(item);
        public void Update(PurchaseOrder item) { }
    }

    private sealed class InMemorySalesOrderRepository(params SalesOrder[] seed) : ISalesOrderRepository
    {
        private readonly List<SalesOrder> items = [.. seed];
        public IReadOnlyList<SalesOrder> List() => items;
        public void Add(SalesOrder item) => items.Add(item);
        public void Update(SalesOrder item) { }
    }
}
