using FreeSql;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.Workflow;

namespace VelrixWorkHub.Domain.Tests;

public sealed class FreeSqlWorkflowInstanceRepositoryTests
{
    [Fact]
    public void Crud_RoundTripsImmutableDefinitionSnapshotAndTerminalStatus()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-workflow-instance-{Guid.NewGuid():N}.db");
        try
        {
            using var fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
                .UseAutoSyncStructure(true)
                .Build();
            var repository = new FreeSqlWorkflowInstanceRepository(fsql);
            var definition = new WorkflowDefinition("CRM_CONTRACT", "合同审批");
            var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
            var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
            definition.Connect(start.Id, end.Id);
            definition.Publish(new DateTime(2026, 7, 14, 10, 30, 0));
            var instance = WorkflowInstance.Start(definition, "crm.contract", Guid.CreateVersion7(), new DateTime(2026, 7, 14, 10, 31, 0), "admin");

            repository.Add(instance);
            fsql.Update<WorkflowInstanceRecord>().Set(x => x.Revision, 0L).Where(x => x.Id == instance.Id).ExecuteAffrows();
            WorkflowSchemaMigration.BackfillInitialRevisions(fsql);
            var loaded = Assert.Single(repository.List(businessType: "crm.contract"));
            Assert.Equal(instance.Id, loaded.Id);
            Assert.Equal(instance.DefinitionSnapshotJson, loaded.DefinitionSnapshotJson);
            Assert.Equal("admin", loaded.StartedBy);
            Assert.Equal(instance.StartedAt, loaded.StartedAt);
            Assert.Equal(1, loaded.Revision);

            var stale = Assert.Single(repository.List(businessType: "crm.contract"));
            loaded.Complete(new DateTime(2026, 7, 14, 10, 32, 0));
            repository.Update(loaded);
            Assert.Equal(2, loaded.Revision);
            var completed = Assert.Single(repository.List(status: WorkflowInstanceStatus.Completed));
            Assert.Equal(WorkflowInstanceStatus.Completed, completed.Status);
            Assert.Equal(loaded.CompletedAt, completed.CompletedAt);
            Assert.Equal(2, completed.Revision);

            var service = new WorkflowInstanceService(repository);
            var error = Assert.Throws<InvalidOperationException>(() => service.Complete(stale));
            Assert.Contains("状态已变化", error.Message);
            Assert.Equal(WorkflowInstanceStatus.Running, stale.Status);
            Assert.Equal(1, stale.Revision);

            var resubmitted = WorkflowInstance.Start(definition, "crm.contract", instance.BusinessId, new DateTime(2026, 7, 14, 10, 33, 0), "admin", instance.Id);
            repository.Add(resubmitted);
            var loadedResubmitted = Assert.Single(repository.List(status: WorkflowInstanceStatus.Running));
            Assert.Equal(instance.Id, loadedResubmitted.PreviousInstanceId);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public void Cas_RejectsStaleNodeAdvanceWithoutMutatingTheStaleSnapshot()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-workflow-instance-cas-{Guid.NewGuid():N}.db");
        try
        {
            using var fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
                .UseAutoSyncStructure(true)
                .Build();
            var repository = new FreeSqlWorkflowInstanceRepository(fsql);
            var definition = new WorkflowDefinition("CAS_FLOW", "实例并发");
            var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
            var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
            var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
            definition.Connect(start.Id, approval.Id);
            definition.Connect(approval.Id, end.Id);
            definition.Publish(new DateTime(2026, 7, 16, 10, 0, 0));
            var instance = WorkflowInstance.Start(definition, "cas.flow", Guid.CreateVersion7(), startedBy: "admin");
            repository.Add(instance);

            var first = Assert.Single(repository.List(businessType: "cas.flow"));
            var stale = Assert.Single(repository.List(businessType: "cas.flow"));
            var service = new WorkflowInstanceService(repository);
            service.Advance(first, approval.Id);

            var error = Assert.Throws<InvalidOperationException>(() => service.Advance(stale, approval.Id));
            Assert.Contains("状态已变化", error.Message);
            Assert.Equal(start.Id, stale.CurrentNodeId);
            Assert.Equal(1, stale.Revision);

            var persisted = Assert.Single(repository.List(businessType: "cas.flow"));
            Assert.Equal(approval.Id, persisted.CurrentNodeId);
            Assert.Equal(2, persisted.Revision);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }
}
