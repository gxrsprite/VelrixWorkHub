using FreeSql;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Workflow;

public sealed class FreeSqlWorkflowOperationRepository(IFreeSql fsql) : IWorkflowOperationRepository
{
    public IReadOnlyList<WorkflowOperation> List(Guid? instanceId = null, string? businessType = null, Guid? businessId = null, WorkflowOperationKind? kind = null)
    {
        var query = fsql.Select<WorkflowOperationRecord>();
        if (instanceId is not null) query = query.Where(x => x.InstanceId == instanceId.Value);
        if (!string.IsNullOrWhiteSpace(businessType)) query = query.Where(x => x.BusinessType == businessType.Trim());
        if (businessId is not null) query = query.Where(x => x.BusinessId == businessId.Value);
        if (kind is not null) query = query.Where(x => x.Kind == kind.Value);
        return query.OrderBy(x => x.OccurredAt).ToList().Select(ToDomain).ToArray();
    }

    public WorkflowOperation? FindByDedupeKey(string dedupeKey)
        => fsql.Select<WorkflowOperationRecord>().Where(x => x.DedupeKey == dedupeKey.Trim()).ToList().Select(ToDomain).FirstOrDefault();

    public void Add(WorkflowOperation operation)
        => fsql.InsertOrUpdate<WorkflowOperationRecord>()
            .SetSource(ToRecord(operation))
            .IfExistsDoNothing()
            .ExecuteAffrows();

    public bool TryAdd(WorkflowOperation operation)
    {
        var parameters = new
        {
            Id = operation.Id,
            InstanceId = operation.InstanceId,
            TaskId = operation.TaskId,
            NodeId = operation.NodeId,
            BusinessType = operation.BusinessType,
            BusinessId = operation.BusinessId,
            Kind = operation.Kind.ToString(),
            Actor = operation.Actor,
            TargetAssignee = operation.TargetAssignee,
            Comment = operation.Comment,
            DedupeKey = operation.DedupeKey,
            OccurredAt = operation.OccurredAt
        };
        var affected = fsql.Ado.DataType switch
        {
            DataType.PostgreSQL => fsql.Ado.ExecuteNonQuery("""
                INSERT INTO "WorkflowOperation" ("Id", "InstanceId", "TaskId", "NodeId", "BusinessType", "BusinessId", "Kind", "Actor", "TargetAssignee", "Comment", "DedupeKey", "OccurredAt")
                VALUES (@Id, @InstanceId, @TaskId, @NodeId, @BusinessType, @BusinessId, @Kind, @Actor, @TargetAssignee, @Comment, @DedupeKey, @OccurredAt)
                ON CONFLICT DO NOTHING;
                """, parameters),
            DataType.Sqlite => fsql.InsertOrUpdate<WorkflowOperationRecord>()
                .SetSource(ToRecord(operation))
                .IfExistsDoNothing()
                .ExecuteAffrows(),
            DataType.SqlServer => fsql.Ado.ExecuteNonQuery("""
                MERGE [WorkflowOperation] WITH (HOLDLOCK) AS target
                USING (VALUES (@Id, @InstanceId, @TaskId, @NodeId, @BusinessType, @BusinessId, @Kind, @Actor, @TargetAssignee, @Comment, @DedupeKey, @OccurredAt))
                    AS source ([Id], [InstanceId], [TaskId], [NodeId], [BusinessType], [BusinessId], [Kind], [Actor], [TargetAssignee], [Comment], [DedupeKey], [OccurredAt])
                ON target.[DedupeKey] = source.[DedupeKey]
                WHEN NOT MATCHED THEN
                    INSERT ([Id], [InstanceId], [TaskId], [NodeId], [BusinessType], [BusinessId], [Kind], [Actor], [TargetAssignee], [Comment], [DedupeKey], [OccurredAt])
                    VALUES (source.[Id], source.[InstanceId], source.[TaskId], source.[NodeId], source.[BusinessType], source.[BusinessId], source.[Kind], source.[Actor], source.[TargetAssignee], source.[Comment], source.[DedupeKey], source.[OccurredAt]);
                """, parameters),
            _ => throw new NotSupportedException($"Workflow 操作历史 TryAdd 暂不支持数据库类型：{fsql.Ado.DataType}")
        };
        return affected == 1;
    }

    private static WorkflowOperationRecord ToRecord(WorkflowOperation item) => new()
    {
        Id = item.Id, InstanceId = item.InstanceId, TaskId = item.TaskId, NodeId = item.NodeId, BusinessType = item.BusinessType,
        BusinessId = item.BusinessId, Kind = item.Kind, Actor = item.Actor, TargetAssignee = item.TargetAssignee,
        Comment = item.Comment, DedupeKey = item.DedupeKey, OccurredAt = item.OccurredAt
    };

    private static WorkflowOperation ToDomain(WorkflowOperationRecord item) => WorkflowOperation.Rehydrate(
        item.Id, item.InstanceId, item.TaskId, item.NodeId, item.BusinessType, item.BusinessId, item.Kind, item.Actor,
        item.TargetAssignee, item.Comment, item.DedupeKey, item.OccurredAt);
}
