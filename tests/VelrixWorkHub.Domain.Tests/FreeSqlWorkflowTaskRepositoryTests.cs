using FreeSql;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.Workflow;

namespace VelrixWorkHub.Domain.Tests;

public sealed class FreeSqlWorkflowTaskRepositoryTests
{
    [Fact]
    public void Crud_RoundTripsTransferredStatusAndStoresEnumName()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-workflow-task-{Guid.NewGuid():N}.db");
        try
        {
            using var fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
                .UseAutoSyncStructure(true)
                .Build();
            var repository = new FreeSqlWorkflowTaskRepository(fsql);
            var definition = new WorkflowDefinition("TASK_PERSISTENCE", "待办持久化");
            var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
            var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
            var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
            definition.Connect(start.Id, approval.Id);
            definition.Connect(approval.Id, end.Id);
            definition.Publish();
            var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7(), startedBy: "admin");
            var task = new WorkflowTask(instance, approval.Id, approval.Name, "admin");
            task.Transfer("admin", "finance", "需要财务复核", new DateTime(2026, 7, 15, 14, 0, 0));

            repository.Add(task);
            fsql.Update<WorkflowTaskRecord>().Set(x => x.Revision, 0L).Where(x => x.Id == task.Id).ExecuteAffrows();
            WorkflowSchemaMigration.BackfillInitialRevisions(fsql);

            var loaded = Assert.Single(repository.List(instance.Id));
            var storedStatus = fsql.Ado.QuerySingle<string>($"select Status from WorkflowTask where Id = '{task.Id}'");
            Assert.Equal(WorkflowTaskStatus.Transferred, loaded.Status);
            Assert.Equal("finance", loaded.TransferTarget);
            Assert.Equal("Transferred", storedStatus);
            Assert.Equal(1, loaded.Revision);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }

    [Fact]
    public void Cas_ReservesPendingTaskAndRejectsStaleDecision()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-workflow-task-cas-{Guid.NewGuid():N}.db");
        try
        {
            using var fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
                .UseAutoSyncStructure(true)
                .Build();
            var repository = new FreeSqlWorkflowTaskRepository(fsql);
            var definition = new WorkflowDefinition("TASK_CAS", "待办并发");
            var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
            var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
            var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
            definition.Connect(start.Id, approval.Id);
            definition.Connect(approval.Id, end.Id);
            definition.Publish();
            var instance = WorkflowInstance.Start(definition, "task.cas", Guid.CreateVersion7(), startedBy: "admin");
            var task = new WorkflowTask(instance, approval.Id, approval.Name, "admin");
            repository.Add(task);

            var first = Assert.Single(repository.List(instance.Id));
            var stale = Assert.Single(repository.List(instance.Id));
            Assert.True(repository.TryUpdate(first));
            Assert.Equal(2, first.Revision);

            var service = new WorkflowTaskService(repository);
            var error = Assert.Throws<InvalidOperationException>(() => service.Approve(stale, "admin", "重复审批"));
            Assert.Contains("状态已变化", error.Message);
            Assert.Equal(WorkflowTaskStatus.Pending, stale.Status);
            Assert.Equal(1, stale.Revision);

            first.Approve("admin", "首次审批");
            repository.Update(first);
            var persisted = Assert.Single(repository.List(instance.Id));
            Assert.Equal(WorkflowTaskStatus.Approved, persisted.Status);
            Assert.Equal(3, persisted.Revision);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }

    [Fact]
    public void Transaction_RollsBackTaskReservationWhenWorkflowActionFails()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-workflow-task-transaction-{Guid.NewGuid():N}.db");
        try
        {
            using var fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
                .UseAutoSyncStructure(true)
                .Build();
            var taskRepository = new FreeSqlWorkflowTaskRepository(fsql);
            var instanceRepository = new FreeSqlWorkflowInstanceRepository(fsql);
            var instanceService = new WorkflowInstanceService(instanceRepository);
            var definition = new WorkflowDefinition("TASK_TRANSACTION", "待办事务", description: "动作失败回滚");
            var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
            var approval = definition.AddNode(
                Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批",
                configJson: "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
            var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
            definition.Connect(start.Id, approval.Id);
            definition.Connect(approval.Id, end.Id);
            definition.Publish();
            var instance = instanceService.Start(definition, "transaction.document", Guid.CreateVersion7(), startedBy: "admin");
            var task = new WorkflowTask(instance, approval.Id, approval.Name, "admin");
            taskRepository.Add(task);

            var service = new WorkflowTaskService(
                taskRepository,
                instanceService,
                new WorkflowActionExecutor([new ThrowingActionHandler()]),
                transactions: new FreeSqlWorkflowTransactionBoundary(fsql));

            Assert.Throws<InvalidOperationException>(() => service.Approve(task, "admin", "事务失败"));

            var persisted = Assert.Single(taskRepository.List(instance.Id));
            Assert.Equal(WorkflowTaskStatus.Pending, persisted.Status);
            Assert.Equal(1, persisted.Revision);
            Assert.Equal(WorkflowTaskStatus.Pending, task.Status);
            Assert.Equal(1, task.Revision);
            Assert.Equal(WorkflowInstanceStatus.Running, Assert.Single(instanceRepository.List(businessId: instance.BusinessId)).Status);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }

    private sealed class ThrowingActionHandler : IWorkflowActionHandler
    {
        public bool CanHandle(string businessType) => businessType == "transaction.document";
        public void Execute(WorkflowActionContext context, WorkflowActionDefinition action) => throw new InvalidOperationException("模拟业务动作失败");
    }
}
