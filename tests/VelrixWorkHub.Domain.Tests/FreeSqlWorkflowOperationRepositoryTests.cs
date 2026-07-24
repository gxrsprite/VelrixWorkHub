using FreeSql;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.Workflow;

namespace VelrixWorkHub.Domain.Tests;

public sealed class FreeSqlWorkflowOperationRepositoryTests
{
    [Fact]
    public void Crud_RoundTripsOperationAndStoresEnumName()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"velrix-workflow-operation-{Guid.NewGuid():N}.db");
        try
        {
            using var fsql = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
                .UseAutoSyncStructure(true)
                .Build();
            var repository = new FreeSqlWorkflowOperationRepository(fsql);
            var instanceId = Guid.CreateVersion7();
            var taskId = Guid.CreateVersion7();
            var operation = new WorkflowOperation(instanceId, taskId, Guid.CreateVersion7(), nameof(SalesContract), Guid.CreateVersion7(), WorkflowOperationKind.Transferred, "admin", "finance", "请复核", "workflow-task-transferred:1", new DateTime(2026, 7, 15, 10, 0, 0));

            repository.Add(operation);

            var loaded = Assert.Single(repository.List(instanceId: instanceId));
            var storedKind = fsql.Ado.QuerySingle<string>($"select Kind from WorkflowOperation where Id = '{operation.Id}'");
            Assert.Equal(operation.Id, loaded.Id);
            Assert.Equal(WorkflowOperationKind.Transferred, loaded.Kind);
            Assert.Equal("finance", loaded.TargetAssignee);
            Assert.Equal("Transferred", storedKind);

            var duplicate = new WorkflowOperation(instanceId, taskId, Guid.CreateVersion7(), nameof(SalesContract), operation.BusinessId, WorkflowOperationKind.Transferred, "admin", "finance", "重复", operation.DedupeKey, new DateTime(2026, 7, 15, 10, 1, 0));
            repository.Add(duplicate);
            Assert.Equal(operation.Id, duplicate.Id);
            Assert.Single(repository.List(instanceId: instanceId));
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }
}
