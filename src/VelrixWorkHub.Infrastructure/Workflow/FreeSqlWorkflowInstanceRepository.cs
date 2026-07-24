using FreeSql;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Workflow;

public sealed class FreeSqlWorkflowInstanceRepository(IFreeSql fsql) : IWorkflowInstanceRepository, IWorkflowInstanceLockRepository, IWorkflowInstanceCompensationRepository
{
    public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
    {
        var query = fsql.Select<WorkflowInstanceRecord>();
        if (!string.IsNullOrWhiteSpace(businessType)) query = query.Where(x => x.BusinessType == businessType.Trim());
        if (businessId is not null) query = query.Where(x => x.BusinessId == businessId.Value);
        if (status is not null) query = query.Where(x => x.Status == status.Value);
        return query.OrderByDescending(x => x.StartedAt).ToList().Select(ToDomain).ToArray();
    }

    public void Add(WorkflowInstance instance)
    {
        if (!TryAdd(instance)) throw new WorkflowRunningInstanceConflictException();
    }

    public bool TryAdd(WorkflowInstance instance)
    {
        var parameters = new
        {
            Id = instance.Id,
            DefinitionId = instance.DefinitionId,
            DefinitionCode = instance.DefinitionCode,
            DefinitionVersion = instance.DefinitionVersion,
            BusinessType = instance.BusinessType,
            BusinessId = instance.BusinessId,
            StartedBy = instance.StartedBy,
            DefinitionSnapshotJson = instance.DefinitionSnapshotJson,
            Status = instance.Status.ToString(),
            CurrentNodeId = instance.CurrentNodeId,
            StartedAt = instance.StartedAt,
            CompletedAt = instance.CompletedAt,
            PreviousInstanceId = instance.PreviousInstanceId,
            Revision = instance.Revision,
            ActiveNodeIdsJson = instance.ActiveNodeIdsJson,
            ParallelJoinArrivalsJson = instance.ParallelJoinArrivalsJson,
            LoopIterationsJson = instance.LoopIterationsJson,
            ApprovalAssigneesJson = instance.ApprovalAssigneesJson
        };
        var affected = fsql.Ado.DataType switch
        {
            DataType.PostgreSQL => fsql.Ado.ExecuteNonQuery("""
                INSERT INTO "WorkflowInstance" ("Id", "DefinitionId", "DefinitionCode", "DefinitionVersion", "BusinessType", "BusinessId", "StartedBy", "DefinitionSnapshotJson", "Status", "CurrentNodeId", "StartedAt", "CompletedAt", "PreviousInstanceId", "Revision", "ActiveNodeIdsJson", "ParallelJoinArrivalsJson", "LoopIterationsJson", "ApprovalAssigneesJson")
                VALUES (@Id, @DefinitionId, @DefinitionCode, @DefinitionVersion, @BusinessType, @BusinessId, @StartedBy, @DefinitionSnapshotJson, @Status, @CurrentNodeId, @StartedAt, @CompletedAt, @PreviousInstanceId, @Revision, @ActiveNodeIdsJson, @ParallelJoinArrivalsJson, @LoopIterationsJson, @ApprovalAssigneesJson)
                ON CONFLICT DO NOTHING;
                """, parameters),
            DataType.SqlServer => fsql.Ado.ExecuteNonQuery("""
                MERGE [WorkflowInstance] WITH (HOLDLOCK) AS target
                USING (VALUES (@Id, @DefinitionId, @DefinitionCode, @DefinitionVersion, @BusinessType, @BusinessId, @StartedBy, @DefinitionSnapshotJson, @Status, @CurrentNodeId, @StartedAt, @CompletedAt, @PreviousInstanceId, @Revision, @ActiveNodeIdsJson, @ParallelJoinArrivalsJson, @LoopIterationsJson, @ApprovalAssigneesJson))
                    AS source ([Id], [DefinitionId], [DefinitionCode], [DefinitionVersion], [BusinessType], [BusinessId], [StartedBy], [DefinitionSnapshotJson], [Status], [CurrentNodeId], [StartedAt], [CompletedAt], [PreviousInstanceId], [Revision], [ActiveNodeIdsJson], [ParallelJoinArrivalsJson], [LoopIterationsJson], [ApprovalAssigneesJson])
                ON target.[Id] = source.[Id]
                    OR (target.[BusinessType] = source.[BusinessType] AND target.[BusinessId] = source.[BusinessId] AND target.[DefinitionCode] = source.[DefinitionCode] AND target.[Status] = N'Running' AND source.[Status] = N'Running')
                WHEN NOT MATCHED THEN
                    INSERT ([Id], [DefinitionId], [DefinitionCode], [DefinitionVersion], [BusinessType], [BusinessId], [StartedBy], [DefinitionSnapshotJson], [Status], [CurrentNodeId], [StartedAt], [CompletedAt], [PreviousInstanceId], [Revision], [ActiveNodeIdsJson], [ParallelJoinArrivalsJson], [LoopIterationsJson], [ApprovalAssigneesJson])
                    VALUES (source.[Id], source.[DefinitionId], source.[DefinitionCode], source.[DefinitionVersion], source.[BusinessType], source.[BusinessId], source.[StartedBy], source.[DefinitionSnapshotJson], source.[Status], source.[CurrentNodeId], source.[StartedAt], source.[CompletedAt], source.[PreviousInstanceId], source.[Revision], source.[ActiveNodeIdsJson], source.[ParallelJoinArrivalsJson], source.[LoopIterationsJson], source.[ApprovalAssigneesJson]);
                """, parameters),
            // Existing SQLite fixtures rely on FreeSql auto-sync; retain that compatibility path.
            DataType.Sqlite => fsql.InsertOrUpdate<WorkflowInstanceRecord>()
                .SetSource(ToRecord(instance))
                .IfExistsDoNothing()
                .ExecuteAffrows(),
            _ => throw new NotSupportedException($"Workflow 实例 TryAdd 暂不支持数据库类型：{fsql.Ado.DataType}")
        };
        return affected == 1;
    }

    public void Remove(Guid instanceId)
        => fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == instanceId).ExecuteAffrows();

    public void Update(WorkflowInstance instance)
    {
        if (!TryUpdate(instance)) throw new InvalidOperationException("流程实例状态已变化，请刷新后重试。");
    }

    public bool TryUpdate(WorkflowInstance instance)
    {
        var expectedRevision = instance.Revision;
        var nextRevision = checked(expectedRevision + 1);
        var rows = fsql.Update<WorkflowInstanceRecord>()
            .SetSource(ToRecord(instance, nextRevision))
            .Where(x => x.Id == instance.Id && x.Revision == expectedRevision)
            .ExecuteAffrows();
        if (rows != 1) return false;
        instance.MarkPersistedRevision(nextRevision);
        return true;
    }

    public void LockForUpdate(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (fsql.Ado.DataType == DataType.Sqlite)
            return;

        var record = fsql.Ado.DataType switch
        {
            DataType.PostgreSQL => fsql.Ado.Query<WorkflowInstanceRecord>(
                """SELECT * FROM "WorkflowInstance" WHERE "Id" = @Id FOR UPDATE""",
                new { Id = instance.Id }).SingleOrDefault(),
            DataType.SqlServer => fsql.Ado.Query<WorkflowInstanceRecord>(
                """SELECT * FROM [WorkflowInstance] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @Id""",
                new { Id = instance.Id }).SingleOrDefault(),
            _ => throw new NotSupportedException($"Workflow 实例行锁暂不支持数据库类型：{fsql.Ado.DataType}")
        };

        if (record is null || record.Revision != instance.Revision || record.Status != WorkflowInstanceStatus.Running)
            throw new InvalidOperationException("流程实例状态已变化，请刷新后重试。");
    }

    private static WorkflowInstanceRecord ToRecord(WorkflowInstance item, long? revision = null) => new()
    {
        Id = item.Id, DefinitionId = item.DefinitionId, DefinitionCode = item.DefinitionCode, DefinitionVersion = item.DefinitionVersion,
        BusinessType = item.BusinessType, BusinessId = item.BusinessId, StartedBy = item.StartedBy, DefinitionSnapshotJson = item.DefinitionSnapshotJson,
        Status = item.Status, CurrentNodeId = item.CurrentNodeId, StartedAt = item.StartedAt, CompletedAt = item.CompletedAt, PreviousInstanceId = item.PreviousInstanceId,
        Revision = revision ?? item.Revision, ActiveNodeIdsJson = item.ActiveNodeIdsJson, ParallelJoinArrivalsJson = item.ParallelJoinArrivalsJson, LoopIterationsJson = item.LoopIterationsJson,
        ApprovalAssigneesJson = item.ApprovalAssigneesJson
    };

    private static WorkflowInstance ToDomain(WorkflowInstanceRecord record)
    {
        DateTime? completedAt = record.CompletedAt is { } value && value != default ? value : null;
        return WorkflowInstance.Rehydrate(
            record.Id, record.DefinitionId, record.DefinitionCode, record.DefinitionVersion, record.BusinessType, record.BusinessId, record.StartedBy,
            record.DefinitionSnapshotJson, record.Status, record.CurrentNodeId, record.StartedAt, completedAt, record.PreviousInstanceId, record.Revision, record.ActiveNodeIdsJson, record.ParallelJoinArrivalsJson, record.LoopIterationsJson, record.ApprovalAssigneesJson);
    }
}
