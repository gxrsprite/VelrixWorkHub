using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class WorkflowRuntimeServiceTests
{
    [Fact]
    public void DefinitionValidator_RejectsMalformedAutomaticNodeConfiguration()
    {
        var definition = new WorkflowDefinition("RUNTIME_INVALID", "自动节点配置校验");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var notification = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "通知", configJson: "{\"recipients\":[]}");
        var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "动作", configJson: "{\"action\":{\"type\":\"Unknown\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, notification.Id);
        definition.Connect(notification.Id, action.Id);
        definition.Connect(action.Id, end.Id);

        var error = Assert.Throws<InvalidOperationException>(() => definition.Publish());

        Assert.Contains("通知", error.Message);
        Assert.Contains("动作", error.Message);
    }

    [Fact]
    public void Continue_ExecutesNotificationAndBusinessActionNodes_AndCompletesAtEnd()
    {
        var definition = CreateAutomaticDefinition();
        var instanceRepository = new InMemoryInstanceRepository();
        var operationRepository = new InMemoryOperationRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository, new WorkflowOperationService(operationRepository));
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var notifications = new InMemoryNotificationRepository();
        var handler = new RecordingHandler();
        var operations = new WorkflowOperationService(operationRepository);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([handler]), new NotificationService(notifications), operations);

        var result = runtime.Continue(instance, occurredAt: new DateTime(2026, 7, 16, 10, 0, 0));

        Assert.Equal(WorkflowRuntimeState.Completed, result.State);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(2, notifications.Items.Count);
        Assert.Equal(1, handler.ExecutionCount);
        Assert.Equal(2, operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeExecuted).Count);
        Assert.Contains(operations.List(instanceId: instance.Id), x => x.Kind == WorkflowOperationKind.NodeCompleted && x.NodeId == instance.CurrentNodeId);
        Assert.Contains(notifications.Items, x => x.Recipient == "admin" && x.Title == "流程已提交");
        Assert.Contains(notifications.Items, x => x.Recipient == "finance");
        Assert.Equal(WorkflowRuntimeState.Completed, runtime.Continue(instance).State);
    }

    [Fact]
    public void Continue_LocksInstanceBeforePureGraphStateTransition()
    {
        var definition = new WorkflowDefinition("RUNTIME_STATE_LOCK", "图状态行锁");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();

        var repository = new LockingInstanceRepository();
        var boundary = new NestedTransactionBoundary();
        var instances = new WorkflowInstanceService(repository, transactions: boundary);
        var instance = instances.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        repository.RejectNextLock = true;
        var runtime = new WorkflowRuntimeService(
            instances,
            new WorkflowActionExecutor([]),
            new NotificationService(new InMemoryNotificationRepository()),
            transactions: boundary);

        var error = Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));

        Assert.Equal("模拟实例行锁失败", error.Message);
        Assert.Equal(1, repository.LockCount);
        Assert.Equal(start.Id, instance.CurrentNodeId);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(1, instance.Revision);
    }

    [Fact]
    public void InstanceService_Advance_LocksBeforeMutatingWhenCalledDirectly()
    {
        var definition = new WorkflowDefinition("INSTANCE_STATE_LOCK", "直接用例行锁");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();

        var repository = new LockingInstanceRepository();
        var boundary = new NestedTransactionBoundary();
        var instances = new WorkflowInstanceService(repository, transactions: boundary);
        var instance = instances.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        repository.RejectNextLock = true;

        var error = Assert.Throws<InvalidOperationException>(() => instances.Advance(instance, approval.Id));

        Assert.Equal("模拟实例行锁失败", error.Message);
        Assert.Equal(1, repository.LockCount);
        Assert.Equal(start.Id, instance.CurrentNodeId);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(1, instance.Revision);
    }

    [Fact]
    public void Continue_ReleasesPerInstanceLockAfterCompletion()
    {
        var definition = CreateAutomaticDefinition();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([new RecordingHandler()]), new NotificationService(new InMemoryNotificationRepository()));

        runtime.Continue(instance);

        var locks = typeof(WorkflowRuntimeService).GetField("instanceLocks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runtime)!;
        var count = (int)locks.GetType().GetProperty("Count")!.GetValue(locks)!;
        Assert.Equal(0, count);
    }

    [Fact]
    public void Reject_ReleasesRuntimeLockAfterTerminalTransactionCommits()
    {
        var definition = CreateApprovalToAutomaticDefinition();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()));
        var tasks = new WorkflowTaskService(new InMemoryTaskRepository(), instanceService, runtime: runtime);
        runtime.Continue(instance);
        var task = tasks.CreateApprovalTask(instance, instance.CurrentNodeId, "审批", "admin");

        tasks.Reject(task, "admin", "退回");

        Assert.Equal(WorkflowInstanceStatus.Rejected, instance.Status);
        var locks = typeof(WorkflowRuntimeService).GetField("instanceLocks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runtime)!;
        var count = (int)locks.GetType().GetProperty("Count")!.GetValue(locks)!;
        Assert.Equal(0, count);
    }

    [Fact]
    public void Reject_WhenOuterTransactionRollsBack_RestoresStateAndKeepsRuntimeLock()
    {
        var definition = CreateApprovalToAutomaticDefinition();
        var boundary = new NestedTransactionBoundary();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), transactions: boundary);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()), transactions: boundary);
        var tasks = new InMemoryTaskRepository();
        var taskService = new WorkflowTaskService(tasks, instanceService, runtime: runtime, transactions: boundary);
        runtime.Continue(instance);
        var task = taskService.CreateApprovalTask(instance, instance.CurrentNodeId, "审批", "admin");

        Assert.Throws<InvalidOperationException>(() => boundary.Execute(() =>
        {
            taskService.Reject(task, "admin", "外层失败");
            throw new InvalidOperationException("模拟外层写入失败");
        }));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
        var locks = typeof(WorkflowRuntimeService).GetField("instanceLocks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runtime)!;
        Assert.Equal(1, (int)locks.GetType().GetProperty("Count")!.GetValue(locks)!);
    }

    [Fact]
    public void Cancel_WhenOuterTransactionRollsBack_RestoresStateAndKeepsRuntimeLock()
    {
        var definition = CreateApprovalToAutomaticDefinition();
        var boundary = new NestedTransactionBoundary();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), transactions: boundary);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()), transactions: boundary);
        var tasks = new InMemoryTaskRepository();
        var taskService = new WorkflowTaskService(tasks, instanceService, runtime: runtime, transactions: boundary);
        runtime.Continue(instance);
        var task = taskService.CreateApprovalTask(instance, instance.CurrentNodeId, "审批", "admin");

        Assert.Throws<InvalidOperationException>(() => boundary.Execute(() =>
        {
            taskService.Cancel(task, "admin", "外层失败");
            throw new InvalidOperationException("模拟外层写入失败");
        }));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
        var locks = typeof(WorkflowRuntimeService).GetField("instanceLocks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runtime)!;
        Assert.Equal(1, (int)locks.GetType().GetProperty("Count")!.GetValue(locks)!);
    }

    [Fact]
    public void Withdraw_WhenOuterTransactionRollsBack_RestoresStateAndKeepsRuntimeLock()
    {
        var definition = CreateApprovalToAutomaticDefinition();
        var boundary = new NestedTransactionBoundary();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), transactions: boundary);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()), transactions: boundary);
        var tasks = new InMemoryTaskRepository();
        var taskService = new WorkflowTaskService(tasks, instanceService, runtime: runtime, transactions: boundary);
        runtime.Continue(instance);
        var task = taskService.CreateApprovalTask(instance, instance.CurrentNodeId, "审批", "admin");

        Assert.Throws<InvalidOperationException>(() => boundary.Execute(() =>
        {
            taskService.Withdraw(instance.Id, "admin", "外层失败");
            throw new InvalidOperationException("模拟外层写入失败");
        }));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
        var locks = typeof(WorkflowRuntimeService).GetField("instanceLocks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runtime)!;
        Assert.Equal(1, (int)locks.GetType().GetProperty("Count")!.GetValue(locks)!);
    }

    [Fact]
    public void Approve_WhenOuterTransactionRollsBack_RestoresTaskAndKeepsRuntimeLock()
    {
        var definition = CreateApprovalToAutomaticDefinition();
        var boundary = new NestedTransactionBoundary();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), transactions: boundary);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()), transactions: boundary);
        var tasks = new InMemoryTaskRepository();
        var taskService = new WorkflowTaskService(tasks, instanceService, runtime: runtime, transactions: boundary);
        runtime.Continue(instance);
        var task = taskService.CreateApprovalTask(instance, instance.CurrentNodeId, "审批", "admin");

        Assert.Throws<InvalidOperationException>(() => boundary.Execute(() =>
        {
            taskService.Approve(task, "admin", "外层失败");
            throw new InvalidOperationException("模拟外层写入失败");
        }));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
        var locks = typeof(WorkflowRuntimeService).GetField("instanceLocks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runtime)!;
        Assert.Equal(1, (int)locks.GetType().GetProperty("Count")!.GetValue(locks)!);
    }

    [Fact]
    public void Transfer_WhenOuterTransactionRollsBack_RemovesCreatedTaskAndRestoresOriginal()
    {
        var definition = CreateApprovalToAutomaticDefinition();
        var boundary = new NestedTransactionBoundary();
        var instances = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instances, transactions: boundary);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        instanceService.Advance(instance, instance.CurrentNodeId == definition.Nodes.Single(x => x.Type == WorkflowNodeType.Start).Id
            ? definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id
            : instance.CurrentNodeId);
        var tasks = new InMemoryTaskRepository();
        var taskService = new WorkflowTaskService(tasks, instanceService, transactions: boundary);
        var task = taskService.CreateApprovalTask(instance, instance.CurrentNodeId, "审批", "admin");

        Assert.Throws<InvalidOperationException>(() => boundary.Execute(() =>
        {
            taskService.Transfer(task, "admin", "finance", "外层失败");
            throw new InvalidOperationException("模拟外层写入失败");
        }));

        Assert.Single(tasks.Items);
        Assert.Same(task, tasks.Items[0]);
        Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
        Assert.Null(task.TransferTarget);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
    }

    [Fact]
    public void ReturnToNode_WhenOuterTransactionRollsBack_RemovesCreatedTaskAndRestoresOriginal()
    {
        var definition = new WorkflowDefinition("RUNTIME_RETURN_TASK_ROLLBACK", "退回任务补偿");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "复审", configJson: $"{{\"approver\":\"finance\",\"returnTargets\":[\"{first.Id}\"]}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, first.Id);
        definition.Connect(first.Id, second.Id);
        definition.Connect(second.Id, end.Id);
        definition.Publish();

        var boundary = new NestedTransactionBoundary();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), transactions: boundary);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        instanceService.Advance(instance, first.Id);
        instanceService.Advance(instance, second.Id);
        var tasks = new InMemoryTaskRepository();
        var firstTask = new WorkflowTask(instance, first.Id, first.Name, "admin");
        firstTask.Approve("admin", "已通过");
        tasks.Add(firstTask);
        var reviewTask = new WorkflowTask(instance, second.Id, second.Name, "finance");
        tasks.Add(reviewTask);
        var taskService = new WorkflowTaskService(tasks, instanceService, transactions: boundary);

        Assert.Throws<InvalidOperationException>(() => boundary.Execute(() =>
        {
            taskService.ReturnToNode(reviewTask, "finance", first.Id, "外层失败");
            throw new InvalidOperationException("模拟外层写入失败");
        }));

        Assert.Equal(2, tasks.Items.Count);
        Assert.Equal(WorkflowTaskStatus.Approved, firstTask.Status);
        Assert.Equal(WorkflowTaskStatus.Pending, reviewTask.Status);
        Assert.Equal(second.Id, instance.CurrentNodeId);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
    }

    [Fact]
    public void CreateApprovalTask_WhenOuterTransactionRollsBack_RemovesCreatedTask()
    {
        var definition = CreateApprovalToAutomaticDefinition();
        var boundary = new NestedTransactionBoundary();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), transactions: boundary);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var approval = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
        instanceService.Advance(instance, approval.Id);
        var tasks = new InMemoryTaskRepository();
        var taskService = new WorkflowTaskService(tasks, instanceService, transactions: boundary);

        Assert.Throws<InvalidOperationException>(() => boundary.Execute(() =>
        {
            taskService.CreateApprovalTask(instance, approval.Id, approval.Name, "admin");
            throw new InvalidOperationException("模拟外层写入失败");
        }));

        Assert.Empty(tasks.Items);
        Assert.Equal(approval.Id, instance.CurrentNodeId);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
    }

    [Fact]
    public void Continue_WhenOuterTransactionRollsBackAfterCompletion_KeepsRuntimeLock()
    {
        var definition = CreateActionOnlyDefinition();
        var instanceRepository = new InMemoryInstanceRepository();
        var boundary = new NestedTransactionBoundary();
        var instances = new WorkflowInstanceService(instanceRepository, transactions: boundary);
        var instance = instances.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var runtime = new WorkflowRuntimeService(instances, new WorkflowActionExecutor([new RecordingHandler()]), new NotificationService(new InMemoryNotificationRepository()), transactions: boundary);

        Assert.Throws<InvalidOperationException>(() => boundary.Execute(() =>
        {
            runtime.Continue(instance);
            throw new InvalidOperationException("模拟外层完成后写入失败");
        }));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        var locks = typeof(WorkflowRuntimeService).GetField("instanceLocks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runtime)!;
        var count = (int)locks.GetType().GetProperty("Count")!.GetValue(locks)!;
        Assert.Equal(1, count);
    }

    [Fact]
    public void Continue_WithoutTransaction_RestoresStateWhenTerminalPersistenceFails()
    {
        var definition = CreateActionOnlyDefinition();
        var instances = new ThrowingCompletionInstanceRepository();
        var instanceService = new WorkflowInstanceService(instances);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var end = definition.Nodes.Single(x => x.Type == WorkflowNodeType.End);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([new RecordingHandler()]), new NotificationService(new InMemoryNotificationRepository()));

        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(end.Id, instance.CurrentNodeId);
        Assert.Equal(3, instance.Revision);
        Assert.Contains(end.Id, instance.ActiveNodeIds);
    }

    [Fact]
    public void Continue_WhenNotificationPersistenceFails_CompletesNodeAndRecordsDeliveryFailure()
    {
        var definition = new WorkflowDefinition("RUNTIME_NOTIFICATION_FAILURE", "通知失败不阻断");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var notification = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "通知", configJson: "{\"recipients\":\"finance\",\"content\":\"请处理\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, notification.Id);
        definition.Connect(notification.Id, end.Id);
        definition.Publish();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var failures = new InMemoryNotificationFailureRecorder();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new ThrowingNotificationRepository(), failures), operations);

        var result = runtime.Continue(instance);

        Assert.Equal(WorkflowRuntimeState.Completed, result.State);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        var failure = Assert.Single(failures.List());
        Assert.Equal("publish", failure.Operation);
        Assert.Equal("finance", failure.Recipient);
        Assert.NotNull(failure.Payload);
        Assert.Equal("请处理", failure.Payload!.Content);
        Assert.Equal(WorkNotificationKind.System, failure.Payload.Kind);
        Assert.Contains(operations.List(instanceId: instance.Id), x => x.Kind == WorkflowOperationKind.NodeExecuted && x.NodeId == notification.Id);
    }

    [Fact]
    public void ContinueAfterApproval_UsesRuntimeForAutomaticNodes_AndDoesNotCompleteUnsupportedNodeEarly()
    {
        var definition = CreateApprovalToAutomaticDefinition();
        var instanceRepository = new InMemoryInstanceRepository();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(instanceRepository, operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var notifications = new InMemoryNotificationRepository();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([new RecordingHandler()]), new NotificationService(notifications), operations);
        var taskRepository = new InMemoryTaskRepository();
        var tasks = new WorkflowTaskService(taskRepository, instanceService, runtime: runtime);
        runtime.Continue(instance);
        var approvalId = instance.CurrentNodeId;
        var task = tasks.CreateApprovalTask(instance, approvalId, "审批", "admin");

        tasks.Approve(task, "admin", "同意", new DateTime(2026, 7, 16, 10, 1, 0));

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(WorkflowNodeType.End, instance.GetNodeType(instance.CurrentNodeId));
        Assert.Single(notifications.Items);
        Assert.Contains(operations.List(instanceId: instance.Id), x => x.Kind == WorkflowOperationKind.NodeExecuted);
    }

    [Fact]
    public void ApprovingTask_PropagatesActorToAutomaticBusinessAction()
    {
        var definition = new WorkflowDefinition("RUNTIME_ACTOR_PROPAGATION", "审批人传递");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"reviewer\"}");
        var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "回写", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, action.Id);
        definition.Connect(action.Id, end.Id);
        definition.Publish();

        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
        var handler = new ActorCapturingHandler();
        var runtime = new WorkflowRuntimeService(
            instanceService,
            new WorkflowActionExecutor([handler]),
            new NotificationService(new InMemoryNotificationRepository()),
            operations);
        var taskRepository = new InMemoryTaskRepository();
        var tasks = new WorkflowTaskService(taskRepository, instanceService, operations: operations, runtime: runtime);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "requester");

        runtime.Continue(instance);
        var task = tasks.CreateApprovalTask(instance, approval.Id, approval.Name, "reviewer");
        tasks.Approve(task, "reviewer", "同意");

        Assert.Equal("reviewer", handler.LastActor);
        Assert.Single(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeExecuted), x => x.NodeId == action.Id && x.Actor == "reviewer");
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public void ContinueAfterApproval_ActivatesNextApprovalTaskThroughRuntime()
    {
        var definition = new WorkflowDefinition("RUNTIME_SERIAL", "运行时串行审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "复审", configJson: "{\"approver\":\"finance\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, first.Id);
        definition.Connect(first.Id, second.Id);
        definition.Connect(second.Id, end.Id);
        definition.Publish();
        var instanceRepository = new InMemoryInstanceRepository();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(instanceRepository, operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var taskRepository = new InMemoryTaskRepository();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()), operations);
        var tasks = new WorkflowTaskService(taskRepository, instanceService, operations: operations, runtime: runtime);
        runtime.Continue(instance);
        var firstTask = tasks.CreateApprovalTask(instance, first.Id, first.Name, "admin");

        tasks.Approve(firstTask, "admin", "通过");

        var secondTask = Assert.Single(taskRepository.List(status: WorkflowTaskStatus.Pending));
        Assert.Equal(second.Id, secondTask.NodeId);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        tasks.Approve(secondTask, "finance", "通过");
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public void CrossModuleApprovalChain_ConditionReturnAndBusinessActionCompleteInNewRound()
    {
        var definition = new WorkflowDefinition("RUNTIME_CROSS_MODULE_CHAIN", "条件退回业务动作链路");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "金额条件", configJson: "{\"branches\":[{\"key\":\"high\",\"expression\":\"amount > 100\"},{\"key\":\"normal\",\"expression\":\"amount <= 100\"}]}");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "业务初审", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务复审", configJson: $"{{\"approver\":\"finance\",\"returnTargets\":[\"{first.Id}\"]}}");
        var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "回写业务状态", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, condition.Id);
        definition.Connect(condition.Id, first.Id, "high");
        definition.Connect(condition.Id, first.Id, "normal");
        definition.Connect(first.Id, second.Id);
        definition.Connect(second.Id, action.Id);
        definition.Connect(action.Id, end.Id);
        definition.Publish();

        var instanceRepository = new InMemoryInstanceRepository();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(instanceRepository, operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var taskRepository = new InMemoryTaskRepository();
        var handler = new RecordingHandler();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([handler]), new NotificationService(new InMemoryNotificationRepository()), operations);
        var tasks = new WorkflowTaskService(taskRepository, instanceService, operations: operations, runtime: runtime);

        Assert.Equal(WorkflowRuntimeState.WaitingForCondition, runtime.Continue(instance).State);
        Assert.Equal(WorkflowRuntimeState.WaitingForApproval, runtime.Continue(instance, new Dictionary<string, object?> { ["amount"] = 500m }).State);
        var firstTask = Assert.Single(tasks.EnsureCurrentApprovalTask(instance));
        tasks.Approve(firstTask, "admin", "初审通过");
        var secondTask = Assert.Single(taskRepository.List(status: WorkflowTaskStatus.Pending));

        var returned = Assert.Single(tasks.ReturnToNode(secondTask, "finance", first.Id, "补充业务资料"));
        Assert.Equal(WorkflowTaskStatus.Returned, secondTask.Status);
        Assert.Equal(first.Id, returned.NodeId);
        Assert.Equal(2, returned.Round);

        tasks.Approve(returned, "admin", "补充后通过");
        var secondRoundTask = Assert.Single(taskRepository.List(status: WorkflowTaskStatus.Pending));
        tasks.Approve(secondRoundTask, "finance", "复审通过");

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(1, handler.ExecutionCount);
        Assert.Equal(end.Id, instance.CurrentNodeId);
        Assert.Contains(operationRepository.List(instanceId: instance.Id), item => item.Kind == WorkflowOperationKind.Returned);
    }

    [Fact]
    public void Continue_ReexecutesBusinessActionWhenApprovalReturnReentersItsNode()
    {
        var definition = new WorkflowDefinition("RUNTIME_ACTION_RETURN", "退回后重新执行自动动作");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"admin\"}");
        var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "同步业务状态", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "复审", configJson: $"{{\"approver\":\"finance\",\"returnTargets\":[\"{first.Id}\"]}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, first.Id);
        definition.Connect(first.Id, action.Id);
        definition.Connect(action.Id, second.Id);
        definition.Connect(second.Id, end.Id);
        definition.Publish();

        var operations = new WorkflowOperationService(new InMemoryOperationRepository());
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var taskRepository = new InMemoryTaskRepository();
        var handler = new RecordingHandler();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([handler]), new NotificationService(new InMemoryNotificationRepository()), operations);
        var tasks = new WorkflowTaskService(taskRepository, instanceService, operations: operations, runtime: runtime);

        runtime.Continue(instance);
        var firstTask = Assert.Single(tasks.EnsureCurrentApprovalTask(instance));
        tasks.Approve(firstTask, "admin", "初审通过");
        Assert.Equal(1, handler.ExecutionCount);

        var secondTask = Assert.Single(taskRepository.List(status: WorkflowTaskStatus.Pending));
        var returnedFirstTask = Assert.Single(tasks.ReturnToNode(secondTask, "finance", first.Id, "补充资料"));
        tasks.Approve(returnedFirstTask, "admin", "补充后通过");
        Assert.Equal(2, handler.ExecutionCount);

        var secondRoundTask = Assert.Single(taskRepository.List(status: WorkflowTaskStatus.Pending));
        tasks.Approve(secondRoundTask, "finance", "复审通过");
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(2, operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeExecuted).Count(x => x.NodeId == action.Id));
    }

    [Fact]
    public void ContinueAfterApproval_WaitsForAllParallelBranchesThenCompletes()
    {
        var definition = new WorkflowDefinition("RUNTIME_PARALLEL", "运行时并行汇聚");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务审批", configJson: "{\"approver\":\"finance\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, first.Id);
        definition.Connect(split.Id, second.Id);
        definition.Connect(first.Id, join.Id);
        definition.Connect(second.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var operations = new WorkflowOperationService(new InMemoryOperationRepository());
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var taskRepository = new InMemoryTaskRepository();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()), operations);
        var tasks = new WorkflowTaskService(taskRepository, instanceService, operations: operations, runtime: runtime);

        runtime.Continue(instance);
        var pending = tasks.EnsureCurrentApprovalTask(instance);
        Assert.Equal(2, pending.Count);
        tasks.Approve(pending.Single(x => x.NodeId == first.Id), "admin", "部门通过");
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal([second.Id], instance.ActiveNodeIds);

        tasks.Approve(pending.Single(x => x.NodeId == second.Id), "finance", "财务通过");

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(end.Id, instance.CurrentNodeId);
        Assert.Contains(operations.List(instanceId: instance.Id), x => x.Kind == WorkflowOperationKind.NodeEntered && x.NodeId == join.Id);
    }

    [Fact]
    public void Continue_LoopBranchArrivesAtParallelJoinInsteadOfBypassingIt()
    {
        var definition = new WorkflowDefinition("RUNTIME_LOOP_TO_JOIN", "循环分支汇聚");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "人工审批", configJson: "{\"approver\":\"admin\"}");
        var loop = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Loop, "循环出口", configJson: "{\"maxIterations\":1}");
        var retry = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "循环重试", configJson: "{\"recipients\":\"system\",\"content\":\"重试\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, approval.Id);
        definition.Connect(split.Id, loop.Id);
        definition.Connect(approval.Id, join.Id);
        definition.Connect(loop.Id, join.Id, "exit");
        definition.Connect(loop.Id, retry.Id, "repeat");
        definition.Connect(retry.Id, loop.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var operations = new WorkflowOperationService(new InMemoryOperationRepository());
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var taskRepository = new InMemoryTaskRepository();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()), operations);
        var tasks = new WorkflowTaskService(taskRepository, instanceService, operations: operations, runtime: runtime);

        var waiting = runtime.Continue(instance);

        Assert.Equal(WorkflowRuntimeState.WaitingForApproval, waiting.State);
        Assert.Equal([approval.Id], instance.ActiveNodeIds);
        Assert.Contains(loop.Id.ToString(), instance.ParallelJoinArrivalsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(operations.List(instanceId: instance.Id), x => x.Kind == WorkflowOperationKind.NodeEntered && x.NodeId == join.Id);
        var task = Assert.Single(tasks.EnsureCurrentApprovalTask(instance));
        tasks.Approve(task, "admin", "通过");
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Single(operations.List(instanceId: instance.Id), x => x.Kind == WorkflowOperationKind.NodeEntered && x.NodeId == join.Id);
    }

    [Fact]
    public void EnsureCurrentApprovalTask_InParallelBranchesRepairsOnlyMissingBranchAndPreservesTransfer()
    {
        var definition = new WorkflowDefinition("PARALLEL_APPROVAL_SNAPSHOT", "并行审批快照");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务审批", configJson: "{\"approver\":\"finance\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, first.Id);
        definition.Connect(split.Id, second.Id);
        definition.Connect(first.Id, join.Id);
        definition.Connect(second.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instances = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instances);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var repository = new InMemoryTaskRepository();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()));
        var tasks = new WorkflowTaskService(repository, instanceService, runtime: runtime);
        runtime.Continue(instance);
        var initial = tasks.EnsureCurrentApprovalTask(instance);
        tasks.Transfer(initial.Single(x => x.NodeId == first.Id), "admin", "director");
        repository.Items.Remove(initial.Single(x => x.NodeId == second.Id));

        var repaired = tasks.EnsureCurrentApprovalTask(instance);

        Assert.Single(repaired);
        Assert.Equal(second.Id, repaired[0].NodeId);
        Assert.Equal("finance", repaired[0].Assignee);
        var pending = repository.Items.Where(x => x.Status == WorkflowTaskStatus.Pending).ToArray();
        Assert.Equal(2, pending.Length);
        Assert.Contains(pending, x => x.NodeId == first.Id && x.Assignee == "director");
        Assert.Contains(pending, x => x.NodeId == second.Id && x.Assignee == "finance");
        Assert.DoesNotContain(pending, x => x.Assignee == "admin");
    }

    [Fact]
    public void Continue_WhenParallelBusinessActionFails_PreservesOtherBranchAndRetryCanJoin()
    {
        var definition = new WorkflowDefinition("PARALLEL_ACTION_RETRY", "并行自动动作重试");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "自动动作", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "人工审批", configJson: "{\"approver\":\"admin\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, action.Id);
        definition.Connect(split.Id, approval.Id);
        definition.Connect(action.Id, join.Id);
        definition.Connect(approval.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var operations = new WorkflowOperationService(new InMemoryOperationRepository());
        var instances = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instances, operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var handler = new FailOnceHandler();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([handler]), new NotificationService(new InMemoryNotificationRepository()), operations);
        var repository = new InMemoryTaskRepository();
        var tasks = new WorkflowTaskService(repository, instanceService, runtime: runtime);

        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));
        Assert.Equal(new[] { action.Id, approval.Id }.OrderBy(x => x), instance.ActiveNodeIds.OrderBy(x => x));
        Assert.Single(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed), x => x.NodeId == action.Id);

        Assert.Equal(WorkflowRuntimeState.WaitingForApproval, runtime.Retry(instance, "admin").State);
        Assert.Equal([approval.Id], instance.ActiveNodeIds);
        var task = Assert.Single(tasks.EnsureCurrentApprovalTask(instance));
        tasks.Approve(task, "admin", "通过");

        Assert.Equal(2, handler.ExecutionCount);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Single(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeExecuted), x => x.NodeId == action.Id);
    }

    [Fact]
    public void AnyApprovalInParallel_CancelsOnlySameNodeSiblingsThenWaitsForOtherBranch()
    {
        var definition = new WorkflowDefinition("PARALLEL_ANY_APPROVAL", "并行或签审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var anyApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门或签", configJson: "{\"approvers\":[\"admin\",\"finance\"],\"approvalMode\":\"Any\"}");
        var otherApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "法务审批", configJson: "{\"approver\":\"legal\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, anyApproval.Id);
        definition.Connect(split.Id, otherApproval.Id);
        definition.Connect(anyApproval.Id, join.Id);
        definition.Connect(otherApproval.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instances = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instances);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var repository = new InMemoryTaskRepository();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()));
        var tasks = new WorkflowTaskService(repository, instanceService, runtime: runtime);
        runtime.Continue(instance);
        var pending = tasks.EnsureCurrentApprovalTask(instance);

        tasks.Approve(pending.Single(x => x.NodeId == anyApproval.Id && x.Assignee == "admin"), "admin", "部门通过");

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(new[] { otherApproval.Id }, instance.ActiveNodeIds);
        Assert.Contains(repository.Items, x => x.NodeId == anyApproval.Id && x.Assignee == "finance" && x.Status == WorkflowTaskStatus.Cancelled);
        var remaining = Assert.Single(repository.Items, x => x.Status == WorkflowTaskStatus.Pending);
        Assert.Equal(otherApproval.Id, remaining.NodeId);
        Assert.Equal("legal", remaining.Assignee);

        tasks.Approve(remaining, "legal", "法务通过");

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public void MajorityApprovalInParallel_CancelsOnlySameNodeAfterThresholdThenWaitsForOtherBranch()
    {
        var definition = new WorkflowDefinition("PARALLEL_MAJORITY_APPROVAL", "并行多数会签审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var majorityApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门多数会签", configJson: "{\"approvers\":[\"admin\",\"finance\",\"director\"],\"approvalMode\":\"Majority\"}");
        var otherApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "法务审批", configJson: "{\"approver\":\"legal\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, majorityApproval.Id);
        definition.Connect(split.Id, otherApproval.Id);
        definition.Connect(majorityApproval.Id, join.Id);
        definition.Connect(otherApproval.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instances = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instances);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var repository = new InMemoryTaskRepository();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()));
        var tasks = new WorkflowTaskService(repository, instanceService, runtime: runtime);
        runtime.Continue(instance);
        var pending = tasks.EnsureCurrentApprovalTask(instance);

        tasks.Approve(pending.Single(x => x.NodeId == majorityApproval.Id && x.Assignee == "admin"), "admin", "第一票");

        Assert.Contains(majorityApproval.Id, instance.ActiveNodeIds);
        Assert.Contains(repository.Items, x => x.NodeId == majorityApproval.Id && x.Assignee == "finance" && x.Status == WorkflowTaskStatus.Pending);
        Assert.Contains(repository.Items, x => x.NodeId == majorityApproval.Id && x.Assignee == "director" && x.Status == WorkflowTaskStatus.Pending);

        tasks.Approve(repository.Items.Single(x => x.NodeId == majorityApproval.Id && x.Assignee == "finance" && x.Status == WorkflowTaskStatus.Pending), "finance", "第二票");

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal([otherApproval.Id], instance.ActiveNodeIds);
        Assert.Contains(repository.Items, x => x.NodeId == majorityApproval.Id && x.Assignee == "director" && x.Status == WorkflowTaskStatus.Cancelled);
        var remaining = Assert.Single(repository.Items, x => x.Status == WorkflowTaskStatus.Pending);
        Assert.Equal(otherApproval.Id, remaining.NodeId);

        tasks.Approve(remaining, "legal", "法务通过");

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public void Continue_ExecutesAutomaticParallelBranchBeforeWaitingForApproval()
    {
        var definition = new WorkflowDefinition("RUNTIME_PARALLEL_AUTO", "并行自动分支");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "人工审批", configJson: "{\"approver\":\"admin\"}");
        var notification = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "自动通知", configJson: "{\"recipients\":\"finance\",\"content\":\"并行通知\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, approval.Id);
        definition.Connect(split.Id, notification.Id);
        definition.Connect(approval.Id, join.Id);
        definition.Connect(notification.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var notifications = new InMemoryNotificationRepository();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(notifications));

        var result = runtime.Continue(instance);

        Assert.Equal(WorkflowRuntimeState.WaitingForApproval, result.State);
        Assert.Equal(approval.Id, instance.CurrentNodeId);
        Assert.Single(notifications.Items);
        Assert.Equal("finance", notifications.Items[0].Recipient);
        Assert.Equal([approval.Id], instance.ActiveNodeIds);
    }

    [Fact]
    public void Continue_RejectsParallelBranchThatEndsBeforeJoin()
    {
        var definition = new WorkflowDefinition("RUNTIME_PARALLEL_EARLY_END", "并行提前结束");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "人工审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, approval.Id);
        var notification = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "通知", configJson: "{\"recipients\":\"finance\",\"content\":\"通知\"}");
        definition.Connect(split.Id, notification.Id);
        definition.Connect(notification.Id, end.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var notifications = new InMemoryNotificationRepository();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(notifications));

        var error = Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));

        Assert.Contains("ParallelJoin", error.Message);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.DoesNotContain(end.Id, instance.ActiveNodeIds);
        Assert.Contains(approval.Id, instance.ActiveNodeIds);
        Assert.Contains(notification.Id, instance.ActiveNodeIds);
    }

    [Fact]
    public void Continue_ParallelConditionDoesNotBlockApproval_AndAdvancesWhenFieldsProvided()
    {
        var definition = new WorkflowDefinition("RUNTIME_PARALLEL_CONDITION", "并行条件分支");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "人工审批", configJson: "{\"approver\":\"admin\"}");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "金额条件", configJson: "{\"branches\":[{\"key\":\"high\",\"expression\":\"amount > 100\"},{\"key\":\"normal\",\"expression\":\"amount <= 100\"}]}" );
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, approval.Id);
        definition.Connect(split.Id, condition.Id);
        definition.Connect(approval.Id, join.Id);
        definition.Connect(condition.Id, join.Id, "high");
        definition.Connect(condition.Id, join.Id, "normal");
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var notifications = new InMemoryNotificationRepository();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(notifications));

        var waiting = runtime.Continue(instance);
        var afterCondition = runtime.Continue(instance, new Dictionary<string, object?> { ["amount"] = 10m });

        Assert.Equal(WorkflowRuntimeState.WaitingForApproval, waiting.State);
        Assert.Equal(WorkflowRuntimeState.WaitingForApproval, afterCondition.State);
        Assert.Equal([approval.Id], instance.ActiveNodeIds);
    }

    [Fact]
    public void ContinueAfterApproval_SupportsNestedParallelSplitAndJoin()
    {
        var definition = new WorkflowDefinition("RUNTIME_NESTED_PARALLEL", "嵌套并行汇聚");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var outerSplit = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "外层拆分");
        var outerApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "外层审批", configJson: "{\"approver\":\"outer\"}");
        var innerSplit = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "内层拆分");
        var innerFirst = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "内层部门审批", configJson: "{\"approver\":\"department\"}");
        var innerSecond = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "内层财务审批", configJson: "{\"approver\":\"finance\"}");
        var innerJoin = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "内层汇聚");
        var outerJoin = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "外层汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, outerSplit.Id);
        definition.Connect(outerSplit.Id, outerApproval.Id);
        definition.Connect(outerSplit.Id, innerSplit.Id);
        definition.Connect(outerApproval.Id, outerJoin.Id);
        definition.Connect(innerSplit.Id, innerFirst.Id);
        definition.Connect(innerSplit.Id, innerSecond.Id);
        definition.Connect(innerFirst.Id, innerJoin.Id);
        definition.Connect(innerSecond.Id, innerJoin.Id);
        definition.Connect(innerJoin.Id, outerJoin.Id);
        definition.Connect(outerJoin.Id, end.Id);
        definition.Publish();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()));
        var taskRepository = new InMemoryTaskRepository();
        var tasks = new WorkflowTaskService(taskRepository, instanceService, runtime: runtime);

        runtime.Continue(instance);
        var pending = tasks.EnsureCurrentApprovalTask(instance);
        Assert.Equal(3, pending.Count);
        Assert.Equal(3, instance.ActiveNodeIds.Count);

        tasks.Approve(pending.Single(x => x.NodeId == outerApproval.Id), "outer", "外层通过");
        tasks.Approve(pending.Single(x => x.NodeId == innerFirst.Id), "department", "部门通过");
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal([innerSecond.Id], instance.ActiveNodeIds);

        tasks.Approve(pending.Single(x => x.NodeId == innerSecond.Id), "finance", "财务通过");

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(end.Id, instance.CurrentNodeId);
    }

    [Fact]
    public void Continue_ConditionBranchesCanConvergeAtNestedJoin()
    {
        var definition = new WorkflowDefinition("RUNTIME_CONDITION_NESTED_JOIN", "条件分支嵌套汇聚");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var outerSplit = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "外层拆分");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "人工审批", configJson: "{\"approver\":\"admin\"}");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "金额条件", configJson: "{\"branches\":[{\"key\":\"high\",\"expression\":\"amount > 100\"},{\"key\":\"normal\",\"expression\":\"amount <= 100\"}]}");
        var innerJoin = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "条件汇聚");
        var outerJoin = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "外层汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, outerSplit.Id);
        definition.Connect(outerSplit.Id, approval.Id);
        definition.Connect(outerSplit.Id, condition.Id);
        definition.Connect(condition.Id, innerJoin.Id, "high");
        definition.Connect(condition.Id, innerJoin.Id, "normal");
        definition.Connect(innerJoin.Id, outerJoin.Id);
        definition.Connect(approval.Id, outerJoin.Id);
        definition.Connect(outerJoin.Id, end.Id);
        definition.Publish();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()));
        var tasks = new WorkflowTaskService(new InMemoryTaskRepository(), instanceService, runtime: runtime);

        Assert.Equal(WorkflowRuntimeState.WaitingForApproval, runtime.Continue(instance).State);
        Assert.Equal(WorkflowRuntimeState.WaitingForApproval, runtime.Continue(instance, new Dictionary<string, object?> { ["amount"] = 200m }).State);
        Assert.Equal([approval.Id], instance.ActiveNodeIds);
        var pending = tasks.EnsureCurrentApprovalTask(instance);

        tasks.Approve(Assert.Single(pending), "admin", "审批通过");

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(end.Id, instance.CurrentNodeId);
    }

    [Fact]
    public void ApprovingTaskFromNonCurrentNode_DoesNotCompleteRunningInstance()
    {
        var definition = new WorkflowDefinition("RUNTIME_STALE_NODE", "过期节点保护");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "复审", configJson: "{\"approver\":\"finance\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, first.Id);
        definition.Connect(first.Id, second.Id);
        definition.Connect(second.Id, end.Id);
        definition.Publish();
        var instanceRepository = new InMemoryInstanceRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(first.Id);
        instance.AdvanceTo(second.Id);
        var taskRepository = new InMemoryTaskRepository();
        var tasks = new WorkflowTaskService(taskRepository, instanceService);
        var staleTask = tasks.CreateApprovalTask(instance, first.Id, first.Name, "admin");

        var error = Assert.Throws<InvalidOperationException>(() => tasks.Approve(staleTask, "admin", "迟到的处理"));

        Assert.Contains("活动节点", error.Message);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(second.Id, instance.CurrentNodeId);
    }

    [Fact]
    public void Continue_WhenBusinessActionFails_LeavesNodeRunningAndAllowsRetry()
    {
        var definition = CreateActionOnlyDefinition();
        var instanceRepository = new InMemoryInstanceRepository();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(instanceRepository, operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var handler = new FailOnceHandler();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([handler]), new NotificationService(new InMemoryNotificationRepository()), operations);

        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));
        var actionNodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.BusinessAction).Id;
        Assert.Equal(actionNodeId, instance.CurrentNodeId);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);

        var result = runtime.Continue(instance);

        Assert.Equal(WorkflowRuntimeState.Completed, result.State);
        Assert.Equal(2, handler.ExecutionCount);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Single(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed));
        Assert.Single(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeExecuted));
    }

    [Fact]
    public void Continue_WhenAutomaticTransitionCasFails_DoesNotRecordFalseNodeFailure()
    {
        var definition = CreateActionOnlyDefinition();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceRepository = new FailOnSecondInstanceUpdateRepository();
        var instanceService = new WorkflowInstanceService(instanceRepository, operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var runtime = new WorkflowRuntimeService(
            instanceService,
            new WorkflowActionExecutor([new SuccessfulHandler()]),
            new NotificationService(new InMemoryNotificationRepository()),
            operations);

        var error = Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));

        Assert.Contains("状态已变化", error.Message);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Single(instance.ActiveNodeIds);
        Assert.Equal(definition.Nodes.Single(x => x.Type == WorkflowNodeType.BusinessAction).Id, instance.CurrentNodeId);
        Assert.Empty(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed));
    }

    [Fact]
    public void Retry_AutomaticFailureRequiresInitiatorAndResumesActiveNode()
    {
        var definition = CreateActionOnlyDefinition();
        var instanceRepository = new InMemoryInstanceRepository();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(instanceRepository, operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var handler = new FailOnceHandler();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([handler]), new NotificationService(new InMemoryNotificationRepository()), operations);

        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));
        Assert.Throws<InvalidOperationException>(() => runtime.Retry(instance, "finance"));

        var result = runtime.Retry(instance, "admin");

        Assert.Equal(WorkflowRuntimeState.Completed, result.State);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(2, handler.ExecutionCount);
        Assert.Equal("admin", handler.LastActor);
        Assert.Single(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried), x => x.Actor == "admin");
        Assert.Throws<InvalidOperationException>(() => runtime.Retry(instance, "admin"));
        Assert.Single(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried));
    }

    [Fact]
    public void Retry_WhenAutomaticActionFailsAgain_KeepsInstanceRunningAtFailedNode()
    {
        var definition = CreateActionOnlyDefinition();
        var instanceRepository = new InMemoryInstanceRepository();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(instanceRepository, operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var actionNodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.BusinessAction).Id;
        var runtime = new WorkflowRuntimeService(
            instanceService,
            new WorkflowActionExecutor([new AlwaysFailHandler()]),
            new NotificationService(new InMemoryNotificationRepository()),
            operations);

        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));
        Assert.Throws<InvalidOperationException>(() => runtime.Retry(instance, "admin"));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(actionNodeId, instance.CurrentNodeId);
        Assert.Contains(actionNodeId, instance.ActiveNodeIds);
        Assert.NotEmpty(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed));
        Assert.Single(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried), x => x.Actor == "admin");
    }

    [Fact]
    public void Retry_WhenAnotherRequestAlreadyClaimedFailure_DoesNotExecuteNodeAgain()
    {
        var definition = CreateActionOnlyDefinition();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var handler = new FailOnceHandler();
        var runtime = new WorkflowRuntimeService(
            instanceService,
            new WorkflowActionExecutor([handler]),
            new NotificationService(new InMemoryNotificationRepository()),
            operations);

        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));
        var actionNodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.BusinessAction).Id;
        var failure = operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed).Single(x => x.NodeId == actionNodeId);
        operations.Record(instance, WorkflowOperationKind.Retried, "other", "其他请求已抢占", $"workflow-runtime-retried:{instance.Id}:{actionNodeId}:{failure.Id:N}", nodeId: actionNodeId);

        var error = Assert.Throws<InvalidOperationException>(() => runtime.Retry(instance, "admin", failedNodeId: actionNodeId));

        Assert.Contains("其他请求", error.Message);
        Assert.Equal(1, handler.ExecutionCount);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Single(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried));
    }

    [Fact]
    public void Retry_WhenFailureAuditChangesAfterInitialRead_RejectsStaleAttempt()
    {
        var definition = CreateActionOnlyDefinition();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var actionNodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.BusinessAction).Id;
        var transactionBoundary = new InjectingTransactionBoundary();
        var runtime = new WorkflowRuntimeService(
            instanceService,
            new WorkflowActionExecutor([new AlwaysFailHandler()]),
            new NotificationService(new InMemoryNotificationRepository()),
            operations,
            transactionBoundary);

        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));
        var firstFailure = operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed).Single(x => x.NodeId == actionNodeId);
        transactionBoundary.BeforeNextExecution = () => operations.Record(
            instance,
            WorkflowOperationKind.NodeFailed,
            "system",
            "后续失败",
            $"workflow-node-failed:{instance.Id}:{actionNodeId}:newer",
            nodeId: actionNodeId,
            occurredAt: firstFailure.OccurredAt.AddMinutes(1));

        var error = Assert.Throws<InvalidOperationException>(() => runtime.Retry(instance, "admin", failedNodeId: actionNodeId));

        Assert.Contains("失败审计已变化", error.Message);
        Assert.Empty(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried));
        Assert.Equal(2, operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed).Count(x => x.NodeId == actionNodeId));
    }

    [Fact]
    public void Retry_UsesLatestFailureAsStableAttemptKey()
    {
        var definition = CreateActionOnlyDefinition();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var actionNodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.BusinessAction).Id;
        var runtime = new WorkflowRuntimeService(
            instanceService,
            new WorkflowActionExecutor([new AlwaysFailHandler()]),
            new NotificationService(new InMemoryNotificationRepository()),
            operations);

        var firstAttemptAt = new DateTime(2026, 7, 19, 10, 0, 0);
        var firstRetryAt = new DateTime(2026, 7, 19, 10, 1, 0);
        var secondRetryAt = new DateTime(2026, 7, 19, 10, 2, 0);
        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance, occurredAt: firstAttemptAt));
        var firstFailure = operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed).Single(x => x.NodeId == actionNodeId);

        Assert.Throws<InvalidOperationException>(() => runtime.Retry(instance, "admin", firstRetryAt, actionNodeId));
        var firstRetry = operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried).Single();
        Assert.Equal($"workflow-runtime-retried:{instance.Id}:{actionNodeId}:{firstFailure.Id:N}", firstRetry.DedupeKey);

        var secondFailure = operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed)
            .Where(x => x.NodeId == actionNodeId)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.Id)
            .Last();
        Assert.NotEqual(firstFailure.DedupeKey, secondFailure.DedupeKey);

        Assert.Throws<InvalidOperationException>(() => runtime.Retry(instance, "admin", secondRetryAt, actionNodeId));
        var retryRecords = operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried)
            .Where(x => x.NodeId == actionNodeId)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.Id)
            .ToArray();
        Assert.Equal(2, retryRecords.Length);
        Assert.Equal($"workflow-runtime-retried:{instance.Id}:{actionNodeId}:{secondFailure.Id:N}", retryRecords[1].DedupeKey);
    }

    [Fact]
    public void Retry_RejectsEveryTerminalInstanceWithoutWritingAudit()
    {
        foreach (var terminalStatus in new[] { WorkflowInstanceStatus.Completed, WorkflowInstanceStatus.Rejected, WorkflowInstanceStatus.Cancelled })
        {
            var definition = CreateActionOnlyDefinition();
            var operationRepository = new InMemoryOperationRepository();
            var operations = new WorkflowOperationService(operationRepository);
            var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
            var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");

            switch (terminalStatus)
            {
                case WorkflowInstanceStatus.Completed: instance.Complete(); break;
                case WorkflowInstanceStatus.Rejected: instance.Reject(); break;
                case WorkflowInstanceStatus.Cancelled: instance.Cancel(); break;
            }

            var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()), operations);
            Assert.Throws<InvalidOperationException>(() => runtime.Retry(instance, "admin"));
            Assert.Empty(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried));
        }
    }

    [Fact]
    public void Retry_WithFailedNodeId_RetriesTheRequestedParallelBranch()
    {
        var definition = new WorkflowDefinition("RUNTIME_TARGETED_RETRY", "并行失败节点定向重试");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var firstAction = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "第一动作", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var secondAction = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "第二动作", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, firstAction.Id);
        definition.Connect(split.Id, secondAction.Id);
        definition.Connect(firstAction.Id, join.Id);
        definition.Connect(secondAction.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();

        var instanceRepository = new InMemoryInstanceRepository();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(instanceRepository, operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var runtime = new WorkflowRuntimeService(
            instanceService,
            new WorkflowActionExecutor([new AlwaysFailHandler()]),
            new NotificationService(new InMemoryNotificationRepository()),
            operations);

        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));
        Assert.Contains(firstAction.Id, instance.ActiveNodeIds);
        Assert.Contains(secondAction.Id, instance.ActiveNodeIds);
        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance, preferredNodeId: secondAction.Id));

        Assert.Throws<InvalidOperationException>(() => runtime.Retry(instance, "admin", failedNodeId: secondAction.Id));

        Assert.Contains(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried), x => x.NodeId == secondAction.Id);
        Assert.DoesNotContain(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried), x => x.NodeId == firstAction.Id);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Contains(secondAction.Id, instance.ActiveNodeIds);
    }

    [Fact]
    public void Retry_WithUnknownFailedNodeId_DoesNotFallbackToAnotherNode()
    {
        var definition = CreateActionOnlyDefinition();
        var instanceRepository = new InMemoryInstanceRepository();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(instanceRepository, operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var runtime = new WorkflowRuntimeService(
            instanceService,
            new WorkflowActionExecutor([new AlwaysFailHandler()]),
            new NotificationService(new InMemoryNotificationRepository()),
            operations);

        Assert.Throws<InvalidOperationException>(() => runtime.Continue(instance));
        Assert.Throws<InvalidOperationException>(() => runtime.Retry(instance, "admin", failedNodeId: Guid.CreateVersion7()));

        Assert.Empty(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried));
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
    }

    [Fact]
    public void Continue_ConditionNodeWaitsForFields_ThenUsesSnapshotBranch()
    {
        var definition = new WorkflowDefinition("RUNTIME_CONDITION", "条件运行时");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "金额判断", configJson: "{\"branches\":[{\"key\":\"high\",\"expression\":\"amount > 10000\"},{\"key\":\"normal\",\"expression\":\"amount <= 10000\"}],\"defaultKey\":\"normal\"}");
        var high = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "高金额结束");
        var normal = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "普通结束");
        definition.Connect(start.Id, condition.Id);
        definition.Connect(condition.Id, high.Id, "high");
        definition.Connect(condition.Id, normal.Id, "normal");
        definition.Publish();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()));

        Assert.Equal(WorkflowRuntimeState.WaitingForCondition, runtime.Continue(instance).State);
        var result = runtime.Continue(instance, new Dictionary<string, object?> { ["amount"] = 12000m });

        Assert.Equal(WorkflowRuntimeState.Completed, result.State);
        Assert.Equal(high.Id, instance.CurrentNodeId);
    }

    [Fact]
    public void ContinueAfterCondition_OnlyAdvancesSpecifiedActiveCondition()
    {
        var definition = new WorkflowDefinition("RUNTIME_TARGETED_CONDITION", "条件节点定向推进");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "金额条件", configJson: "{\"branches\":[{\"key\":\"high\",\"expression\":\"amount > 100\"},{\"key\":\"normal\",\"expression\":\"amount <= 100\"}]}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "风险条件", configJson: "{\"branches\":[{\"key\":\"high\",\"expression\":\"risk > 0\"},{\"key\":\"normal\",\"expression\":\"risk <= 0\"}]}");
        var firstApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "金额审批", configJson: "{\"approver\":\"admin\"}");
        var secondApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "风险审批", configJson: "{\"approver\":\"finance\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, first.Id);
        definition.Connect(split.Id, second.Id);
        definition.Connect(first.Id, firstApproval.Id, "high");
        definition.Connect(first.Id, firstApproval.Id, "normal");
        definition.Connect(second.Id, secondApproval.Id, "high");
        definition.Connect(second.Id, secondApproval.Id, "normal");
        definition.Connect(firstApproval.Id, join.Id);
        definition.Connect(secondApproval.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()));

        Assert.Equal(WorkflowRuntimeState.WaitingForCondition, runtime.Continue(instance).State);
        var result = runtime.ContinueAfterCondition(instance, first.Id, new Dictionary<string, object?> { ["amount"] = 200m });

        Assert.Equal(WorkflowRuntimeState.WaitingForApproval, result.State);
        Assert.Contains(second.Id, instance.ActiveNodeIds);
        Assert.Contains(firstApproval.Id, instance.ActiveNodeIds);
        Assert.DoesNotContain(first.Id, instance.ActiveNodeIds);
        Assert.Throws<InvalidOperationException>(() => runtime.ContinueAfterCondition(instance, first.Id, new Dictionary<string, object?> { ["amount"] = 200m }));
    }

    [Fact]
    public void ContinueAfterCondition_PropagatesActorToAutomaticBusinessAction()
    {
        var definition = new WorkflowDefinition("RUNTIME_CONDITION_ACTOR", "条件分支操作者");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "条件", configJson: "{\"branches\":[{\"key\":\"submit\",\"expression\":\"approved == true\"},{\"key\":\"skip\",\"expression\":\"approved == false\"}]}");
        var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "回写", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, condition.Id);
        definition.Connect(condition.Id, action.Id, "submit");
        definition.Connect(condition.Id, end.Id, "skip");
        definition.Connect(action.Id, end.Id);
        definition.Publish();

        var operations = new WorkflowOperationService(new InMemoryOperationRepository());
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
        var handler = new ActorCapturingHandler();
        var runtime = new WorkflowRuntimeService(
            instanceService,
            new WorkflowActionExecutor([handler]),
            new NotificationService(new InMemoryNotificationRepository()),
            operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());

        Assert.Equal(WorkflowRuntimeState.WaitingForCondition, runtime.Continue(instance).State);
        var result = runtime.ContinueAfterCondition(instance, condition.Id, new Dictionary<string, object?> { ["approved"] = true }, actor: "reviewer");

        Assert.Equal(WorkflowRuntimeState.Completed, result.State);
        Assert.Equal("reviewer", handler.LastActor);
        Assert.Single(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeExecuted), x => x.NodeId == action.Id && x.Actor == "reviewer");
    }

    [Fact]
    public void Continue_ConditionWithoutMatch_RemainsWaitingAndCanRetry()
    {
        var definition = new WorkflowDefinition("RUNTIME_CONDITION_RETRY", "条件无命中可重试");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "金额条件", configJson: "{\"branches\":[{\"key\":\"high\",\"expression\":\"amount > 100\"},{\"key\":\"normal\",\"expression\":\"amount <= 100\"}]}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, condition.Id);
        definition.Connect(condition.Id, end.Id, "high");
        definition.Connect(condition.Id, end.Id, "normal");
        definition.Publish();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()));

        Assert.Equal(WorkflowRuntimeState.WaitingForCondition, runtime.Continue(instance).State);
        var waiting = runtime.Continue(instance, new Dictionary<string, object?>());

        Assert.Equal(WorkflowRuntimeState.WaitingForCondition, waiting.State);
        Assert.Equal(condition.Id, waiting.CurrentNodeId);
        Assert.Contains(condition.Id, instance.ActiveNodeIds);
        Assert.Equal(WorkflowRuntimeState.Completed, runtime.Continue(instance, new Dictionary<string, object?> { ["amount"] = 200m }).State);
    }

    [Fact]
    public void Continue_SkipsAlreadyRecordedBusinessActionExecution()
    {
        var definition = CreateActionOnlyDefinition();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var actionNodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.BusinessAction).Id;
        instanceService.Advance(instance, actionNodeId);
        operations.Record(instance, WorkflowOperationKind.NodeExecuted, "system", "已执行", $"workflow-node-executed:{instance.Id}:{actionNodeId}", nodeId: actionNodeId);
        var handler = new RecordingHandler();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([handler]), new NotificationService(new InMemoryNotificationRepository()), operations);

        runtime.Continue(instance);

        Assert.Equal(0, handler.ExecutionCount);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Single(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeExecuted));
    }

    [Fact]
    public void Retry_DoesNotExposeStaleFailureAfterNodeWasExecuted()
    {
        var definition = CreateActionOnlyDefinition();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
        var actionNodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.BusinessAction).Id;
        instanceService.Advance(instance, actionNodeId);
        operations.Record(instance, WorkflowOperationKind.NodeFailed, "system", "历史失败", $"workflow-node-failed:{instance.Id}:{actionNodeId}:old", nodeId: actionNodeId, occurredAt: new DateTime(2026, 7, 18, 10, 0, 0));
        operations.Record(instance, WorkflowOperationKind.NodeExecuted, "system", "后来成功", $"workflow-node-executed:{instance.Id}:{actionNodeId}:root", nodeId: actionNodeId, occurredAt: new DateTime(2026, 7, 18, 10, 1, 0));
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()), operations);

        Assert.Empty(runtime.GetRetryableNodeIds(instance));
        var error = Assert.Throws<InvalidOperationException>(() => runtime.Retry(instance, "admin"));

        Assert.Contains("没有可重试的失败自动节点", error.Message);
        Assert.Empty(operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried));
    }

    [Fact]
    public void Continue_ControlledLoop_RepeatsUntilConfiguredLimitThenExits()
    {
        var definition = new WorkflowDefinition("RUNTIME_LOOP", "受控循环");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "补充审批", configJson: "{\"approver\":\"admin\"}");
        var loop = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Loop, "最多两轮", configJson: "{\"maxIterations\":2}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, loop.Id);
        definition.Connect(loop.Id, approval.Id, WorkflowLoopConfiguration.RepeatKey);
        definition.Connect(loop.Id, end.Id, WorkflowLoopConfiguration.ExitKey);
        definition.Publish();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var notifications = new InMemoryNotificationRepository();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(notifications));

        Assert.Equal(WorkflowRuntimeState.WaitingForApproval, runtime.Continue(instance).State);
        Assert.Equal(WorkflowRuntimeState.WaitingForApproval, runtime.ContinueAfterApproval(instance, approval.Id).State);
        Assert.Equal(approval.Id, instance.CurrentNodeId);
        Assert.Contains($"\"{loop.Id}\":1", instance.LoopIterationsJson);

        Assert.Equal(WorkflowRuntimeState.Completed, runtime.ContinueAfterApproval(instance, approval.Id).State);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(end.Id, instance.CurrentNodeId);
        Assert.Contains($"\"{loop.Id}\":2", instance.LoopIterationsJson);
    }

    [Fact]
    public void DefinitionValidator_RejectsCycleWithoutLoopRepeatBranch()
    {
        var definition = new WorkflowDefinition("RUNTIME_UNCONTROLLED_CYCLE", "非受控循环");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "是否结束", configJson: "{\"branches\":[{\"key\":\"repeat\",\"expression\":\"amount > 0\"},{\"key\":\"exit\",\"expression\":\"amount <= 0\"}]}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, condition.Id);
        definition.Connect(condition.Id, approval.Id, "repeat");
        definition.Connect(condition.Id, end.Id, "exit");

        var result = definition.Validate();

        Assert.Contains(result.Errors, error => error.Contains("Loop 节点的 repeat", StringComparison.Ordinal));
    }

    [Fact]
    public void Continue_ControlledAutomaticLoop_AllowsReenteringAutomaticNodeThenExits()
    {
        var definition = new WorkflowDefinition("RUNTIME_AUTO_LOOP", "自动受控循环");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var loop = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Loop, "最多三轮", configJson: "{\"maxIterations\":3}");
        var notification = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "循环通知", configJson: "{\"recipients\":\"admin\",\"content\":\"循环中\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, loop.Id);
        definition.Connect(loop.Id, notification.Id, WorkflowLoopConfiguration.RepeatKey);
        definition.Connect(notification.Id, loop.Id);
        definition.Connect(loop.Id, end.Id, WorkflowLoopConfiguration.ExitKey);
        definition.Publish();
        var operationRepository = new InMemoryOperationRepository();
        var operations = new WorkflowOperationService(operationRepository);
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository(), operations);
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var notifications = new InMemoryNotificationRepository();
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(notifications), operations);

        var result = runtime.Continue(instance);

        Assert.Equal(WorkflowRuntimeState.Completed, result.State);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Contains($"\"{loop.Id}\":3", instance.LoopIterationsJson);
        Assert.Equal(2, notifications.Items.Count);
        Assert.Equal(3, operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeCompleted).Count(x => x.NodeId == loop.Id));
        Assert.Equal(3, operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeEntered).Count(x => x.NodeId == loop.Id));
    }

    [Fact]
    public void Continue_ControlledAutomaticLoop_AllowsMaximumConfiguredIterations()
    {
        var definition = new WorkflowDefinition("RUNTIME_AUTO_LOOP_MAX", "自动循环最大次数");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var loop = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Loop, "最大一百轮", configJson: "{\"maxIterations\":100}");
        var notification = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "循环通知", configJson: "{\"recipients\":\"admin\",\"content\":\"循环中\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, loop.Id);
        definition.Connect(loop.Id, notification.Id, WorkflowLoopConfiguration.RepeatKey);
        definition.Connect(notification.Id, loop.Id);
        definition.Connect(loop.Id, end.Id, WorkflowLoopConfiguration.ExitKey);
        definition.Publish();
        var instanceService = new WorkflowInstanceService(new InMemoryInstanceRepository());
        var instance = instanceService.Start(definition, "custom.document", Guid.CreateVersion7());
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new InMemoryNotificationRepository()));

        var result = runtime.Continue(instance);

        Assert.Equal(WorkflowRuntimeState.Completed, result.State);
        Assert.Contains($"\"{loop.Id}\":100", instance.LoopIterationsJson);
    }

    private static WorkflowDefinition CreateAutomaticDefinition()
    {
        var definition = new WorkflowDefinition("RUNTIME_AUTO", "自动节点运行时");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var notification = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "通知", configJson: "{\"recipients\":[\"ADMIN\",\"admin\",\"finance\"],\"title\":\"流程已提交\",\"content\":\"请关注\"}");
        var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "业务动作", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, notification.Id);
        definition.Connect(notification.Id, action.Id);
        definition.Connect(action.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private static WorkflowDefinition CreateApprovalToAutomaticDefinition()
    {
        var definition = new WorkflowDefinition("RUNTIME_APPROVAL", "审批后自动节点");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var notification = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "完成通知", configJson: "{\"recipients\":\"admin\",\"content\":\"已通过\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, notification.Id);
        definition.Connect(notification.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private static WorkflowDefinition CreateActionOnlyDefinition()
    {
        var definition = new WorkflowDefinition("RUNTIME_RETRY", "动作重试");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "动作", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, action.Id);
        definition.Connect(action.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private sealed class RecordingHandler : IWorkflowActionHandler
    {
        public int ExecutionCount { get; private set; }
        public bool CanHandle(string businessType) => businessType == "custom.document";
        public void Execute(WorkflowActionContext context, WorkflowActionDefinition action) => ExecutionCount++;
    }

    private sealed class ActorCapturingHandler : IWorkflowActionHandler
    {
        public string? LastActor { get; private set; }
        public bool CanHandle(string businessType) => businessType == "custom.document";
        public void Execute(WorkflowActionContext context, WorkflowActionDefinition action) => LastActor = context.Actor;
    }

    private sealed class FailOnceHandler : IWorkflowActionHandler
    {
        public int ExecutionCount { get; private set; }
        public string? LastActor { get; private set; }
        public bool CanHandle(string businessType) => businessType == "custom.document";
        public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
        {
            ExecutionCount++;
            LastActor = context.Actor;
            if (ExecutionCount == 1) throw new InvalidOperationException("模拟业务动作失败");
        }
    }

    private sealed class AlwaysFailHandler : IWorkflowActionHandler
    {
        public bool CanHandle(string businessType) => businessType == "custom.document";
        public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
            => throw new InvalidOperationException("模拟重试后业务动作仍失败");
    }

    private sealed class SuccessfulHandler : IWorkflowActionHandler
    {
        public bool CanHandle(string businessType) => businessType == "custom.document";
        public void Execute(WorkflowActionContext context, WorkflowActionDefinition action) { }
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

    private sealed class LockingInstanceRepository : IWorkflowInstanceRepository, IWorkflowInstanceLockRepository
    {
        private readonly List<WorkflowInstance> items = [];

        public int LockCount { get; private set; }
        public bool RejectNextLock { get; set; }

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

        public void Update(WorkflowInstance instance) { }

        public bool TryUpdate(WorkflowInstance instance)
        {
            var nextRevision = checked(instance.Revision + 1);
            Update(instance);
            instance.MarkPersistedRevision(nextRevision);
            return true;
        }

        public void LockForUpdate(WorkflowInstance instance)
        {
            LockCount++;
            if (RejectNextLock)
            {
                RejectNextLock = false;
                throw new InvalidOperationException("模拟实例行锁失败");
            }
        }
    }

    private sealed class ThrowingCompletionInstanceRepository : IWorkflowInstanceRepository
    {
        private readonly List<WorkflowInstance> items = [];
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
            => items.Where(x => businessType is null || x.BusinessType == businessType).Where(x => businessId is null || x.BusinessId == businessId).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowInstance instance) => items.Add(instance);
        public bool TryAdd(WorkflowInstance instance) { if (items.Any(x => x.Id == instance.Id)) return false; Add(instance); return true; }
        public void Update(WorkflowInstance instance)
        {
            if (instance.Status == WorkflowInstanceStatus.Completed)
                throw new InvalidOperationException("模拟终态实例持久化失败");
        }
        public bool TryUpdate(WorkflowInstance instance) { var nextRevision = checked(instance.Revision + 1); Update(instance); instance.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class FailOnSecondInstanceUpdateRepository : IWorkflowInstanceRepository
    {
        private readonly List<WorkflowInstance> items = [];
        private int updateCount;
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
            => items.Where(x => businessType is null || x.BusinessType == businessType).Where(x => businessId is null || x.BusinessId == businessId).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowInstance instance) => items.Add(instance);
        public bool TryAdd(WorkflowInstance instance) { if (items.Any(x => x.Id == instance.Id)) return false; Add(instance); return true; }
        public void Update(WorkflowInstance instance) { }
        public bool TryUpdate(WorkflowInstance instance)
        {
            if (Interlocked.Increment(ref updateCount) == 2) return false;
            var nextRevision = checked(instance.Revision + 1);
            instance.MarkPersistedRevision(nextRevision);
            return true;
        }
    }

    private sealed class InjectingTransactionBoundary : IWorkflowTransactionBoundary
    {
        public Action? BeforeNextExecution { get; set; }

        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            var before = BeforeNextExecution;
            BeforeNextExecution = null;
            before?.Invoke();
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

    private sealed class InMemoryOperationRepository : IWorkflowOperationRepository
    {
        private readonly List<WorkflowOperation> items = [];
        public IReadOnlyList<WorkflowOperation> List(Guid? instanceId = null, string? businessType = null, Guid? businessId = null, WorkflowOperationKind? kind = null)
            => items.Where(x => instanceId is null || x.InstanceId == instanceId).Where(x => businessType is null || x.BusinessType == businessType).Where(x => businessId is null || x.BusinessId == businessId).Where(x => kind is null || x.Kind == kind).ToArray();
        public WorkflowOperation? FindByDedupeKey(string dedupeKey) => items.FirstOrDefault(x => x.DedupeKey == dedupeKey);
        public void Add(WorkflowOperation operation) => items.Add(operation);
        public bool TryAdd(WorkflowOperation operation)
        {
            if (items.Any(x => x.DedupeKey == operation.DedupeKey)) return false;
            items.Add(operation);
            return true;
        }
    }

    private sealed class InMemoryTaskRepository : IWorkflowTaskRepository, IWorkflowTaskCompensationRepository
    {
        private readonly List<WorkflowTask> items = [];
        public List<WorkflowTask> Items => items;
        public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
            => items.Where(x => instanceId is null || x.InstanceId == instanceId).Where(x => assignee is null || x.Assignee.Equals(assignee, StringComparison.OrdinalIgnoreCase)).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowTask task) => items.Add(task);
        public void Remove(Guid taskId) => items.RemoveAll(x => x.Id == taskId);
        public bool TryAdd(WorkflowTask task) { if (items.Any(x => x.Id == task.Id)) return false; Add(task); return true; }
        public void Update(WorkflowTask task) { }
        public bool TryUpdate(WorkflowTask task) { var nextRevision = checked(task.Revision + 1); Update(task); task.MarkPersistedRevision(nextRevision); return true; }
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

    private sealed class NestedTransactionBoundary : IWorkflowTransactionBoundary
    {
        private sealed class Scope
        {
            public List<Action<Exception>> Rollbacks { get; } = [];
            public List<Action> Commits { get; } = [];
        }

        private readonly Stack<Scope> scopes = [];

        public void Execute(Action operation, Action<Exception>? afterRollback = null)
            => Execute(operation, afterRollback, null);

        public void Execute(Action operation, Action<Exception>? afterRollback, Action? afterCommit)
        {
            var scope = new Scope();
            if (afterRollback is not null) scope.Rollbacks.Add(afterRollback);
            if (afterCommit is not null) scope.Commits.Add(afterCommit);
            scopes.Push(scope);
            try
            {
                operation();
            }
            catch (Exception exception)
            {
                scopes.Pop();
                foreach (var callback in scope.Rollbacks.AsEnumerable().Reverse().ToArray()) callback(exception);
                throw;
            }

            scopes.Pop();
            if (scopes.Count > 0)
            {
                scopes.Peek().Rollbacks.AddRange(scope.Rollbacks);
                scopes.Peek().Commits.AddRange(scope.Commits);
                return;
            }

            foreach (var callback in scope.Commits) callback();
        }
    }

    private sealed class ThrowingNotificationRepository : INotificationRepository
    {
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => [];
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => null;
        public void Add(WorkNotification notification) => throw new InvalidOperationException("模拟通知写入失败");
        public bool TryAdd(WorkNotification notification) => throw new InvalidOperationException("模拟通知写入失败");
        public void Update(WorkNotification notification) => throw new InvalidOperationException("模拟通知更新失败");
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => throw new InvalidOperationException("模拟通知删除失败");
    }
}
