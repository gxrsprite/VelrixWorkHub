using FreeSql;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.Workflow;

namespace VelrixWorkHub.Domain.Tests;

public sealed class FreeSqlWorkflowDefinitionRepositoryTests
{
    [Fact]
    public void Crud_RoundTripsWorkflowNodesConnectionsAndStatus()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-workflow-{Guid.NewGuid():N}.db");
        try
        {
            using var fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
                .UseAutoSyncStructure(true)
                .Build();
            var repository = new FreeSqlWorkflowDefinitionRepository(fsql);
            var definition = new WorkflowDefinition("PMP_CHANGE", "项目变更审批", description: "项目变更发布前审批");
            var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始", 10, 20);
            var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "项目经理审批", 120, 20, "{\"approver\":\"project-manager\"}");
            var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束", 240, 20);
            definition.Connect(start.Id, approval.Id);
            definition.Connect(approval.Id, end.Id);

            repository.Add(definition);
            var loaded = Assert.Single(repository.List("PMP_CHANGE"));
            Assert.Equal(definition.Id, loaded.Id);
            Assert.Equal(3, loaded.Nodes.Count);
            Assert.Equal(2, loaded.Connections.Count);
            Assert.Contains("project-manager", loaded.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).ConfigJson);

            loaded.Publish(new DateTime(2026, 7, 14, 9, 0, 0));
            repository.Update(loaded);
            var published = Assert.Single(repository.List(status: WorkflowDefinitionStatus.Published));
            Assert.Equal(WorkflowDefinitionStatus.Published, published.Status);
            Assert.Equal(1, published.VersionNumber);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }
}
