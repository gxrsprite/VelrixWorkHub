using FreeSql;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Workflow;

public sealed class FreeSqlWorkflowTaskRepository(IFreeSql fsql) : IWorkflowTaskRepository, IWorkflowTaskCompensationRepository
{
    public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
    {
        var query = fsql.Select<WorkflowTaskRecord>();
        if (instanceId is not null) query = query.Where(x => x.InstanceId == instanceId.Value);
        if (!string.IsNullOrWhiteSpace(assignee)) query = query.Where(x => x.Assignee == assignee.Trim());
        if (status is not null) query = query.Where(x => x.Status == status.Value);
        return query.OrderByDescending(x => x.CreatedAt).ToList().Select(ToDomain).ToArray();
    }

    public void Add(WorkflowTask task) => TryAdd(task);

    public bool TryAdd(WorkflowTask task)
    {
        var parameters = new
        {
            Id = task.Id,
            InstanceId = task.InstanceId,
            DefinitionId = task.DefinitionId,
            DefinitionVersion = task.DefinitionVersion,
            NodeId = task.NodeId,
            NodeName = task.NodeName,
            BusinessType = task.BusinessType,
            BusinessId = task.BusinessId,
            Assignee = task.Assignee,
            Status = task.Status.ToString(),
            TransferTarget = task.TransferTarget,
            DecisionComment = task.DecisionComment,
            DecisionActor = task.DecisionActor,
            CreatedAt = task.CreatedAt,
            CompletedAt = task.CompletedAt,
            Revision = task.Revision,
            Round = task.Round
        };
        var affected = fsql.Ado.DataType switch
        {
            DataType.PostgreSQL => fsql.Ado.ExecuteNonQuery("""
                INSERT INTO "WorkflowTask" ("Id", "InstanceId", "DefinitionId", "DefinitionVersion", "NodeId", "NodeName", "BusinessType", "BusinessId", "Assignee", "Status", "TransferTarget", "DecisionComment", "DecisionActor", "CreatedAt", "CompletedAt", "Revision", "Round")
                SELECT @Id, @InstanceId, @DefinitionId, @DefinitionVersion, @NodeId, @NodeName, @BusinessType, @BusinessId, @Assignee, @Status, @TransferTarget, @DecisionComment, @DecisionActor, @CreatedAt, @CompletedAt, @Revision, @Round
                FROM "WorkflowInstance" AS instance
                WHERE instance."Id" = @InstanceId AND instance."Status" = 'Running'
                FOR UPDATE
                ON CONFLICT ("Id") DO NOTHING;
                """, parameters),
            // Existing SQLite fixtures rely on FreeSql auto-sync. Keep that compatibility path
            // while PostgreSQL and SQL Server use explicit database-native atomic statements.
            DataType.Sqlite => fsql.InsertOrUpdate<WorkflowTaskRecord>()
                .SetSource(ToRecord(task))
                .IfExistsDoNothing()
                .ExecuteAffrows(),
            DataType.SqlServer => fsql.Ado.ExecuteNonQuery("""
                MERGE [WorkflowTask] WITH (HOLDLOCK) AS target
                USING (
                    SELECT @Id AS [Id], @InstanceId AS [InstanceId], @DefinitionId AS [DefinitionId], @DefinitionVersion AS [DefinitionVersion], @NodeId AS [NodeId], @NodeName AS [NodeName], @BusinessType AS [BusinessType], @BusinessId AS [BusinessId], @Assignee AS [Assignee], @Status AS [Status], @TransferTarget AS [TransferTarget], @DecisionComment AS [DecisionComment], @DecisionActor AS [DecisionActor], @CreatedAt AS [CreatedAt], @CompletedAt AS [CompletedAt], @Revision AS [Revision], @Round AS [Round]
                    FROM [WorkflowInstance] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [Id] = @InstanceId AND [Status] = N'Running'
                )
                    AS source ([Id], [InstanceId], [DefinitionId], [DefinitionVersion], [NodeId], [NodeName], [BusinessType], [BusinessId], [Assignee], [Status], [TransferTarget], [DecisionComment], [DecisionActor], [CreatedAt], [CompletedAt], [Revision], [Round])
                ON target.[Id] = source.[Id]
                WHEN NOT MATCHED THEN
                    INSERT ([Id], [InstanceId], [DefinitionId], [DefinitionVersion], [NodeId], [NodeName], [BusinessType], [BusinessId], [Assignee], [Status], [TransferTarget], [DecisionComment], [DecisionActor], [CreatedAt], [CompletedAt], [Revision], [Round])
                    VALUES (source.[Id], source.[InstanceId], source.[DefinitionId], source.[DefinitionVersion], source.[NodeId], source.[NodeName], source.[BusinessType], source.[BusinessId], source.[Assignee], source.[Status], source.[TransferTarget], source.[DecisionComment], source.[DecisionActor], source.[CreatedAt], source.[CompletedAt], source.[Revision], source.[Round]);
                """, parameters),
            _ => throw new NotSupportedException($"Workflow 待办 TryAdd 暂不支持数据库类型：{fsql.Ado.DataType}")
        };
        return affected == 1;
    }

    public void Update(WorkflowTask task)
    {
        if (!TryUpdate(task)) throw new InvalidOperationException("审批待办状态已变化，请刷新后重试。");
    }

    public bool TryUpdate(WorkflowTask task)
    {
        var expectedRevision = task.Revision;
        var nextRevision = checked(expectedRevision + 1);
        var rows = fsql.Update<WorkflowTaskRecord>()
            .SetSource(ToRecord(task, nextRevision))
            .Where(x => x.Id == task.Id && x.Revision == expectedRevision)
            .ExecuteAffrows();
        if (rows != 1) return false;
        task.MarkPersistedRevision(nextRevision);
        return true;
    }

    public void Remove(Guid taskId)
        => fsql.Delete<WorkflowTaskRecord>().Where(x => x.Id == taskId).ExecuteAffrows();

    private static WorkflowTaskRecord ToRecord(WorkflowTask item, long? revision = null) => new()
    {
        Id = item.Id, InstanceId = item.InstanceId, DefinitionId = item.DefinitionId, DefinitionVersion = item.DefinitionVersion,
        NodeId = item.NodeId, NodeName = item.NodeName, BusinessType = item.BusinessType, BusinessId = item.BusinessId,
        Assignee = item.Assignee, Status = item.Status, TransferTarget = item.TransferTarget, DecisionComment = item.DecisionComment, DecisionActor = item.DecisionActor,
        CreatedAt = item.CreatedAt, CompletedAt = item.CompletedAt, Revision = revision ?? item.Revision, Round = item.Round
    };

    private static WorkflowTask ToDomain(WorkflowTaskRecord record) => WorkflowTask.Rehydrate(
        record.Id, record.InstanceId, record.DefinitionId, record.DefinitionVersion, record.NodeId, record.NodeName,
        record.BusinessType, record.BusinessId, record.Assignee, record.Status,
        record.DecisionComment, record.DecisionActor, record.CreatedAt, record.CompletedAt, record.TransferTarget, record.Revision, record.Round);
}
